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

using TRE.FindCaseLaw;
using TRE.FindCaseLaw.Inputs;
using TRE.FindCaseLaw.Outputs;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace test.TRE.FindCaseLaw;

public class EndToEndTestWorker
{
    private static readonly byte[] InvalidDocx = Encoding.UTF8.GetBytes("this is not a docx file");

    private readonly Dictionary<string, string> uploads = new();
    private readonly Mock<IAmazonS3> mockS3 = new();
    private readonly Mock<IExternalResourceRetriever> mockExternalResourceRetriever = new();
    private readonly Worker worker;

    public EndToEndTestWorker()
    {
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((PutObjectRequest request, CancellationToken _) =>
              {
                  using var reader = new StreamReader(request.InputStream);
                  uploads[request.Key] = reader.ReadToEnd();
                  return new PutObjectResponse();
              });

        var loggerProvider = new CustomLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var parser = new Api.Parser(loggerFactory.CreateLogger<Api.Parser>(), new Validator());

        worker = new Worker(loggerFactory.CreateLogger<Worker>(), parser, loggerProvider, mockS3.Object,
            new InputHelper(), mockExternalResourceRetriever.Object, new FakeTimeProvider());
    }

    private JsonElement[] SavedLog()
    {
        return JsonDocument.Parse(uploads["consignment/" + "parser.log"]).RootElement.EnumerateArray().ToArray();
    }

