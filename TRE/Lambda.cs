
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

using Api = UK.Gov.NationalArchives.Judgments.Api;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UK.Gov.NationalArchives.CaseLaw.TRE {

public class Lambda {

    private readonly UK.Gov.Legislation.Judgments.CustomLoggerProvider loggerProvider;
    private readonly ILogger logger;

    private readonly string logFilename = "parser.log";

    public Lambda() {
        loggerProvider = new UK.Gov.Legislation.Judgments.CustomLoggerProvider();
        UK.Gov.Legislation.Judgments.Logging.Factory.AddProvider(loggerProvider);
        logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<Lambda>();
    }

    public Stream FunctionHandler(Stream input) {
        using MemoryStream ms = new MemoryStream();
        input.CopyTo(ms);
        Request request = JsonSerializer.Deserialize<Request>(ms.ToArray());
        Response response = Helper1(request);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(response);
        return new MemoryStream(json);
    }

    private Response Helper1(Request request) {
        ParserOutputs outputs = Helper2(request.Inputs);
        return new Response { Outputs = outputs };
    }

    private ParserOutputs Helper2(ParserInputs inputs) {
        List<string> errors = new List<string>();
        if (inputs is null) {
            logger.LogError("parser-inputs is null");
            errors.Add("parser-inputs is null");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }
        byte[] docx;
        try {
            docx = ReadDocx(inputs);
        } catch (Exception e) {
            logger.LogError(e, e.Message);
            logger.LogError(e, "error reading .docx file");
            errors.Add("error reading .docx file");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }
        List<Api.Attachment> attachments = new List<Api.Attachment>();
        if (inputs.AttachmentURLs is not null) {
            using HttpClient http = new HttpClient();
            foreach (string url in inputs.AttachmentURLs) {
                try {
                    byte[] content = http.GetByteArrayAsync(url).Result;
                    attachments.Add(new Api.Attachment { Content = content });
                } catch (Exception e) {
                    logger.LogError(e, e.Message);
                    logger.LogError(e, "error reading attachment");
                    errors.Add("error reading attachment");
                }
            }
        }
        Api.Hint? hint;
        try {
            hint = InputHelper.GetHint(inputs, logger);
        } catch (Exception e) {
            logger.LogError(e, "error reading document type");
            errors.Add("error reading document type");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }

        Api.Meta meta;
        try {
            meta = InputHelper.GetMetadata(inputs, logger);
        } catch (Exception e) {
            logger.LogError(e, "error reading metadata");
            errors.Add("error reading metadata");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }

        Api.Response response;
        try {
            response = Api.Parser.Parse(new Api.Request { Content = docx, Attachments = attachments, Hint = hint, Meta = meta });
        } catch (Exception e) {
            logger.LogError(e, "parse error");
            errors.Add("error parsing document");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }
        string xmlFilename = inputs.ConsignmentReference + ".xml";
        try {
            Save(inputs.S3Bucket, inputs.S3OutputPrefix, xmlFilename, Encoding.UTF8.GetBytes(response.Xml), "application/xml");
        } catch (Exception e) {
            logger.LogError(e, "error saving xml");
            errors.Add("error saving xml");
            xmlFilename = null;
        }
        string metadataFilename = "metadata.json";
        try {
            byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(response.Meta, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Save(inputs.S3Bucket, inputs.S3OutputPrefix, metadataFilename, metadata, "application/json");
        } catch (Exception e) {
            logger.LogError(e, "error saving metadata");
            errors.Add("error saving metadata");
            metadataFilename = null;
        }
        List<string> imageFilenames = new List<string>();
        foreach (var image in response.Images) {
            try {
                Save(inputs.S3Bucket, inputs.S3OutputPrefix, image.Name, image.Content, image.Type);
                imageFilenames.Add(image.Name);
            } catch (Exception e) {
                // logger.LogError(e, e.Message);
                logger.LogError(e, "error saving image { name }", image.Name);
                errors.Add("error saving image " + image.Name);
            }
        }
        bool saveLogWasSuccessful;
        try {
            ClearAndSaveLog(inputs.S3Bucket, inputs.S3OutputPrefix);
            saveLogWasSuccessful = true;
        } catch (Exception) {
            errors.Add("error saving log file");
            saveLogWasSuccessful = false;
        }
        return new ParserOutputs {
            XMLFilename = xmlFilename,
            MetadataFilename = metadataFilename,
            ImageFilenames = imageFilenames,
            LogFilename = saveLogWasSuccessful ? logFilename : null,
            ErrorMessages = errors
        };
    }

    private PutObjectResponse Save(string bucket, string prefix, string filename, byte[] content, string type) {
        PutObjectRequest request = new PutObjectRequest() {
            BucketName = bucket,
            Key = prefix + filename,
            ContentType = type,
            InputStream = new MemoryStream(content)
        };
        AmazonS3Client s3 = new AmazonS3Client();
        return s3.PutObjectAsync(request).Result;
    }

    private ParserOutputs ClearAndSaveLogAndReturnErrors(ParserInputs inputs, List<string> errors) {
        try {
            ClearAndSaveLog(inputs.S3Bucket, inputs.S3OutputPrefix);
            return new ParserOutputs {
                LogFilename = logFilename,
                ErrorMessages = errors
            };
        } catch (Exception) {
            errors.Add("error saving log");
            return new ParserOutputs {
                ErrorMessages = errors
            };
        }
    }

    private void ClearAndSaveLog(string bucket, string prefix) {
        IList<UK.Gov.Legislation.Judgments.LogMessage> messages = loggerProvider.Reset();
        string log = UK.Gov.Legislation.Judgments.CustomLoggerProvider.ToJson(messages);
        Save(bucket, prefix, logFilename, Encoding.UTF8.GetBytes(log), "application/json");
    }

    private byte[] ReadDocx(ParserInputs inputs) {
        if (!string.IsNullOrEmpty(inputs.DocumentUrl)) {
            using HttpClient http = new HttpClient();
            var task = http.GetByteArrayAsync(inputs.DocumentUrl);
            return task.Result;
        } else {
            AmazonS3Client s3 = new AmazonS3Client();
            GetObjectRequest request = new GetObjectRequest {
                BucketName = inputs.S3InputBucket,
                Key = inputs.S3InputKey
            };
            var task = s3.GetObjectAsync(request);
            var response = task.Result;
            using var ms = new MemoryStream();
            response.ResponseStream.CopyTo(ms);
            return ms.ToArray();
        }
    }

}

public class Response {

    [JsonPropertyName("parser-outputs")]
    public ParserOutputs Outputs { get; set; }

    public static Response Error(string error) => Response.Errors(new List<string>(1) { error });

    public static Response Errors(IEnumerable<string> errors) =>
        new Response { Outputs = new ParserOutputs { ErrorMessages = errors } };

}

public class ParserOutputs {

    [JsonPropertyName("xml")]
    public string XMLFilename { get; set; }

    [JsonPropertyName("metadata")]
    public string MetadataFilename { get; set; }

    [JsonPropertyName("images")]
    public IEnumerable<string> ImageFilenames { get; set; }

    [JsonPropertyName("log")]
    public string LogFilename { get; set; }

    [JsonPropertyName("error-messages")]
    public IEnumerable<string> ErrorMessages { get; set; }

}

}
