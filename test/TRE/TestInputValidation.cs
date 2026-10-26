using System;

using Microsoft.Extensions.Logging;

using TRE.FindCaseLaw.Inputs;

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace UK.Gov.NationalArchives.CaseLaw.TRE.Test;

public class TestInputValidation
{

    private readonly ILogger Logger = Logging.Factory.CreateLogger<TestInputValidation>();

    [Fact]
    public void TesMBadDocType()
    {
        ParserInputs inputs = new()
        {
            DocumentType = "unrecognized"
        };
        Assert.Throws<Exception>(() => { InputHelper.GetHint(inputs, Logger); });
    }

    [Fact]
    public void TestMissingDocType()
    {
        var uri = "uksc/1900/1";
        ParserInputs inputs = new()
        {
            Metadata = new InputMetadata() { URI = uri }
        };
        Assert.Throws<Exception>(() => { InputHelper.GetMetadata(inputs, Logger); });
    }

    [Fact]
    public void TestBadUri()
    {
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata() { URI = "not/supported" }
        };
        Assert.Throws<Exception>(() => { InputHelper.GetMetadata(inputs, Logger); });
    }

    [Fact]
    public void TestNewStyleUri()
    {
        /// FCL may pass UUID-based URIs 
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata() { URI = "d-00000000-0000-0000-ffff-000000000000" }
        };
        InputHelper.GetMetadata(inputs, Logger);
    }

    [Fact]
    public void TestBadCite()
    {
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata() { Cite = "[1900] BAD 1" }
        };
        Assert.Throws<Exception>(() => { InputHelper.GetMetadata(inputs, Logger); });
    }

    [Fact]
    public void TestBadCourt()
    {
        var court = "UKCS";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata() { Court = court }
        };
        Assert.Throws<Exception>(() => { InputHelper.GetMetadata(inputs, Logger); });
    }

    [Fact]
    public void TestHintFromCourt()
    {
        var court = "UKSC";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata() { Court = court }
        };
        Judgments.Api.Hint? hint = InputHelper.GetHint(inputs, Logger);
        Assert.Equal(Judgments.Api.Hint.UKSC, hint);
    }

    [Fact]
    public void TestBadDate()
    {
        var date = "1900-0";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata() { Date = date }
        };
        Assert.Throws<Exception>(() => { InputHelper.GetMetadata(inputs, Logger); });
    }

    [Fact]
    public void TestConflictingURIandCite()
    {
        var uri = "uksc/1900/1";
        var cite = "[1900] UKPC 2";
        ParserInputs inputs = new()
        {
            DocumentType = ParserInputs.JudgmentDocumentType,
            Metadata = new InputMetadata() { URI = uri, Cite = cite }
        };
        var exception = Record.Exception(() => { InputHelper.GetMetadata(inputs, Logger); });
        Assert.Null(exception);
    }

}
