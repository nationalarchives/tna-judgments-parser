#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using Moq;

using NationalArchives.FindCaseLaw.Utils.Metadata;
using NationalArchives.FindCaseLaw.Utils.Metadata.Enums;

using Shouldly;

using test.Mocks;

using TRE.FindCaseLaw;
using TRE.FindCaseLaw.Inputs;
using TRE.FindCaseLaw.Outputs;
using TRE.Metadata;

using UK.Gov.Legislation.Judgments;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace test.TRE.FindCaseLaw;

public class TestWorker
{
    private record Upload(string ContentType, byte[] Content);

    private static readonly byte[] Docx = [1, 2, 3];

    private readonly Dictionary<string, Upload> uploads = new();
    private readonly HashSet<string> keysWhichFailToUpload = [];

    private readonly MockLogger<Worker> mockLogger = new();
    private readonly Mock<Api.IParser> mockParser = new();
    private readonly Mock<ICustomLoggerProvider> mockCustomLoggerProvider = new();
    private readonly Mock<IAmazonS3> mockS3 = new();
    private readonly Mock<IInputHelper> mockInputHelper = new();
    private readonly Mock<IExternalResourceRetriever> mockExternalResourceRetriever = new();
    private readonly Mock<IStructuredMetadataConsolidator> mockStructuredMetadataConsolidator = new();
    private readonly FakeTimeProvider fakeTimeProvider = new(new DateTimeOffset(2024, 6, 1, 10, 30, 0, TimeSpan.Zero));
    private readonly Worker worker;

    private readonly List<LogMessage> logMessages = [];

    private readonly ParserInputs inputs = new()
    {
        ConsignmentReference = "TDR-2024-ABC",
        S3Bucket = "output-bucket",
        S3OutputPrefix = "consignment/",
        S3InputBucket = "input-bucket",
        S3InputKey = "input.docx",
        DocumentType = ParserInputs.JudgmentDocumentType
    };

    private Api.Response parserResponse = new()
    {
        Xml = "<akomaNtoso/>",
        Meta = new Api.Meta { DocumentType = "judgment" },
        Images = []
    };

