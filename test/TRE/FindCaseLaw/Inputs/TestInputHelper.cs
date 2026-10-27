using System;

using Microsoft.Extensions.Logging;

using TRE.FindCaseLaw.Inputs;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.Judgments.Api;

using Xunit;

namespace test.TRE.FindCaseLaw.Inputs;

public class TestInputHelper
{
    private readonly ILogger logger = Logging.Factory.CreateLogger<TestInputHelper>();
    private readonly InputHelper inputHelper = new();


    [Fact]
    public void TesMBadDocType()
    {
        ParserInputs inputs = new() { DocumentType = "unrecognized" };
        Assert.Throws<ArgumentException>(() => { inputHelper.GetHint(inputs, logger); });
    }

    [Fact]
    public void TestMissingDocType()
    {
        var uri = "uksc/1900/1";
        ParserInputs inputs = new() { Metadata = new InputMetadata { URI = uri } };
        Assert.Throws<ArgumentException>(() => { inputHelper.GetMetadata(inputs, logger); });
    }

    [Fact]
    public void TestBadUri()
    {
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { URI = "not/supported" }
        };
        Assert.Throws<ArgumentException>(() => { inputHelper.GetMetadata(inputs, logger); });
    }

    [Fact]
    public void TestNewStyleUri()
    {
        /// FCL may pass UUID-based URIs 
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { URI = "d-00000000-0000-0000-ffff-000000000000" }
        };
        inputHelper.GetMetadata(inputs, logger);
    }

    [Fact]
    public void TestBadCite()
    {
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { Cite = "[1900] BAD 1" }
        };
        Assert.Throws<FormatException>(() => { inputHelper.GetMetadata(inputs, logger); });
    }

    [Fact]
    public void TestBadCourt()
    {
        var court = "UKCS";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { Court = court }
        };
        Assert.Throws<ArgumentException>(() => { inputHelper.GetMetadata(inputs, logger); });
    }

    [Fact]
    public void TestHintFromCourt()
    {
        var court = "UKSC";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { Court = court }
        };
        var hint = inputHelper.GetHint(inputs, logger);
        Assert.Equal(Hint.UKSC, hint);
    }

    [Fact]
    public void TestBadDate()
    {
        var date = "1900-0";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { Date = date }
        };
        Assert.Throws<FormatException>(() => { inputHelper.GetMetadata(inputs, logger); });
    }

    [Fact]
    public void TestConflictingURIandCite()
    {
        var uri = "uksc/1900/1";
        var cite = "[1900] UKPC 2";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata { URI = uri, Cite = cite }
        };
        var exception = Record.Exception(() => { inputHelper.GetMetadata(inputs, logger); });
        Assert.Null(exception);
    }
}