    private ParserOutputs ProcessDocument(string docxResource, string documentType)
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>()))
                                     .Returns(DocumentHelpers.ReadDocx(docxResource));

        var request = JsonSerializer.SerializeToUtf8Bytes(new Request
        {
            Inputs = new ParserInputs
            {
                ConsignmentReference = "TDR-2024-ABC",
                S3Bucket = "output-bucket",
                S3OutputPrefix = "consignment/",
                S3InputBucket = "input-bucket",
                S3InputKey = "input.docx",
                DocumentType = documentType
            }
        });

        using var result = worker.Process(new MemoryStream(request));

        return JsonSerializer.Deserialize<Response>(result).Outputs;
    }

    [Fact]
    public void Process_Judgment_SavesXmlMetadataAndLog()
    {
        var outputs = ProcessDocument("test.judgments.test1.docx", ParserInputs.JudgmentDocumentType);

        outputs.XMLFilename.ShouldBe("TDR-2024-ABC.xml");
        outputs.MetadataFilename.ShouldBe("metadata.json");
        outputs.LogFilename.ShouldBe("parser.log");
        outputs.ErrorMessages.ShouldBeEmpty();

        uploads.Keys.ShouldBe(
            ["consignment/TDR-2024-ABC.xml", "consignment/metadata.json", "consignment/parser.log"],
            ignoreOrder: true);
        uploads["consignment/TDR-2024-ABC.xml"].ShouldContain("<judgment name=\"judgment\">");

        var metadata = JsonDocument.Parse(uploads["consignment/metadata.json"]).RootElement;
        metadata.GetProperty("documentType").GetString().ShouldBe("judgment");
        metadata.GetProperty("primary_source").GetProperty("route").GetString().ShouldBe("tdr");
        metadata.GetProperty("xml_contains_document_text").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Process_PressSummary_SavesXmlMetadataImageAndLog()
    {
        var outputs = ProcessDocument("test.ps.test1.docx", ParserInputs.PressSummaryDocumentType);

        outputs.XMLFilename.ShouldBe("TDR-2024-ABC.xml");
        outputs.MetadataFilename.ShouldBe("metadata.json");
        outputs.LogFilename.ShouldBe("parser.log");
        outputs.ImageFilenames.ShouldBe(["image1.png"]);
        outputs.ErrorMessages.ShouldBeEmpty();

        uploads.Keys.ShouldBe(
            [
                "consignment/TDR-2024-ABC.xml", "consignment/metadata.json", "consignment/image1.png",
                "consignment/parser.log"
            ],
            ignoreOrder: true);
        uploads["consignment/TDR-2024-ABC.xml"].ShouldContain("<doc name=\"pressSummary\">");

        var metadata = JsonDocument.Parse(uploads["consignment/metadata.json"]).RootElement;
        metadata.GetProperty("documentType").GetString().ShouldBe("pressSummary");
        metadata.GetProperty("primary_source").GetProperty("route").GetString().ShouldBe("tdr");
        metadata.GetProperty("xml_contains_document_text").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void InvalidDocxReportsParseErrorAndSavesLog()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>())).Returns(InvalidDocx);

        var request = JsonSerializer.SerializeToUtf8Bytes(new Request
        {
            Inputs = new ParserInputs
            {
                ConsignmentReference = "TDR-2024-ABC",
                S3Bucket = "output-bucket",
                S3OutputPrefix = "consignment/",
                S3InputBucket = "input-bucket",
                S3InputKey = "input.docx",
                DocumentType = ParserInputs.JudgmentDocumentType
            }
        });

        using var result = worker.Process(new MemoryStream(request));

        var outputs = JsonSerializer.Deserialize<Response>(result).Outputs;

        outputs.XMLFilename.ShouldBeNull();
        outputs.MetadataFilename.ShouldBeNull();
        outputs.LogFilename.ShouldBe("parser.log");

        uploads.Keys.ShouldBe(["consignment/" + "parser.log"]);

        var errorEntries = SavedLog().Where(e => e.GetProperty("level").GetString() == "error").ToArray();
        errorEntries.Length.ShouldBe(1);
        errorEntries[0].GetProperty("class").GetString().ShouldBe(typeof(Worker).FullName);
        errorEntries[0].GetProperty("message").GetString()
                       .ShouldBe("error processing document: File contains corrupted data.");

        outputs.ErrorMessages.ShouldBe([errorEntries[0].GetProperty("message").GetString()]);
    }

    [Fact]
    public void UnreadableDocxReportsReadErrorAndSavesLog()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>()))
                                     .Throws(new IOException("no such object"));

        var request = JsonSerializer.SerializeToUtf8Bytes(new Request
        {
            Inputs = new ParserInputs
            {
                ConsignmentReference = "TDR-2024-ABC",
                S3Bucket = "output-bucket",
                S3OutputPrefix = "consignment/",
                S3InputBucket = "input-bucket",
                S3InputKey = "input.docx",
                DocumentType = ParserInputs.JudgmentDocumentType
            }
        });

        using var result = worker.Process(new MemoryStream(request));

        var outputs = JsonSerializer.Deserialize<Response>(result).Outputs;

        outputs.XMLFilename.ShouldBeNull();
        outputs.MetadataFilename.ShouldBeNull();
        outputs.LogFilename.ShouldBe("parser.log");
        outputs.ErrorMessages.ShouldBe(["error processing document: no such object"]);

        var errorEntries = SavedLog().Where(e => e.GetProperty("level").GetString() == "error").ToArray();
        errorEntries.Length.ShouldBe(1);
        errorEntries[0].GetProperty("message").GetString()
                       .ShouldBe("error processing document: no such object");
    }

    [Fact]
    public void FailureToSaveLogIsReportedWithoutLogFilename()
    {
        mockExternalResourceRetriever.Setup(x => x.GetDocx(It.IsAny<ParserInputs>())).Returns(InvalidDocx);
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new AmazonS3Exception("access denied"));

        var request = JsonSerializer.SerializeToUtf8Bytes(new Request
        {
            Inputs = new ParserInputs
            {
                ConsignmentReference = "TDR-2024-ABC",
                S3Bucket = "output-bucket",
                S3OutputPrefix = "consignment/",
                S3InputBucket = "input-bucket",
                S3InputKey = "input.docx",
                DocumentType = ParserInputs.JudgmentDocumentType
            }
        });

        using var result = worker.Process(new MemoryStream(request));

        var outputs = JsonSerializer.Deserialize<Response>(result).Outputs;

        outputs.LogFilename.ShouldBeNull();

        var errorMessages = outputs.ErrorMessages.ToArray();
        errorMessages.Length.ShouldBe(2);
        errorMessages[0].ShouldBe("error processing document: File contains corrupted data.");
        errorMessages[1].ShouldBe("error saving parser.log to S3: access denied");
    }
}