    public TestWorker()
    {
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((PutObjectRequest request, CancellationToken _) =>
              {
                  if (string.IsNullOrEmpty(request.BucketName))
                  {
                      throw new ArgumentException("BucketName is a required property");
                  }

                  if (keysWhichFailToUpload.Contains(request.Key))
                  {
                      throw new AmazonS3Exception("access denied");
                  }

                  using var content = new MemoryStream();
                  request.InputStream.CopyTo(content);
                  uploads[request.Key] = new Upload(request.ContentType, content.ToArray());
                  return new PutObjectResponse();
              });

        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>())).Returns(() => parserResponse);

        mockLogger.Setup(x => x.Log(
                      It.IsAny<LogLevel>(),
                      It.IsAny<EventId>(),
                      It.IsAny<It.IsAnyType>(),
                      It.IsAny<Exception>(),
                      It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                  .Callback(
                      new InvocationAction(invocation => logMessages.Add(new LogMessage
                      {
                          Level = (LogLevel)invocation.Arguments[0]!,
                          Message = (string)((Delegate)invocation.Arguments[4]!).DynamicInvoke(
                              invocation.Arguments[2], invocation.Arguments[3])!
                      }))
                  );

        mockCustomLoggerProvider.Setup(x => x.Reset())
                                .Returns(() =>
                                {
                                    var messages = new List<LogMessage>(logMessages);
                                    logMessages.Clear();
                                    return messages;
                                });

        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>())).Returns(Docx);

        mockInputHelper.Setup(x => x.GetRoute(It.IsAny<ParserInputs>()))
                       .Returns((ParserInputs pi) => pi.Metadata is null ? Route.Tdr : Route.Eui);
        mockInputHelper.Setup(x => x.GetSourceFilename(It.IsAny<ParserInputs>()))
                       .Returns((ParserInputs pi) => pi.S3InputKey);

        mockStructuredMetadataConsolidator
            .Setup(x => x.BuildMetadataFields(It.IsAny<IMetadataField[]>(), It.IsAny<IMetadataField[]>()))
            .Returns((IMetadataField[] input, IMetadataField[] document) => [.. input, .. document]);

        worker = new Worker(mockLogger.Object, mockParser.Object, mockCustomLoggerProvider.Object, mockS3.Object,
            mockInputHelper.Object, mockExternalResourceRetriever.Object, fakeTimeProvider,
            mockStructuredMetadataConsolidator.Object);
    }

    private ParserOutputs Run(ParserInputs? parserInputs)
    {
        var request = JsonSerializer.SerializeToUtf8Bytes(new Request { Inputs = parserInputs },
            ParserProcessMetadata.JsonSerializerOptions);
        using var result = worker.Process(new MemoryStream(request));
        return JsonSerializer.Deserialize<Response>(result)!.Outputs;
    }

    private JsonElement SavedMetadata()
    {
        return JsonDocument.Parse(uploads["consignment/metadata.json"].Content).RootElement;
    }

    private string GetUploadedJsonString()
    {
        var uploadedJson = Encoding.UTF8.GetString(uploads["consignment/metadata.json"].Content);
        uploadedJson = Regex.Replace(uploadedJson, "\"parser_run_id\":\"[-0-9a-z]+\"",
            "\"parser_run_id\":\"00000000-0000-0000-0000-000000000000\"");
        return uploadedJson;
    }

    private static MetadataField<string> MakeField(MetadataFieldName name, string value)
    {
        return new MetadataField<string>
        {
            Id = Guid.NewGuid(),
            Name = name,
            Source = MetadataSource.Editor,
            Timestamp = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Value = value
        };
    }

    [Fact]
    public void Process_ValidInputs_SavesXmlMetadataAndLogUnderOutputPrefix()
    {
        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/TDR-2024-ABC.xml", "consignment/metadata.json", "consignment/parser.log"],
            ignoreOrder: true);
    }

    [Fact]
    public void Process_ValidInputs_ReportsSavedFilenames()
    {
        var outputs = Run(inputs);

        outputs.XMLFilename.ShouldBe("TDR-2024-ABC.xml");
        outputs.MetadataFilename.ShouldBe("metadata.json");
        outputs.LogFilename.ShouldBe("parser.log");
    }

    [Fact]
    public void Process_ValidInputs_ReportsNoErrors()
    {
        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBeEmpty();
    }

    [Fact]
    public void Process_ValidInputs_SavesParsedXmlAsXml()
    {
        Run(inputs);

        uploads["consignment/TDR-2024-ABC.xml"].ContentType.ShouldBe("application/xml");
        Encoding.UTF8.GetString(uploads["consignment/TDR-2024-ABC.xml"].Content).ShouldBe("<akomaNtoso/>");
    }

    [Fact]
    public void Process_ValidInputs_SavesMetadataAsJson()
    {
        Run(inputs);

        uploads["consignment/metadata.json"].ContentType.ShouldBe("application/json");
    }

    [Fact]
    public void Process_ParserReturnsMeta_SavesItInMetadata()
    {
        parserResponse = parserResponse with
        {
            Meta = new Api.Meta
            {
                DocumentType = "judgment",
                Court = "UKUT-IAC",
                Cite = "[2024] UKUT 1 (IAC)"
            }
        };

        Run(inputs);

        var metadata = SavedMetadata();
        metadata.GetProperty("court").GetString().ShouldBe("UKUT-IAC");
        metadata.GetProperty("cite").GetString().ShouldBe("[2024] UKUT 1 (IAC)");
    }

    [Fact]
    public void Process_InputAndParserMetadataFields_PassesThemToConsolidator()
    {
        var inputField = MakeField(MetadataFieldName.Court, "EWCA");
        inputs.Metadata = new InputMetadata { StructuredMetadataFields = [inputField] };
        var parserField = MakeField(MetadataFieldName.Name, "Alpha v Bravo");
        parserResponse = parserResponse with { StructuredMetadataFields = [parserField] };

        Run(inputs);

        mockStructuredMetadataConsolidator.Verify(x => x.BuildMetadataFields(
            It.Is<IMetadataField[]>(fields => fields.SequenceEqual(new IMetadataField[] { inputField })),
            It.Is<IMetadataField[]>(fields => fields.SequenceEqual(new IMetadataField[] { parserField }))));
    }

    [Fact]
    public void Process_NoInputMetadata_PassesEmptyInputFieldsToConsolidator()
    {
        inputs.Metadata = null;

        Run(inputs);

        mockStructuredMetadataConsolidator.Verify(x => x.BuildMetadataFields(
            It.Is<IMetadataField[]>(fields => fields.Length == 0), It.IsAny<IMetadataField[]>()));
    }

    [Fact]
    public void Process_ConsolidatorReturnsFields_SavesThemAsMetadataFields()
    {
        var court = MakeField(MetadataFieldName.Court, "EWCA");
        var name = MakeField(MetadataFieldName.Name, "Alpha v Bravo");
        mockStructuredMetadataConsolidator
            .Setup(x => x.BuildMetadataFields(It.IsAny<IMetadataField[]>(), It.IsAny<IMetadataField[]>()))
            .Returns([court, name]);

        Run(inputs);

        SavedMetadata().GetProperty("metadata_fields").EnumerateArray()
                       .Select(field => field.GetProperty("id").GetString())
                       .ShouldBe([court.Id.ToString(), name.Id.ToString()]);
    }

    [Fact]
    public void Process_ValidInputs_SavesNewParserRunId()
    {
        Run(inputs);

        Guid.TryParse(SavedMetadata().GetProperty("parser_run_id").GetString(), out var parserRunId).ShouldBeTrue();
        parserRunId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Process_ValidInputs_MarksXmlAsContainingDocumentText()
    {
        Run(inputs);

        SavedMetadata().GetProperty("xml_contains_document_text").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Process_ValidInputs_SavesLegacyMetadataInJson()
    {
        parserResponse = parserResponse with
        {
            Meta = new Api.Meta
            {
                DocumentType = "judgment",
                Uri = "ewca/civ/2024/1",
                Court = "EWCA-Civil",
                Cite = "[2024] EWCA Civ 1",
                Date = "2024-05-01",
                Name = "Alpha v Bravo"
            }
        };

        Run(inputs);

        var uploadedJson = GetUploadedJsonString();

        uploadedJson.ShouldBe(
            """{"documentType":"judgment","parser_run_id":"00000000-0000-0000-0000-000000000000","error-messages":[],"metadata_fields":[],"primary_source":{"filename":"input.docx","mimetype":"application/vnd.openxmlformats-officedocument.wordprocessingml.document","route":"tdr","sha256":"039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81","route_datetime":"2024-06-01T10:30:00Z"},"xml_contains_document_text":true,"uri":"ewca/civ/2024/1","court":"EWCA-Civil","cite":"[2024] EWCA Civ 1","date":"2024-05-01","name":"Alpha v Bravo","extensions":null,"attachments":[]}""");
    }

    [Fact]
    public void Process_ValidInputs_SavesPrimarySource()
    {
        mockInputHelper.Setup(x => x.GetRoute(It.IsAny<ParserInputs>())).Returns(Route.Eui);

        inputs.S3InputKey = "My Judgment.docx";

        Run(inputs);

        SavedMetadata().GetProperty("primary_source").GetRawText().ShouldBe(
            """{"filename":"My Judgment.docx","mimetype":"application/vnd.openxmlformats-officedocument.wordprocessingml.document","route":"eui","sha256":"039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81","route_datetime":"2024-06-01T10:30:00Z"}""");
    }

    [Fact]
    public void Process_ValidInputs_LogsTheParserRunIdSavedInMetadata()
    {
        Run(inputs);

        var parserRunId = SavedMetadata().GetProperty("parser_run_id").GetString();
        mockLogger.VerifyLog($"parser run id: {parserRunId}", LogLevel.Information);
    }

    [Fact]
    public void Process_ProcessedTwice_UsesADifferentParserRunIdEachTime()
    {
        Run(inputs);
        var first = SavedMetadata().GetProperty("parser_run_id").GetString();

        Run(inputs);
        var second = SavedMetadata().GetProperty("parser_run_id").GetString();

        second.ShouldNotBe(first);
    }

    [Fact]
    public void Process_RequestJsonWithInvalidMetadataFieldValue_LogsError()
    {
        const string courtField =
            """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"court","source":"external","timestamp":"2024-07-01T00:00:00Z","value":{"randomfield1":"Hello","randomfield2":"there"}}""";
        const string requestJson = $$"""
                                     {
                                       "parser-inputs": {
                                         "consignment-reference": "TDR-2024-ABC",
                                         "s3-bucket": "output-bucket",
                                         "s3-output-prefix": "consignment/",
                                         "s3-input-bucket": "input-bucket",
                                         "s3-input-key": "input.docx",
                                         "document-type": "judgment",
                                         "metadata": {
                                           "structured_metadata_fields": [
                                             {{courtField}},
                                           ]
                                         }
                                       }
                                     }
                                     """;
        using var result = worker.Process(new MemoryStream(Encoding.UTF8.GetBytes(requestJson)));

        mockLogger.VerifyLog(
            """error processing document: Could not deserialize structured metadata field '{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"court","source":"external","timestamp":"2024-07-01T00:00:00Z","value":{"randomfield1":"Hello","randomfield2":"there"}}'""",
            LogLevel.Error);
    }

    [Fact]
    public void Process_RequestJsonWithUnknownMetadataFieldName_LogsError()
    {
        const string requestJson = """
                                   {
                                     "parser-inputs": {
                                       "consignment-reference": "TDR-2024-ABC",
                                       "s3-bucket": "output-bucket",
                                       "s3-output-prefix": "consignment/",
                                       "s3-input-bucket": "input-bucket",
                                       "s3-input-key": "input.docx",
                                       "document-type": "judgment",
                                       "metadata": {
                                         "structured_metadata_fields": [
                                           {
                                             "id":"02d0209d-7482-43f3-8329-8ff7618a2112",
                                             "name":"random",
                                             "source":"external",
                                             "timestamp":"2024-07-01T00:00:00Z",
                                             "value":"Hello"
                                           }
                                         ]
                                       }
                                     }
                                   }
                                   """;

        using var result = worker.Process(new MemoryStream(Encoding.UTF8.GetBytes(requestJson)));

        mockLogger.VerifyLog("error processing document: Unrecognised structured metadata field name 'random'",
            LogLevel.Error);
    }

    [Fact]
    public void Process_RequestJsonWithStructuredMetadata_SavesExpectedMetadataJson()
    {
        const string courtField =
            """{"id":"6f1c1c4e-2f0b-4c56-9d1e-3b8f7a2c9e10","name":"court","source":"external","timestamp":"2024-05-01T00:00:00Z","value":"EWCA"}""";
        const string partyField =
            """{"id":"a41d7b52-8c3e-4f0a-b6d2-5e9c1f3a7d84","name":"party","source":"external","timestamp":"2024-06-01T00:00:00Z","value":{"name":"Alpha","role":"appellant"}}""";
        const string requestJson = $$"""
                                     {
                                       "parser-inputs": {
                                         "consignment-reference": "TDR-2024-ABC",
                                         "s3-bucket": "output-bucket",
                                         "s3-output-prefix": "consignment/",
                                         "s3-input-bucket": "input-bucket",
                                         "s3-input-key": "input.docx",
                                         "document-type": "judgment",
                                         "metadata": {
                                           "structured_metadata_fields": [
                                             {{courtField}},
                                             {{partyField}}
                                           ]
                                         }
                                       }
                                     }
                                     """;

        using var result = worker.Process(new MemoryStream(Encoding.UTF8.GetBytes(requestJson)));

        var uploadedJson = GetUploadedJsonString();

        uploadedJson.ShouldBe(
            $$"""{"documentType":"judgment","parser_run_id":"00000000-0000-0000-0000-000000000000","error-messages":[],"metadata_fields":[{{courtField}},{{partyField}}],"primary_source":{"filename":"input.docx","mimetype":"application/vnd.openxmlformats-officedocument.wordprocessingml.document","route":"eui","sha256":"039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81","route_datetime":"2024-06-01T10:30:00Z"},"xml_contains_document_text":true,"uri":null,"court":null,"cite":null,"date":null,"name":null,"extensions":null,"attachments":[]}""");
    }

    [Fact]
    public void Process_ParserReturnsImages_SavesEachImageWithItsOwnContentType()
    {
        var png = new Api.Image
        {
            Name = "image1.png",
            Type = "image/png",
            Content = [4, 5]
        };
        var jpeg = new Api.Image
        {
            Name = "image2.jpeg",
            Type = "image/jpeg",
            Content = [6]
        };
        parserResponse = parserResponse with { Images = [png, jpeg] };

        Run(inputs);

        uploads["consignment/image1.png"].ContentType.ShouldBe("image/png");
        uploads["consignment/image1.png"].Content.ShouldBe(png.Content);
        uploads["consignment/image2.jpeg"].ContentType.ShouldBe("image/jpeg");
        uploads["consignment/image2.jpeg"].Content.ShouldBe(jpeg.Content);
    }

    [Fact]
    public void Process_ParserReturnsImages_ReportsImageFilenames()
    {
        parserResponse = parserResponse with
        {
            Images =
            [
                new Api.Image
                {
                    Name = "image1.png",
                    Type = "image/png",
                    Content = [4]
                },
                new Api.Image
                {
                    Name = "image2.jpeg",
                    Type = "image/jpeg",
                    Content = [5]
                }
            ]
        };

        var outputs = Run(inputs);

        outputs.ImageFilenames.ShouldBe(["image1.png", "image2.jpeg"]);
    }

    [Fact]
    public void Process_ValidInputs_GivesParserDocxHintMetadataAndAttachments()
    {
        var hint = Api.Hint.Judgment;
        var meta = new Api.Meta { Court = "EWCA" };
        List<Api.Attachment> attachments =
        [
            new()
            {
                Content = [7],
                Type = Api.AttachmentType.Order
            }
        ];
        mockInputHelper.Setup(x => x.GetHint(
            It.Is<ParserInputs>(i => i.ConsignmentReference == inputs.ConsignmentReference),
            It.IsAny<ILogger>())).Returns(hint);
        mockInputHelper.Setup(x => x.GetMetadata(
            It.Is<ParserInputs>(i => i.ConsignmentReference == inputs.ConsignmentReference),
            It.IsAny<ILogger>())).Returns(meta);

        inputs.AttachmentURLs = ["https://example.com/attachment"];

        mockExternalResourceRetriever.Setup(x => x.GetAttachments(inputs.AttachmentURLs)).Returns(attachments);

        Run(inputs);

        mockParser.Verify(
            x => x.Parse(It.Is<Api.Request>(r => r.Content == Docx && r.Hint == hint && r.Meta == meta
                && r.Attachments == attachments)), Times.Once);
    }

    [Fact]
    public void Process_NoAttachmentUrls_DoesNotRetrieveAttachments()
    {
        inputs.AttachmentURLs = null;

        Run(inputs);

        mockExternalResourceRetriever.Verify(x => x.GetAttachments(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public void Process_NoAttachmentsRetrieved_StillParses()
    {
        inputs.AttachmentURLs = ["https://example.com/missing"];
        mockExternalResourceRetriever.Setup(x => x.GetAttachments(inputs.AttachmentURLs)).Returns([]);

        Run(inputs);

        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Once);
    }

    [Fact]
    public void Process_MissingParserInputs_LogsProcessingError()
    {
        Run(null);

        mockLogger.VerifyLog("error processing document: parser-inputs is null", LogLevel.Error);
    }

    [Fact]
    public void Process_MissingParserInputs_LogsThatTheLogCannotBeSaved()
    {
        Run(null);

        mockLogger.VerifyLog("error saving parser.log to S3: BucketName is a required property", LogLevel.Error);
    }

    [Fact]
    public void Process_MissingParserInputs_SavesNothing()
    {
        Run(null);

        uploads.ShouldBeEmpty();
    }

    [Fact]
    public void Process_UnreadableDocx_LogsError()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>()))
                                     .Throws(new IOException("no such object"));

        Run(inputs);

        mockLogger.VerifyLog("error processing document: no such object", LogLevel.Error);
    }

    [Fact]
    public void Process_UnreadableDocx_SavesOnlyTheLog()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>()))
                                     .Throws(new IOException("no such object"));

        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/parser.log"]);
    }

    [Fact]
    public void Process_UnreadableDocx_DoesNotParse()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>()))
                                     .Throws(new IOException("no such object"));

        Run(inputs);

        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void Process_UnreadableDocumentType_LogsError()
    {
        mockInputHelper.Setup(x => x.GetHint(It.IsAny<ParserInputs>(), It.IsAny<ILogger>()))
                       .Throws(new Exception("unknown document type"));

        Run(inputs);

        mockLogger.VerifyLog("error processing document: unknown document type", LogLevel.Error);
    }

    [Fact]
    public void Process_UnreadableDocumentType_SavesOnlyTheLog()
    {
        mockInputHelper.Setup(x => x.GetHint(It.IsAny<ParserInputs>(), It.IsAny<ILogger>()))
                       .Throws(new Exception("unknown document type"));

        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/parser.log"]);
    }

    [Fact]
    public void Process_UnreadableDocumentType_DoesNotParse()
    {
        mockInputHelper.Setup(x => x.GetHint(It.IsAny<ParserInputs>(), It.IsAny<ILogger>()))
                       .Throws(new Exception("unknown document type"));

        Run(inputs);

        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void Process_UnreadableInputMetadata_LogsError()
    {
        mockInputHelper.Setup(x => x.GetMetadata(It.IsAny<ParserInputs>(), It.IsAny<ILogger>()))
                       .Throws(new Exception("input URI is not supported"));

        Run(inputs);

        mockLogger.VerifyLog("error processing document: input URI is not supported", LogLevel.Error);
    }

    [Fact]
    public void Process_UnreadableInputMetadata_SavesOnlyTheLog()
    {
        mockInputHelper.Setup(x => x.GetMetadata(It.IsAny<ParserInputs>(), It.IsAny<ILogger>()))
                       .Throws(new Exception("input URI is not supported"));

        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/parser.log"]);
    }

    [Fact]
    public void Process_UnreadableInputMetadata_DoesNotParse()
    {
        mockInputHelper.Setup(x => x.GetMetadata(It.IsAny<ParserInputs>(), It.IsAny<ILogger>()))
                       .Throws(new Exception("input URI is not supported"));

        Run(inputs);

        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void Process_ParseFails_LogsError()
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>())).Throws(new Exception("not a docx"));

        Run(inputs);

        mockLogger.VerifyLog("error processing document: not a docx", LogLevel.Error);
    }

    [Fact]
    public void Process_ParseFails_SavesOnlyTheLog()
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>())).Throws(new Exception("not a docx"));

        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/parser.log"]);
    }

    [Fact]
    public void Process_XmlSaveFails_LogsError()
    {
        keysWhichFailToUpload.Add("consignment/TDR-2024-ABC.xml");

        Run(inputs);

        mockLogger.VerifyLog("error saving TDR-2024-ABC.xml to S3: access denied", LogLevel.Error);
    }

    [Fact]
    public void Process_XmlSaveFails_OmitsXmlFilename()
    {
        keysWhichFailToUpload.Add("consignment/TDR-2024-ABC.xml");

        var outputs = Run(inputs);

        outputs.XMLFilename.ShouldBeNull();
    }

    [Fact]
    public void Process_XmlSaveFails_StillSavesMetadataAndLog()
    {
        keysWhichFailToUpload.Add("consignment/TDR-2024-ABC.xml");

        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/metadata.json", "consignment/parser.log"], ignoreOrder: true);
    }

    [Fact]
    public void Process_MetadataSaveFails_LogsError()
    {
        keysWhichFailToUpload.Add("consignment/metadata.json");

        Run(inputs);

        mockLogger.VerifyLog("error saving metadata.json to S3: access denied", LogLevel.Error);
    }

    [Fact]
    public void Process_MetadataSaveFails_OmitsMetadataFilename()
    {
        keysWhichFailToUpload.Add("consignment/metadata.json");

        var outputs = Run(inputs);

        outputs.MetadataFilename.ShouldBeNull();
    }

    [Fact]
    public void Process_MetadataSaveFails_StillSavesXmlAndLog()
    {
        keysWhichFailToUpload.Add("consignment/metadata.json");

        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/TDR-2024-ABC.xml", "consignment/parser.log"], ignoreOrder: true);
    }

    [Fact]
    public void Process_ImageSaveFails_LogsError()
    {
        parserResponse = parserResponse with
        {
            Images = new[]
            {
                new Api.Image
                {
                    Name = "image1.png",
                    Type = "image/png",
                    Content = [4]
                }
            }
        };
        keysWhichFailToUpload.Add("consignment/image1.png");

        Run(inputs);

        mockLogger.VerifyLog("error saving image1.png to S3: access denied", LogLevel.Error);
    }

    [Fact]
    public void Process_ImageSaveFails_ReportsOnlyTheSavedImages()
    {
        parserResponse = parserResponse with
        {
            Images =
            [
                new Api.Image
                {
                    Name = "image1.png",
                    Type = "image/png",
                    Content = [4]
                },
                new Api.Image
                {
                    Name = "image2.png",
                    Type = "image/png",
                    Content = [5]
                }
            ]
        };
        keysWhichFailToUpload.Add("consignment/image1.png");

        var outputs = Run(inputs);

        outputs.ImageFilenames.ShouldBe(["image2.png"]);
    }

    [Fact]
    public void Process_LogSaveFails_LogsError()
    {
        keysWhichFailToUpload.Add("consignment/parser.log");

        Run(inputs);

        mockLogger.VerifyLog("error saving parser.log to S3: access denied", LogLevel.Error);
    }

    [Fact]
    public void Process_LogSaveFails_OmitsLogFilename()
    {
        keysWhichFailToUpload.Add("consignment/parser.log");

        var outputs = Run(inputs);

        outputs.LogFilename.ShouldBeNull();
    }

    [Fact]
    public void Process_S3ClientThrowsNonAmazonException_LogsError()
    {
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new ArgumentException("BucketName is a required property"));

        Run(inputs);

        mockLogger.VerifyLog("error saving TDR-2024-ABC.xml to S3: BucketName is a required property",
            LogLevel.Error);
    }

    [Fact]
    public void Process_S3ClientThrowsNonAmazonException_OmitsFilenames()
    {
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new ArgumentException("BucketName is a required property"));

        var outputs = Run(inputs);

        outputs.XMLFilename.ShouldBeNull();
        outputs.MetadataFilename.ShouldBeNull();
        outputs.LogFilename.ShouldBeNull();
    }

    [Fact]
    public void Process_ParserReturnsNoImages_ReportsNoErrors()
    {
        parserResponse = parserResponse with { Images = null };

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBeEmpty();
    }
}
