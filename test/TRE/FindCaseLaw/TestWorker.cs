#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using Moq;

using Shouldly;

using test.Mocks;

using TRE.FindCaseLaw;
using TRE.FindCaseLaw.Inputs;
using TRE.FindCaseLaw.Outputs;
using TRE.Metadata;
using TRE.Metadata.Enums;

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

        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>()))
                  .Returns(new Api.Response
                  {
                      Xml = "<akomaNtoso/>",
                      Meta = new Api.Meta { DocumentType = "judgment" },
                      Images = []
                  });

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

        mockInputHelper.Setup(x => x.GetRoute(It.IsAny<ParserInputs>())).Returns(Route.Tdr);
        mockInputHelper.Setup(x => x.GetSourceFilename(It.IsAny<ParserInputs>())).Returns("input.docx");

        worker = new Worker(mockLogger.Object, mockParser.Object, mockCustomLoggerProvider.Object, mockS3.Object,
            mockInputHelper.Object, mockExternalResourceRetriever.Object, fakeTimeProvider);
    }

    private ParserOutputs Run(ParserInputs? parserInputs)
    {
        var request = JsonSerializer.SerializeToUtf8Bytes(new Request { Inputs = parserInputs },
            ParserProcessMetadata.JsonSerializerOptions);
        using var result = worker.Process(new MemoryStream(request));
        return JsonSerializer.Deserialize<Response>(result)!.Outputs;
    }

    private void SetupParserToReturnImages(params Api.Image[] images)
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>()))
                  .Returns(new Api.Response
                  {
                      Xml = "<akomaNtoso/>",
                      Meta = new Api.Meta { DocumentType = "judgment" },
                      Images = images
                  });
    }

    [Fact]
    public void Process_ValidInputs_SavesXmlMetadataAndLogUnderOutputPrefix()
    {
        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/TDR-2024-ABC.xml", "consignment/metadata.json", "consignment/parser.log"],
            ignoreOrder: true);
    }

    [Fact]
    public void Process_ValidInputs_ReportsSavedFilenamesAndNoErrors()
    {
        var outputs = Run(inputs);

        outputs.XMLFilename.ShouldBe("TDR-2024-ABC.xml");
        outputs.MetadataFilename.ShouldBe("metadata.json");
        outputs.LogFilename.ShouldBe("parser.log");
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
    public void Process_ValidInputs_SavesParsedMetadataAsJson()
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>()))
                  .Returns(new Api.Response
                  {
                      Xml = "<akomaNtoso/>",
                      Meta = new Api.Meta
                      {
                          DocumentType = "judgment",
                          Court = "UKUT-IAC",
                          Cite = "[2024] UKUT 1 (IAC)"
                      },
                      Images = []
                  });

        Run(inputs);

        uploads["consignment/metadata.json"].ContentType.ShouldBe("application/json");
        var metadata = JsonDocument.Parse(uploads["consignment/metadata.json"].Content).RootElement;
        metadata.GetProperty("court").GetString().ShouldBe("UKUT-IAC");
        metadata.GetProperty("cite").GetString().ShouldBe("[2024] UKUT 1 (IAC)");
    }

    private JsonElement SavedMetadata() => JsonDocument.Parse(uploads["consignment/metadata.json"].Content).RootElement;

    [Fact]
    public void Process_MetadataFieldsInInputs_SavesThemAsMetadataFields()
    {
        inputs.Metadata = new InputMetadata
        {
            MetadataFields =
            [
                new MetadataField<string>
                {
                    Name = MetadataFieldName.Court,
                    Source = MetadataSource.External,
                    Timestamp = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                    Value = "EWCA"
                }
            ]
        };

        Run(inputs);

        var metadata = SavedMetadata();
        var fields = metadata.GetProperty("metadata_fields").EnumerateArray().ToArray();
        fields.Length.ShouldBe(1);
        fields[0].GetProperty("name").GetString().ShouldBe("court");
        fields[0].GetProperty("source").GetString().ShouldBe("external");
        fields[0].GetProperty("value").GetString().ShouldBe("EWCA");
    }

    [Fact]
    public void Process_NoMetadataFieldsInInputs_SavesEmptyMetadataFields()
    {
        Run(inputs);

        SavedMetadata().GetProperty("metadata_fields").GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [InlineData(Route.Eui, "eui")]
    [InlineData(Route.Tdr, "tdr")]
    public void Process_RouteFromInputHelper_IsSavedAsPrimarySourceRoute(Route route, string expectedRoute)
    {
        mockInputHelper.Setup(x => x.GetRoute(It.IsAny<ParserInputs>())).Returns(route);

        Run(inputs);

        SavedMetadata().GetProperty("primary_source").GetProperty("route").GetString().ShouldBe(expectedRoute);
    }

    [Fact]
    public void Process_ValidInputs_SavesNewParserRunIdAndMarksXmlAsContainingDocumentText()
    {
        Run(inputs);

        var metadata = SavedMetadata();
        Guid.TryParse(metadata.GetProperty("parser_run_id").GetString(), out var parserRunId).ShouldBeTrue();
        parserRunId.ShouldNotBe(Guid.Empty);
        metadata.GetProperty("xml_contains_document_text").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Process_ValidInputs_SavesPrimarySourceFileDetailsFromTheFetchedDocx()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>())).Returns(Docx);

        Run(inputs);

        var primarySource = SavedMetadata().GetProperty("primary_source");
        primarySource.GetProperty("filename").GetString().ShouldBe("input.docx");
        primarySource.GetProperty("mimetype").GetString()
                     .ShouldBe("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        primarySource.GetProperty("sha256").GetString()
                     .ShouldBe("039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81");
        primarySource.GetProperty("route_datetime").GetString().ShouldBe("2024-06-01T10:30:00Z");
    }

    [Fact]
    public void Process_SourceFilenameFromInputHelper_IsSavedInPrimarySource()
    {
        mockInputHelper.Setup(x => x.GetSourceFilename(It.IsAny<ParserInputs>())).Returns("My Judgment.docx");

        Run(inputs);

        SavedMetadata().GetProperty("primary_source").GetProperty("filename").GetString().ShouldBe("My Judgment.docx");
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
        SetupParserToReturnImages(png, jpeg);

        var outputs = Run(inputs);

        uploads["consignment/image1.png"].ContentType.ShouldBe("image/png");
        uploads["consignment/image1.png"].Content.ShouldBe(png.Content);
        uploads["consignment/image2.jpeg"].ContentType.ShouldBe("image/jpeg");
        uploads["consignment/image2.jpeg"].Content.ShouldBe(jpeg.Content);
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
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>())).Returns(Docx);
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
    public void Process_MissingParserInputs_LogsErrorWithoutThrowing()
    {
        Run(null);

        mockLogger.VerifyLog("error processing document: parser-inputs is null", LogLevel.Error);
        mockLogger.VerifyLog("error saving parser.log to S3: BucketName is a required property", LogLevel.Error);
        uploads.ShouldBeEmpty();
    }

    [Fact]
    public void Process_UnreadableDocx_LogsErrorAndSavesLogAndDoesNotParse()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>()))
                                     .Throws(new IOException("no such object"));

        Run(inputs);

        mockLogger.VerifyLog("error processing document: no such object", LogLevel.Error);
        uploads.Keys.ShouldBe(["consignment/parser.log"]);
        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void Process_UnreadableDocumentType_LogsErrorAndSavesLogAndDoesNotParse()
    {
        mockInputHelper.Setup(x => x.GetHint(It.IsAny<ParserInputs>(), It.IsAny<ILogger>()))
                       .Throws(new Exception("unknown document type"));

        Run(inputs);

        mockLogger.VerifyLog("error processing document: unknown document type", LogLevel.Error);
        uploads.Keys.ShouldBe(["consignment/parser.log"]);
        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void Process_UnreadableInputMetadata_LogsErrorAndSavesLogAndDoesNotParse()
    {
        mockInputHelper.Setup(x => x.GetMetadata(It.IsAny<ParserInputs>(), It.IsAny<ILogger>()))
                       .Throws(new Exception("input URI is not supported"));

        Run(inputs);

        mockLogger.VerifyLog("error processing document: input URI is not supported", LogLevel.Error);
        uploads.Keys.ShouldBe(["consignment/parser.log"]);
        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void Process_ParseFails_LogsErrorAndSavesLog()
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>())).Throws(new Exception("not a docx"));

        Run(inputs);

        mockLogger.VerifyLog("error processing document: not a docx", LogLevel.Error);
        uploads.Keys.ShouldBe(["consignment/parser.log"]);
    }

    [Fact]
    public void Process_XmlSaveFails_LogsErrorAndStillSavesMetadataAndLog()
    {
        keysWhichFailToUpload.Add("consignment/TDR-2024-ABC.xml");

        var outputs = Run(inputs);

        mockLogger.VerifyLog("error saving TDR-2024-ABC.xml to S3: access denied", LogLevel.Error);
        outputs.XMLFilename.ShouldBeNull();
        uploads.Keys.ShouldBe(["consignment/metadata.json", "consignment/parser.log"], ignoreOrder: true);
    }

    [Fact]
    public void Process_MetadataSaveFails_LogsErrorAndStillSavesXmlAndLog()
    {
        keysWhichFailToUpload.Add("consignment/metadata.json");

        var outputs = Run(inputs);

        mockLogger.VerifyLog("error saving metadata.json to S3: access denied", LogLevel.Error);
        outputs.MetadataFilename.ShouldBeNull();
        uploads.Keys.ShouldBe(["consignment/TDR-2024-ABC.xml", "consignment/parser.log"], ignoreOrder: true);
    }

    [Fact]
    public void Process_ImageSaveFails_LogsErrorAndStillSavesOtherImages()
    {
        var first = new Api.Image
        {
            Name = "image1.png",
            Type = "image/png",
            Content = [4]
        };
        var second = new Api.Image
        {
            Name = "image2.png",
            Type = "image/png",
            Content = [5]
        };
        SetupParserToReturnImages(first, second);
        keysWhichFailToUpload.Add("consignment/image1.png");

        var outputs = Run(inputs);

        mockLogger.VerifyLog("error saving image1.png to S3: access denied", LogLevel.Error);
        outputs.ImageFilenames.ShouldBe(["image2.png"]);
    }

    [Fact]
    public void Process_LogSaveFails_LogsErrorAndOmitsLogFilename()
    {
        keysWhichFailToUpload.Add("consignment/parser.log");

        var outputs = Run(inputs);

        mockLogger.VerifyLog("error saving parser.log to S3: access denied", LogLevel.Error);
        outputs.LogFilename.ShouldBeNull();
    }

    [Fact]
    public void Process_S3ClientThrowsNonAmazonException_LogsErrorsAndOmitsFilenames()
    {
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new ArgumentException("BucketName is a required property"));

        var outputs = Run(inputs);

        mockLogger.VerifyLog("error saving TDR-2024-ABC.xml to S3: BucketName is a required property",
            LogLevel.Error);
        outputs.XMLFilename.ShouldBeNull();
        outputs.MetadataFilename.ShouldBeNull();
        outputs.LogFilename.ShouldBeNull();
    }

    [Fact]
    public void Process_ParserReturnsNoImages_ReportsNoErrors()
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>()))
                  .Returns(new Api.Response
                  {
                      Xml = "<akomaNtoso/>",
                      Meta = new Api.Meta { DocumentType = "judgment" },
                      Images = null
                  });

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBeEmpty();
    }
}
