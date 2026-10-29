#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using TRE.FindCaseLaw.Inputs;
using TRE.FindCaseLaw.Outputs;

using UK.Gov.Legislation.Judgments;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace TRE.FindCaseLaw;

public class Worker(
    ILogger<Worker> logger,
    Api.IParser parser,
    ICustomLoggerProvider loggerProvider,
    IAmazonS3 s3,
    IInputHelper inputHelper,
    IExternalResourceRetriever externalResourceRetriever)
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string LogFilename = "parser.log";
    private const string MetadataFilename = "metadata.json";

    private string s3Bucket = string.Empty;
    private string s3OutputPrefix = string.Empty;
    private string consignmentReference = string.Empty;

    public Stream Process(Stream input)
    {
        var outputs = ProcessDocumentAndSaveToS3(input);

        outputs = FinaliseLogs(outputs);

        var response = new Response { Outputs = outputs };

        var json = JsonSerializer.SerializeToUtf8Bytes(response);

        return new MemoryStream(json);
    }

    private ParserOutputs FinaliseLogs(ParserOutputs outputs)
    {
        var allLogMessages = loggerProvider.Reset();

        var log = CustomLoggerProvider.ToJson(allLogMessages);

        if (TrySaveToS3(LogFilename, Encoding.UTF8.GetBytes(log), "application/json"))
        {
            outputs.LogFilename = LogFilename;
        }
        else
        {
            outputs.LogFilename = null;

            // Reset the log provider again to get the error log message about failing to save the logs to S3
            allLogMessages = [.. allLogMessages, .. loggerProvider.Reset()];
        }

        outputs.ErrorMessages = allLogMessages.Where(message => message.Level == LogLevel.Error)
                                              .Select(message => message.Message)
                                              .ToList();

        return outputs;
    }

    private ParserOutputs ProcessDocumentAndSaveToS3(Stream input)
    {
        var outputs = new ParserOutputs();
        try
        {
            var parserRequest = BuildParserRequest(input);
            var parserResponse = parser.Parse(parserRequest);

            var xmlFilename = $"{consignmentReference}.xml";
            if (TrySaveToS3(xmlFilename, Encoding.UTF8.GetBytes(parserResponse.Xml), "application/xml"))
            {
                outputs.XMLFilename = xmlFilename;
            }

            var metadata = JsonSerializer.SerializeToUtf8Bytes(parserResponse.Meta, MetadataJsonOptions);
            if (TrySaveToS3(MetadataFilename, metadata, "application/json"))
            {
                outputs.MetadataFilename = MetadataFilename;
            }

var successfullySavedImages = (parserResponse.Images ?? Enumerable.Empty<Api.Image>())
                .Where(image => TrySaveToS3(image.Name, image.Content, image.Type))
                .ToArray();

            outputs.ImageFilenames = successfullySavedImages.Select(image => image.Name);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error processing document: {Exception}", e.Message);
        }

        return outputs;
    }

    private Api.Request BuildParserRequest(Stream input)
    {
        var inputs = GetInputsFromStream(input);

        var docx = externalResourceRetriever.GetDocx(inputs);

        var attachments = inputs.AttachmentURLs is not null
            ? externalResourceRetriever.GetAttachments(inputs.AttachmentURLs)
            : [];

        var hint = inputHelper.GetHint(inputs, logger);
        var meta = inputHelper.GetMetadata(inputs, logger);

        return new Api.Request
        {
            Content = docx,
            Attachments = attachments,
            Hint = hint,
            Meta = meta
        };
    }

    private ParserInputs GetInputsFromStream(Stream input)
    {
        var request = JsonSerializer.Deserialize<Request>(input);

        var inputs = request?.Inputs;

        if (inputs is null)
        {
            throw new InvalidTreRequestException("parser-inputs is null");
        }

        s3Bucket = inputs.S3Bucket;
        s3OutputPrefix = inputs.S3OutputPrefix;
        consignmentReference = inputs.ConsignmentReference
            ?? throw new InvalidTreRequestException("parser-inputs.consignment-reference is null");

        return inputs;
    }

    private bool TrySaveToS3(string filename, byte[] content, string type)
    {
        var request = new PutObjectRequest
        {
            BucketName = s3Bucket,
            Key = s3OutputPrefix + filename,
            ContentType = type,
            InputStream = new MemoryStream(content)
        };

        try
        {
            s3.PutObjectAsync(request).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "error saving {Filename} to S3: {Message}", filename, e.Message);
            return false;
        }
    }
}
