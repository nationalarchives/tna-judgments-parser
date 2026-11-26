#nullable enable

using System;

using Microsoft.Extensions.Time.Testing;

using Shouldly;

using test.Mocks;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using Parser = UK.Gov.NationalArchives.Judgments.Api.Parser;

namespace test.ApiTests;

public class TestParser_Logging
{
    private static readonly DateTimeOffset Now = new(2024, 6, 1, 10, 30, 0, TimeSpan.Zero);

    private readonly MockLogger<Parser> logger = new();

    private readonly Parser parser;

    public TestParser_Logging()
    {
        parser = new Parser(logger.Object, new Validator(), new FakeTimeProvider(Now));
    }

    private string[] ParseJudgmentAndGetLog(Api.Hint? hint, int judgmentId)
    {
        parser.Parse(new Api.Request { Content = DocumentHelpers.ReadDocx(judgmentId), Hint = hint });

        return [.. logger.LogMessages];
    }

    [Fact]
    public void Parse_WithNoHintAndPerfectEwhcScore_LogsEwhcParserChosen()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (no hint)",
            "trying EWHC parser",
            "EWHC parser scored 7 of 7",
            "using EWHC parser (perfect score)",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":"https://caselaw.nationalarchives.gov.uk/id/ewca/civ/2003/547","Court":"EWCA-Civil","JurisdictionShortNames":[],"Cite":"[2003] EWCA Civ 547","Date":"2003-04-15","Name":"THE QUEEN ON THE APPLICATION OF \u201CB\u201D v ASHWORTH HOSPITAL AUTHORITY","Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(null, 1);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithNoHintAndPerfectUkscScore_LogsUkscParserChosenWithoutTryingUkut()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (no hint)",
            "trying EWHC parser",
            "EWHC parser scored 2 of 7",
            "trying UKSC parser",
            "UKSC parser scored 7 of 7",
            "using UKSC parser (perfect score)",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":"https://caselaw.nationalarchives.gov.uk/id/uksc/2024/14","Court":"UKSC","JurisdictionShortNames":[],"Cite":"[2024] UKSC 14","Date":"2024-05-08","Name":"Sharp Corp Ltd v Viterra BV","Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(null, 82);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithNoHintAndPerfectUkutScore_LogsUkutParserChosenAfterTryingUksc()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (no hint)",
            "trying EWHC parser",
            "EWHC parser scored 3 of 7",
            "trying UKSC parser",
            "UKSC parser scored 0 of 7",
            "trying UKUT parser",
            "UKUT parser scored 7 of 7",
            "using UKUT parser (perfect score)",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":"https://caselaw.nationalarchives.gov.uk/id/ukut/aac/2022/59","Court":"UKUT-AAC","JurisdictionShortNames":[],"Cite":"[2022] UKUT 59 (AAC)","Date":"2022-03-01","Name":"MW v Secretary of State for Work and Pensions","Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(null, 29);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithNoHintAndNoPerfectScoreWhereEwhcIsBest_LogsEwhcBestScoreThenPressSummaryFallback()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (no hint)",
            "trying EWHC parser",
            "EWHC parser scored 6 of 7",
            "trying UKSC parser",
            "UKSC parser scored 2 of 7",
            "trying UKUT parser",
            "UKUT parser scored 2 of 7",
            "using EWHC parser (best score 6 of 7)",
            "no perfect judgment parse, trying press summary parser",
            "press summary parser found no doc type, keeping best judgment parse",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":"https://caselaw.nationalarchives.gov.uk/id/ewhc/scco/2022/1148","Court":"EWHC-SeniorCourtsCosts","JurisdictionShortNames":[],"Cite":"[2022] EWHC 1148 (SCCO)","Date":"2022-05-04","Name":null,"Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(null, 30);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithNoHintAndNoPerfectScoreWhereUkutIsBest_LogsUkutBestScoreThenPressSummaryFallback()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (no hint)",
            "trying EWHC parser",
            "EWHC parser scored 2 of 7",
            "trying UKSC parser",
            "UKSC parser scored 0 of 7",
            "trying UKUT parser",
            "UKUT parser scored 6 of 7",
            "using UKUT parser (best score 6 of 7)",
            "no perfect judgment parse, trying press summary parser",
            "press summary parser found no doc type, keeping best judgment parse",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":"https://caselaw.nationalarchives.gov.uk/id/ukut/aac/2022/96","Court":"UKUT-AAC","JurisdictionShortNames":[],"Cite":"[2022] UKUT 96 (AAC)","Date":"2022-03-29","Name":null,"Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(null, 25);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithEwhcHint_LogsEwhcParserChosenByHintWithoutTryingOthers()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (EWHC)",
            "[STAGE] Reading docx",
            "[STAGE] Pre-parsing",
            "using EWHC parser (hint)",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":"https://caselaw.nationalarchives.gov.uk/id/ewca/civ/2003/547","Court":"EWCA-Civil","JurisdictionShortNames":[],"Cite":"[2003] EWCA Civ 547","Date":"2003-04-15","Name":"THE QUEEN ON THE APPLICATION OF \u201CB\u201D v ASHWORTH HOSPITAL AUTHORITY","Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(Api.Hint.EWHC, 1);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithEwcaHint_LogsEwhcParserChosenByHintWithoutTryingOthers()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (EWCA)",
            "[STAGE] Reading docx",
            "[STAGE] Pre-parsing",
            "using EWHC parser (hint)",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":"https://caselaw.nationalarchives.gov.uk/id/ewca/civ/2003/547","Court":"EWCA-Civil","JurisdictionShortNames":[],"Cite":"[2003] EWCA Civ 547","Date":"2003-04-15","Name":"THE QUEEN ON THE APPLICATION OF \u201CB\u201D v ASHWORTH HOSPITAL AUTHORITY","Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(Api.Hint.EWCA, 1);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithUkscHint_LogsUkscParserChosenByHintWithoutTryingOthers()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (UKSC)",
            "[STAGE] Reading docx",
            "[STAGE] Pre-parsing",
            "using UKSC parser (hint)",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":null,"Court":null,"JurisdictionShortNames":[],"Cite":null,"Date":null,"Name":null,"Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(Api.Hint.UKSC, 1);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Parse_WithUkutHint_LogsUkutParserChosenByHintWithoutTryingOthers()
    {
        string[] expected =
        [
            "[STAGE] Parsing document (UKUT)",
            "[STAGE] Reading docx",
            "[STAGE] Pre-parsing",
            "using UKUT parser (hint)",
            "[STAGE] Validating akoma ntoso",
            "[STAGE] Serializing xml",
            "[STAGE] Extracting metadata",
            """Extracted metadata is: {"DocumentType":"judgment","Uri":null,"Court":null,"JurisdictionShortNames":[],"Cite":null,"Date":null,"Name":"THE QUEEN ON THE APPLICATION OF \u201CB\u201D v ASHWORTH HOSPITAL AUTHORITY","Extensions":null,"Attachments":[]}""",
            "[STAGE] Converting images",
            "[STAGE] Returning response"
        ];

        var actual = ParseJudgmentAndGetLog(Api.Hint.UKUT, 1);

        actual.ShouldBe(expected);
    }
}
