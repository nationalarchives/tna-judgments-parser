using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using Moq;

using Shouldly;

using TRE.FindCaseLaw;
using TRE.FindCaseLaw.Inputs;

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
            new InputHelper(), mockExternalResourceRetriever.Object);
    }

    private JsonElement[] SavedLog()
    {
        return JsonDocument.Parse(uploads["consignment/" + "parser.log"]).RootElement.EnumerateArray().ToArray();
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

        var parserOutputsJson = JsonDocument.Parse(result).RootElement.ToString();

        parserOutputsJson.ShouldBe(
            """{"parser-outputs":{"xml":null,"metadata":null,"images":null,"log":"parser.log","error-messages":["error parsing document"]}}""");

        uploads.Keys.ShouldBe(["consignment/" + "parser.log"]);

        var errorEntries = SavedLog().Where(e => e.GetProperty("level").GetString() == "error").ToArray();
        errorEntries.Length.ShouldBe(1);
        errorEntries[0].GetProperty("class").GetString().ShouldBe(typeof(Worker).FullName);
        errorEntries[0].GetProperty("message").GetString().ShouldStartWith("parse error: ");
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

        var parserOutputsJson = JsonDocument.Parse(result).RootElement.ToString();

        parserOutputsJson.ShouldBe(
            """{"parser-outputs":{"xml":null,"metadata":null,"images":null,"log":"parser.log","error-messages":["error reading .docx file"]}}""");

        var errorEntries = SavedLog().Where(e => e.GetProperty("level").GetString() == "error").ToArray();
        errorEntries.Length.ShouldBe(1);
        errorEntries[0].GetProperty("message").GetString()
                       .ShouldBe("error reading .docx file: no such object");
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

        var parserOutputsJson = JsonDocument.Parse(result).RootElement.ToString();

        parserOutputsJson.ShouldBe(
            """{"parser-outputs":{"xml":null,"metadata":null,"images":null,"log":null,"error-messages":["error parsing document","error saving log"]}}""");
    }
}
