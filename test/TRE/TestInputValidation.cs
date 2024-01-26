
using System;
using Xunit;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw.TRE.Test
{
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
            string uri = "uksc/1900/1";
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
            string court = "UKCS";
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
            string court = "UKSC";
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
            string date = "1900-0";
            ParserInputs inputs = new()
            {
                DocumentType = ParserInputs.JudgmentDocumentType,
                Metadata = new InputMetadata() { Date = date }
            };
            Assert.Throws<Exception>(() => { InputHelper.GetMetadata(inputs, Logger); });
        }

    }

}
