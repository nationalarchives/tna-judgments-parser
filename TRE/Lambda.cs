using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using TRE.FindCaseLaw.Inputs;
using TRE.FindCaseLaw.Outputs;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using Logging = UK.Gov.Legislation.Judgments.Logging;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace UK.Gov.NationalArchives.CaseLaw.TRE;

public class Lambda
{
    private readonly CustomLoggerProvider loggerProvider;
    private readonly ILogger logger;

    private readonly string logFilename = "parser.log";
    private readonly Api.Parser parser;
    private readonly IInputHelper inputHelper = new InputHelper();

    public Lambda()
    {
        loggerProvider = new CustomLoggerProvider();
        Logging.Factory.AddProvider(loggerProvider);
        logger = Logging.Factory.CreateLogger<Lambda>();
        parser = new Api.Parser(Logging.Factory.CreateLogger<Api.Parser>(), new Validator());
    }

    public Stream FunctionHandler(Stream input)
    {
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        var request = JsonSerializer.Deserialize<Request>(ms.ToArray());
        var response = Helper1(request);
        var json = JsonSerializer.SerializeToUtf8Bytes(response);
        return new MemoryStream(json);
    }

    private Response Helper1(Request request)
    {
        var outputs = Helper2(request.Inputs);
        return new Response { Outputs = outputs };
    }

    private ParserOutputs Helper2(ParserInputs inputs)
    {
        var errors = new List<string>();
        if (inputs is null)
        {
            logger.LogError("parser-inputs is null");
            errors.Add("parser-inputs is null");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }

        byte[] docx;
        try
        {
            docx = ReadDocx(inputs);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error reading .docx file: {}", e.Message);
            errors.Add("error reading .docx file");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }

        var attachments = new List<Api.Attachment>();
        if (inputs.AttachmentURLs is not null)
        {
            using var http = new HttpClient();
            foreach (var url in inputs.AttachmentURLs)
            {
                try
                {
                    var content = http.GetByteArrayAsync(url).Result;
                    attachments.Add(new Api.Attachment { Content = content });
                }
                catch (Exception e)
                {
                    logger.LogError(e, "error reading attachment: {}", e.Message);
                    errors.Add("error reading attachment");
                }
            }
        }

        Api.Hint? hint;
        try
        {
            hint = inputHelper.GetHint(inputs, logger);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error reading document type: {}", e.Message);
            errors.Add("error reading document type");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }

        Api.Meta meta;
        try
        {
            meta = inputHelper.GetMetadata(inputs, logger);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error reading metadata: {}", e.Message);
            errors.Add("error reading metadata");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }

        Api.Response response;
        try
        {
            response = parser.Parse(new Api.Request
            {
                Content = docx,
                Attachments = attachments,
                Hint = hint,
                Meta = meta
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "parse error: {}", e.Message);
            errors.Add("error parsing document");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }

        var xmlFilename = inputs.ConsignmentReference + ".xml";
        try
        {
            Save(inputs.S3Bucket, inputs.S3OutputPrefix, xmlFilename, Encoding.UTF8.GetBytes(response.Xml),
                "application/xml");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error saving xml: {}", e.Message);
            errors.Add("error saving xml");
            xmlFilename = null;
        }

        var metadataFilename = "metadata.json";
        try
        {
            var metadata = JsonSerializer.SerializeToUtf8Bytes(response.Meta,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Save(inputs.S3Bucket, inputs.S3OutputPrefix, metadataFilename, metadata, "application/json");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error saving metadata: {}", e.Message);
            errors.Add("error saving metadata");
            metadataFilename = null;
        }

        var imageFilenames = new List<string>();
        foreach (var image in response.Images)
        {
            try
            {
                Save(inputs.S3Bucket, inputs.S3OutputPrefix, image.Name, image.Content, image.Type);
                imageFilenames.Add(image.Name);
            }
            catch (Exception e)
            {
                logger.LogError(e, "error saving image {}: {}", image.Name, e.Message);
                errors.Add("error saving image " + image.Name);
            }
        }

        bool saveLogWasSuccessful;
        try
        {
            ClearAndSaveLog(inputs.S3Bucket, inputs.S3OutputPrefix);
            saveLogWasSuccessful = true;
        }
        catch (Exception)
        {
            errors.Add("error saving log file");
            saveLogWasSuccessful = false;
        }

        return new ParserOutputs
        {
            XMLFilename = xmlFilename,
            MetadataFilename = metadataFilename,
            ImageFilenames = imageFilenames,
            LogFilename = saveLogWasSuccessful ? logFilename : null,
            ErrorMessages = errors
        };
    }

    private PutObjectResponse Save(string bucket, string prefix, string filename, byte[] content, string type)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = prefix + filename,
            ContentType = type,
            InputStream = new MemoryStream(content)
        };
        var s3 = new AmazonS3Client();
        return s3.PutObjectAsync(request).Result;
    }

    private ParserOutputs ClearAndSaveLogAndReturnErrors(ParserInputs inputs, List<string> errors)
    {
        try
        {
            ClearAndSaveLog(inputs.S3Bucket, inputs.S3OutputPrefix);
            return new ParserOutputs { LogFilename = logFilename, ErrorMessages = errors };
        }
        catch (Exception)
        {
            errors.Add("error saving log");
            return new ParserOutputs { ErrorMessages = errors };
        }
    }

    private void ClearAndSaveLog(string bucket, string prefix)
    {
        var messages = loggerProvider.Reset();
        var log = CustomLoggerProvider.ToJson(messages);
        Save(bucket, prefix, logFilename, Encoding.UTF8.GetBytes(log), "application/json");
    }

    private byte[] ReadDocx(ParserInputs inputs)
    {
        if (!string.IsNullOrEmpty(inputs.DocumentUrl))
        {
            using var http = new HttpClient();
            var task = http.GetByteArrayAsync(inputs.DocumentUrl);
            return task.Result;
        }
        else
        {
            var s3 = new AmazonS3Client();
            var request = new GetObjectRequest { BucketName = inputs.S3InputBucket, Key = inputs.S3InputKey };
            var task = s3.GetObjectAsync(request);
            var response = task.Result;
            using var ms = new MemoryStream();
            response.ResponseStream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
