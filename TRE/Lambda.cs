
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    private static readonly ILogger logger;

    static Lambda() {
        string logFile = Path.GetTempPath() + "log-{Date}.txt";
        UK.Gov.Legislation.Judgments.Logging.SetConsoleAndFile(new FileInfo(logFile), LogLevel.Debug);
        logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<Lambda>();
    }

    public Stream FunctionHandler(Stream input) {
        using MemoryStream ms = new MemoryStream();
        input.CopyTo(ms);
        Request request = JsonSerializer.Deserialize<Request>(ms.ToArray());
        Response response = FunctionHandler2(request);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(response);
        return new MemoryStream(json);
    }

    private Response FunctionHandler2(Request request) {
        try {
            string logFile = Path.GetTempPath() + "log-" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
            File.WriteAllText(logFile, string.Empty);
        } catch (Exception) {
        }
        ParserInputs inputs = request.Inputs;
        if (inputs is null) {
            logger.LogError("parser-inputs is null");
            return Response.Error("parser-inputs is null");
        }
        using HttpClient http = new HttpClient();
        byte[] docx;
        try {
            docx = http.GetByteArrayAsync(request.Inputs.DocumentUrl).Result;
        } catch (Exception e) {
            logger.LogError(e, "read error");
            return Response.Error("error reading .docx file");
        }
        List<string> errors = new List<string>();
        List<Api.Attachment> attachments = new List<Api.Attachment>();
        if (inputs.AttachmentURLs is not null) {
            foreach (string url in inputs.AttachmentURLs) {
                try {
                    byte[] content = http.GetByteArrayAsync(url).Result;
                    attachments.Add(new Api.Attachment { Content = content });
                } catch (Exception e) {
                    logger.LogError(e, "read error");
                    errors.Add("error reading attachment");
                }
            }
        }
        Api.Response response;
        try {
            response = Api.Parser.Parse(new Api.Request { Content = docx, Attachments = attachments });
        } catch (Exception e) {
            logger.LogError(e, "parse error");
            errors.Add("error parsing document");
            return Response.Errors(errors);
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
                logger.LogError(e, "error saving image { name }", image.Name);
                errors.Add("error saving image " + image.Name);
            }
        }
        string logFilename = "parser.log";
        try {
            string logFile = Path.GetTempPath() + "log-" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
            byte[] log = File.ReadAllBytes(logFile);
            // File.WriteAllText(logFile, string.Empty);
            Save(inputs.S3Bucket, inputs.S3OutputPrefix, logFilename, log, "text/plain");
        } catch (Exception e) {
            errors.Add("error saving log file");
            logFilename = null;
        }
        return new Response { Outputs = new ParserOutputs {
            XMLFilename = xmlFilename,
            MetadataFilename = metadataFilename,
            ImageFilenames = imageFilenames,
            LogFilename = logFilename,
            ErrorMessages = errors
        } };
    }

    private static PutObjectResponse Save(string bucket, string prefix, string filename, byte[] content, string type) {
        logger.LogInformation("saving " + prefix + filename);
        PutObjectRequest request = new PutObjectRequest() {
            BucketName = bucket,
            Key = prefix + filename,
            ContentType = type,
            InputStream = new MemoryStream(content)
        };
        AmazonS3Client s3 = new AmazonS3Client();
        return s3.PutObjectAsync(request).Result;
    }

}

public class Request {

    [JsonPropertyName("parser-inputs")]
    public ParserInputs Inputs { get; set; }

}

public class ParserInputs {

    [JsonPropertyName("consignment-reference")]
    public string ConsignmentReference { get; set; }

    [JsonPropertyName("document-url")]
    public string DocumentUrl { get; set; }

    [JsonPropertyName("attachment-urls")]
    public IEnumerable<string> AttachmentURLs { get; set; }

    [JsonPropertyName("s3-bucket")]
    public string S3Bucket { get; set; }

    [JsonPropertyName("s3-output-prefix")]
    public string S3OutputPrefix { get; set; }

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
