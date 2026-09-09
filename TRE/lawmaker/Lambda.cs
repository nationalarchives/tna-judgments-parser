
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Lawmaker;

using Api = UK.Gov.Legislation.Lawmaker.Api;

namespace UK.Gov.NationalArchives.CaseLaw.TRE.Lawmaker;


public class Lambda
{

    private readonly UK.Gov.Legislation.Judgments.CustomLoggerProvider loggerProvider;
    private readonly ILogger logger;

    private readonly string logFilename = "parser.log";

    public Lambda()
    {
        loggerProvider = new UK.Gov.Legislation.Judgments.CustomLoggerProvider();
        UK.Gov.Legislation.Judgments.Logging.Factory.AddProvider(loggerProvider);
        logger = UK.Gov.Legislation.Judgments.Logging.Factory.CreateLogger<Lambda>();
    }

    public Stream FunctionHandler(Stream input)
    {
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        Request request = JsonSerializer.Deserialize<Request>(ms.ToArray());
        Response response = Helper1(request);
        var json = JsonSerializer.SerializeToUtf8Bytes(response);
        return new MemoryStream(json);
    }

    private Response Helper1(Request request)
    {
        ParserOutputs outputs = Helper2(request.Inputs);
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
        DocName docName;
        try
        {
            DocName? tmp = DocNames.GetDocName(inputs.DocumentType);
            if (tmp == null) throw new Exception();
            docName = (DocName)tmp;
        }
        catch (Exception e)
        {
            logger.LogError(e, "error reading document type: {}", e.Message);
            errors.Add("error reading document type");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }

        var subType = inputs.SubType;
        var procedure = inputs.Procedure;
        var languages = inputs.Languages;

        Api.Response response;
        try
        {
            var request = new Api.Request { Content = docx, DocName = docName };
            logger.LogInformation("Got content with doctype: {}. Creating response...", request.DocName);
            var classifier = new LegislationClassifier(docName, subType, procedure);
            var languageService = new LanguageService(languages);
            response = Helper.LambdaParse(request, classifier, languageService);
        }
        catch (Exception e)
        {
            logger.LogError(e, "parse error: {}", e.Message);
            errors.Add("error parsing document");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }
        if (response.Error is not null)
        {
            logger.LogError("parsing failed at block {}: {}\nblock text: {}", response.Error.BlockNumber, response.Error.Message, response.Error.BlockText);
            errors.Add($"parsing failed at block {response.Error.BlockNumber}: {response.Error.Message}\nblock text: {response.Error.BlockText}");
            return ClearAndSaveLogAndReturnErrors(inputs, errors);
        }
        var xmlFilename = inputs.ConsignmentReference + ".xml";
        try
        {
            Save(inputs.S3Bucket, inputs.S3OutputPrefix, xmlFilename, Encoding.UTF8.GetBytes(response.Xml), "application/xml");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error saving xml: {}", e.Message);
            errors.Add("error saving xml");
            xmlFilename = null;
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
            ImageFilenames = imageFilenames,
            LogFilename = saveLogWasSuccessful ? logFilename : null,
            ErrorMessages = errors
        };
    }

    private PutObjectResponse Save(string bucket, string prefix, string filename, byte[] content, string type)
    {
        var request = new PutObjectRequest()
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
            return new ParserOutputs
            {
                LogFilename = logFilename,
                ErrorMessages = errors
            };
        }
        catch (Exception)
        {
            errors.Add("error saving log");
            return new ParserOutputs
            {
                ErrorMessages = errors
            };
        }
    }

    private void ClearAndSaveLog(string bucket, string prefix)
    {
        IList<UK.Gov.Legislation.Judgments.LogMessage> messages = loggerProvider.Reset();
        var log = UK.Gov.Legislation.Judgments.CustomLoggerProvider.ToJson(messages);
        Save(bucket, prefix, logFilename, Encoding.UTF8.GetBytes(log), "application/json");
    }

    private byte[] ReadDocx(ParserInputs inputs)
    {
        var s3 = new AmazonS3Client();
        var request = new GetObjectRequest
        {
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
