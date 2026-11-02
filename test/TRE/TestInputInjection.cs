using Microsoft.Extensions.Logging;

using test.Mocks;

using TRE.FindCaseLaw.Inputs;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace test.TRE;

public class TestInputInjection
{
    private readonly byte[] Docx1 = DocumentHelpers.ReadDocx(1);
    private static readonly InputHelper InputHelper = new();

    [Fact]
    public void TestUri()
    {
        var uri = "uksc/1900/1";
        var expected = Api.URI.Domain + "id/" + uri;
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { URI = uri }
        };
        var response = LambdaTest(Docx1, null);
        Assert.NotEqual(expected, response.Meta.Uri);
        response = LambdaTest(Docx1, inputs);
        Assert.Equal(expected, response.Meta.Uri);
    }

    [Fact]
    public void TestCite()
    {
        var cite = "[1900] UKSC 1";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { Cite = cite }
        };
        var response = LambdaTest(Docx1, null);
        Assert.NotEqual(cite, response.Meta.Cite);
        response = LambdaTest(Docx1, inputs);
        Assert.Equal(cite, response.Meta.Cite);
    }

    [Fact]
    public void TestCourt()
    {
        var court = "UKSC";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { Court = court }
        };
        var response = LambdaTest(Docx1, null);
        Assert.NotEqual(court, response.Meta.Court);
        response = LambdaTest(Docx1, inputs);
        Assert.Equal(court, response.Meta.Court);
    }

    [Fact]
    public void TestDate()
    {
        var date = "1900-01-01";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { Date = date }
        };
        var response = LambdaTest(Docx1, null);
        Assert.NotEqual(date, response.Meta.Date);
        response = LambdaTest(Docx1, inputs);
        Assert.Equal(date, response.Meta.Date);
    }

    [Fact]
    public void TestName()
    {
        var name = "Jim";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { Name = name }
        };
        var response = LambdaTest(Docx1, null);
        Assert.NotEqual(name, response.Meta.Name);
        response = LambdaTest(Docx1, inputs);
        Assert.Equal(name, response.Meta.Name);
    }

    [Fact]
    public void TestPressSummary()
    {
        var uri = "uksc/1900/1/press-summary/1";
        var expected = Api.URI.Domain + "id/" + uri;
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.PressSummaryDocumentType,
            Metadata = new InputMetadata { URI = uri }
        };
        var response = LambdaTest(Docx1, null);
        Assert.NotEqual("pressSummary", response.Meta.DocumentType);
        Assert.NotEqual(expected, response.Meta.Uri);
        response = LambdaTest(Docx1, inputs);
        Assert.Equal("pressSummary", response.Meta.DocumentType);
        Assert.Equal(expected, response.Meta.Uri);
    }

    private static readonly ILogger Logger = Logging.Factory.CreateLogger<TestInputInjection>();

    internal static Api.Response LambdaTest(byte[] docx, ParserInputs inputs)
    {
        var hint = InputHelper.GetHint(inputs, Logger);
        var meta = InputHelper.GetMetadata(inputs, Logger);
        Api.Request apiRequest = new()
        {
            Content = docx,
            Hint = hint,
            Meta = meta
        };
        var parser = new Api.Parser(new MockLogger<Api.Parser>().Object, new Validator());
        return parser.Parse(apiRequest);
    }

    [Fact]
    public void TestConflictingURIandCite()
    {
        var uri = "uksc/1900/1";
        var cite = "[1900] UKPC 2";
        var expected = Api.URI.Domain + "id/" + uri;
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata
            {
                URI = uri,
                Cite = cite
            }
        };
        var response = LambdaTest(Docx1, inputs);
        Assert.Equal(expected, response.Meta.Uri);
        Assert.Equal(cite, response.Meta.Cite);
    }
}
