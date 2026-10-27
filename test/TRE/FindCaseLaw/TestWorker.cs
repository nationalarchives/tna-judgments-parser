using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

using Amazon.S3;
using Amazon.S3.Model;

using Moq;

using Shouldly;

using test.Mocks;

using TRE.FindCaseLaw;
using TRE.FindCaseLaw.Inputs;
using TRE.FindCaseLaw.Outputs;

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

    private readonly Mock<Api.IParser> mockParser = new();
    private readonly Mock<ICustomLoggerProvider> mockLoggerProvider = new();
    private readonly Mock<IAmazonS3> mockS3 = new();
    private readonly Mock<IInputHelper> mockInputHelper = new();
    private readonly Mock<IExternalResourceRetriever> mockExternalResourceRetriever = new();
    private readonly Worker worker;

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
              if (keysWhichFailToUpload.Contains(request.Key))
              {
                  throw new AmazonS3Exception("access denied");
              }

              using var content = new MemoryStream();
              request.InputStream.CopyTo(content);
              uploads[request.Key] = new Upload(request.ContentType, content.ToArray());
              return new PutObjectResponse();
          });

        mockLoggerProvider.Setup(x => x.Reset()).Returns(new List<LogMessage>());

        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>()))
              .Returns(new Api.Response { Xml = "<akomaNtoso/>", Meta = new Api.Meta(), Images = [] });

        worker = new Worker(new MockLogger<Worker>().Object, mockParser.Object, mockLoggerProvider.Object, mockS3.Object,
            mockInputHelper.Object, mockExternalResourceRetriever.Object);
    }

    private ParserOutputs Run(ParserInputs parserInputs)
    {
        var request = JsonSerializer.SerializeToUtf8Bytes(new Request { Inputs = parserInputs });
        using var result = worker.Process(new MemoryStream(request));
        return JsonSerializer.Deserialize<Response>(result).Outputs;
    }

    private void SetupParserToReturnImages(params Api.Image[] images)
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>()))
              .Returns(new Api.Response { Xml = "<akomaNtoso/>", Meta = new Api.Meta(), Images = images });
    }

    [Fact]
    public void SuccessfulRunSavesXmlMetadataAndLogUnderOutputPrefix()
    {
        Run(inputs);

        uploads.Keys.ShouldBe(["consignment/TDR-2024-ABC.xml", "consignment/metadata.json", "consignment/parser.log"],
            ignoreOrder: true);
    }

    [Fact]
    public void SuccessfulRunReportsTheSavedFilenamesAndNoErrors()
    {
        var outputs = Run(inputs);

        outputs.XMLFilename.ShouldBe("TDR-2024-ABC.xml");
        outputs.MetadataFilename.ShouldBe("metadata.json");
        outputs.LogFilename.ShouldBe("parser.log");
        outputs.ErrorMessages.ShouldBeEmpty();
    }

    [Fact]
    public void SavesTheParsedXmlAsXml()
    {
        Run(inputs);

        uploads["consignment/TDR-2024-ABC.xml"].ContentType.ShouldBe("application/xml");
        Encoding.UTF8.GetString(uploads["consignment/TDR-2024-ABC.xml"].Content).ShouldBe("<akomaNtoso/>");
    }

    [Fact]
    public void SavesTheParsedMetadataAsJson()
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>()))
              .Returns(new Api.Response
              {
                  Xml = "<akomaNtoso/>",
                  Meta = new Api.Meta { Court = "UKUT-IAC", Cite = "[2024] UKUT 1 (IAC)" },
                  Images = []
              });

        Run(inputs);

        uploads["consignment/metadata.json"].ContentType.ShouldBe("application/json");
        var metadata = JsonDocument.Parse(uploads["consignment/metadata.json"].Content).RootElement;
        metadata.GetProperty("court").GetString().ShouldBe("UKUT-IAC");
        metadata.GetProperty("cite").GetString().ShouldBe("[2024] UKUT 1 (IAC)");
    }

    [Fact]
    public void SavesEachImageWithItsOwnContentType()
    {
        var png = new Api.Image { Name = "image1.png", Type = "image/png", Content = [4, 5] };
        var jpeg = new Api.Image { Name = "image2.jpeg", Type = "image/jpeg", Content = [6] };
        SetupParserToReturnImages(png, jpeg);

        var outputs = Run(inputs);

        uploads["consignment/image1.png"].ContentType.ShouldBe("image/png");
        uploads["consignment/image1.png"].Content.ShouldBe(png.Content);
        uploads["consignment/image2.jpeg"].ContentType.ShouldBe("image/jpeg");
        uploads["consignment/image2.jpeg"].Content.ShouldBe(jpeg.Content);
        outputs.ImageFilenames.ShouldBe(["image1.png", "image2.jpeg"]);
    }

    [Fact]
    public void ParserIsGivenTheDocxHintMetadataAndAttachments()
    {
        var hint = Api.Hint.Judgment;
        var meta = new Api.Meta { Court = "EWCA" };
        List<Api.Attachment> attachments = [new() { Content = [7], Type = Api.AttachmentType.Order }];
        mockInputHelper.Setup(x => x.GetHint(It.Is<ParserInputs>(i => i.ConsignmentReference == inputs.ConsignmentReference),
                       It.IsAny<Microsoft.Extensions.Logging.ILogger>())).Returns(hint);
        mockInputHelper.Setup(x => x.GetMetadata(It.Is<ParserInputs>(i => i.ConsignmentReference == inputs.ConsignmentReference),
                       It.IsAny<Microsoft.Extensions.Logging.ILogger>())).Returns(meta);
        inputs.AttachmentURLs = ["https://example.com/attachment"];
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>())).Returns(Docx);
        mockExternalResourceRetriever.Setup(x => x.GetAttachments(inputs.AttachmentURLs)).Returns((attachments, []));

        Run(inputs);

        mockParser.Verify(x => x.Parse(It.Is<Api.Request>(r =>
            r.Content == Docx && r.Hint == hint && r.Meta == meta && r.Attachments == attachments)), Times.Once);
    }

    [Fact]
    public void AttachmentsAreNotRetrievedWhenThereAreNoAttachmentUrls()
    {
        inputs.AttachmentURLs = null;

        Run(inputs);

        mockExternalResourceRetriever.Verify(x => x.GetAttachments(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public void AttachmentErrorsAreReportedButParsingStillHappens()
    {
        inputs.AttachmentURLs = ["https://example.com/missing"];
        mockExternalResourceRetriever.Setup(x => x.GetAttachments(inputs.AttachmentURLs))
                                 .Returns(([], ["error reading attachment https://example.com/missing"]));

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error reading attachment https://example.com/missing"]);
        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Once);
    }

    [Fact]
    public void MissingParserInputsIsReportedWithoutThrowing()
    {
        var outputs = Run(null);

        outputs.ErrorMessages.ShouldBe(["parser-inputs is null"]);
        uploads.ShouldBeEmpty();
    }

    [Fact]
    public void UnreadableDocxIsReportedAndLogIsSavedAndParserIsNotCalled()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>())).Throws(new IOException("no such object"));

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error reading .docx file"]);
        uploads.Keys.ShouldBe(["consignment/parser.log"]);
        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void UnreadableDocumentTypeIsReportedAndLogIsSavedAndParserIsNotCalled()
    {
        mockInputHelper.Setup(x => x.GetHint(It.IsAny<ParserInputs>(), It.IsAny<Microsoft.Extensions.Logging.ILogger>()))
                   .Throws(new Exception("unknown document type"));

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error reading document type"]);
        uploads.Keys.ShouldBe(["consignment/parser.log"]);
        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void UnreadableInputMetadataIsReportedAndLogIsSavedAndParserIsNotCalled()
    {
        mockInputHelper.Setup(x => x.GetMetadata(It.IsAny<ParserInputs>(), It.IsAny<Microsoft.Extensions.Logging.ILogger>()))
                   .Throws(new Exception("input URI is not supported"));

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error reading metadata"]);
        uploads.Keys.ShouldBe(["consignment/parser.log"]);
        mockParser.Verify(x => x.Parse(It.IsAny<Api.Request>()), Times.Never);
    }

    [Fact]
    public void ParseFailureIsReportedAndLogIsSaved()
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>())).Throws(new Exception("not a docx"));

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error parsing document"]);
        uploads.Keys.ShouldBe(["consignment/parser.log"]);
    }

    [Fact]
    public void FailureToSaveXmlIsReportedButMetadataAndLogAreStillSaved()
    {
        keysWhichFailToUpload.Add("consignment/TDR-2024-ABC.xml");

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error saving xml"]);
        outputs.XMLFilename.ShouldBeNull();
        uploads.Keys.ShouldBe(["consignment/metadata.json", "consignment/parser.log"], ignoreOrder: true);
    }

    [Fact]
    public void FailureToSaveMetadataIsReportedButXmlAndLogAreStillSaved()
    {
        keysWhichFailToUpload.Add("consignment/metadata.json");

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error saving metadata"]);
        outputs.MetadataFilename.ShouldBeNull();
        uploads.Keys.ShouldBe(["consignment/TDR-2024-ABC.xml", "consignment/parser.log"], ignoreOrder: true);
    }

    [Fact]
    public void FailureToSaveAnImageIsReportedAndOtherImagesAreStillSaved()
    {
        var first = new Api.Image { Name = "image1.png", Type = "image/png", Content = [4] };
        var second = new Api.Image { Name = "image2.png", Type = "image/png", Content = [5] };
        SetupParserToReturnImages(first, second);
        keysWhichFailToUpload.Add("consignment/image1.png");

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error saving image image1.png"]);
        outputs.ImageFilenames.ShouldBe(["image2.png"]);
    }

    [Fact]
    public void FailureToSaveLogIsReportedAndLogFilenameIsOmitted()
    {
        keysWhichFailToUpload.Add("consignment/parser.log");

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBe(["error saving log file"]);
        outputs.LogFilename.ShouldBeNull();
    }

    [Fact]
    public void ParserReturningNoImagesIsNotAnError()
    {
        mockParser.Setup(x => x.Parse(It.IsAny<Api.Request>()))
              .Returns(new Api.Response { Xml = "<akomaNtoso/>", Meta = new Api.Meta(), Images = null });

        var outputs = Run(inputs);

        outputs.ErrorMessages.ShouldBeEmpty();
    }

    [Fact]
    public void LogSavedIsTheLogOfThisRunOnly()
    {
        var messages = new List<LogMessage>();
        mockLoggerProvider.Setup(x => x.Reset()).Returns(messages);

        Run(inputs);

        mockLoggerProvider.Verify(x => x.Reset(), Times.Once);
        JsonDocument.Parse(uploads["consignment/parser.log"].Content).RootElement.GetArrayLength().ShouldBe(0);
    }
}
