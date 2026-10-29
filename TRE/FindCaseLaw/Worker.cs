#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using TRE.FindCaseLaw.Inputs;
using TRE.FindCaseLaw.Outputs;
using TRE.Metadata;
using TRE.Metadata.Enums;

using UK.Gov.Legislation.Judgments;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace TRE.FindCaseLaw;

public class Worker(
    ILogger<Worker> logger,
    Api.IParser parser,
    ICustomLoggerProvider loggerProvider,
    IAmazonS3 s3,
    IInputHelper inputHelper,
    IExternalResourceRetriever externalResourceRetriever,
    TimeProvider timeProvider)
{
    private const string LogFilename = "parser.log";
    private const string MetadataFilename = "metadata.json";
    private const string DocxMimetype = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private string s3Bucket = string.Empty;
    private string s3OutputPrefix = string.Empty;
    private string consignmentReference = string.Empty;
    private List<IMetadataField> structuredMetadata = [];
    private Guid parserRunId;
    private string sourceFilename = string.Empty;
    private string docxSha256 = string.Empty;
    private Route route;

    public Stream Process(Stream input)
    {
        parserRunId = Guid.NewGuid();
        logger.LogInformation("parser run id: {ParserRunId}", parserRunId);

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

            var metadata = JsonSerializer.SerializeToUtf8Bytes(BuildParserProcessMetadata(parserResponse),
                ParserProcessMetadata.JsonSerializerOptions);

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

    private ParserProcessMetadata BuildParserProcessMetadata(Api.Response parserResponse)
    {
        return new ParserProcessMetadata
        {
            DocumentType = Enum.Parse<DocumentType>(parserResponse.Meta.DocumentType, true),
            ParserRunId = parserRunId,
            MetadataFields = structuredMetadata,
            PrimarySource = new PrimarySourceFile
            {
                Filename = sourceFilename,
                Mimetype = DocxMimetype,
                Route = route,
                Sha256 = docxSha256,
                RouteDateTime = timeProvider.GetUtcNow().UtcDateTime
            },
            XmlContainsDocumentText = true,
            Uri = parserResponse.Meta.Uri,
            Court = parserResponse.Meta.Court,
            Cite = parserResponse.Meta.Cite,
            Date = parserResponse.Meta.Date,
            Name = parserResponse.Meta.Name,
            Extensions = parserResponse.Meta.Extensions,
            Attachments = parserResponse.Meta.Attachments ?? []
        };
    }

    private Api.Request BuildParserRequest(Stream input)
    {
        var inputs = GetInputsFromStream(input);

        var docx = externalResourceRetriever.GetDocx(inputs);

        docxSha256 = Convert.ToHexStringLower(SHA256.HashData(docx));
        sourceFilename = inputHelper.GetSourceFilename(inputs);
        route = inputHelper.GetRoute(inputs);

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
        var request = JsonSerializer.Deserialize<Request>(input, ParserProcessMetadata.JsonSerializerOptions);

        var inputs = request?.Inputs;

        if (inputs is null)
        {
            throw new InvalidTreRequestException("parser-inputs is null");
        }

        s3Bucket = inputs.S3Bucket;
        s3OutputPrefix = inputs.S3OutputPrefix;
        structuredMetadata = inputs.Metadata?.MetadataFields ?? [];
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
