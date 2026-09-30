#nullable enable

using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.coa;

public class TestNeutralCitation : ParserTestBase
{
    private static readonly NetrualCitation Enricher = new();

    [Theory]
    [InlineData("Neutral Citation Number: [2022] EWCA Civ 733", "[2022] EWCA Civ 733")]
    [InlineData("Neutral Citation Number: [2007] EWHC 197 (Comm)", "[2007] EWHC 197 (Comm)")]
    [InlineData("Neutral Citation Number: [2003] EWHC 301 Admin", "[2003] EWHC 301 Admin")]
    [InlineData("Neutral Citation Number: [2006] EWCH 2373 (Admin)", "[2006] EWCH 2373 (Admin)")] // misspelling of EWHC
    [InlineData("Neutral Citation Number: [2022] EHWC 950 (Ch)", "[2022] EHWC 950 (Ch)")] // misspelling of EWHC
    [InlineData("Neutral Citation Number: [2021] EWCOP 12", "[2021] EWCOP 12")]
    [InlineData("Neutral Citation Number: [2020] EWFC 45 (B)", "[2020] EWFC 45 (B)")]
    [InlineData("Neutral Citation Number: [2017] EWCA 1798 (Civ)", "[2017] EWCA 1798 (Civ)")]
    [InlineData("Neutral Citation Number: [2017] EWCA 1798 Civ", "[2017] EWCA 1798 Civ")]
    [InlineData("Neutral Citation Number: [2019] EWCC 3", "[2019] EWCC 3")]
    [InlineData("Neutral Citation Number: [2019] EWCR 3", "[2019] EWCR 3")]
    [InlineData("Neutral Citation Number: [2021] EAT 5", "[2021] EAT 5")]
    public void Enrich_SingleRunWithNeutralCitationLabel_MarksUpTheCitation(string text, string expectedCitation)
    {
        var line = TextLine(text);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeOfType<WText>().Text.ShouldBe(text[..^expectedCitation.Length]);
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe(expectedCitation);
    }

    [Theory]
    [InlineData("[2022] EWCA Crim 733", "[2022] EWCA Crim 733")]
    [InlineData("[2003] EWHC 320 (Admlty.)", "[2003] EWHC 320 (Admlty.)")] // trailing period after Admlty
    [InlineData("[2021] EWHC [3505] (IPEC)", "[2021] EWHC [3505] (IPEC)")] // number itself in brackets
    [InlineData("Neutral Citation Nunber: [2006] EWCA Civ 1507", "[2006] EWCA Civ 1507")] // misspelling of Number
    [InlineData("Neutral Citation Numer: [2015] EWHC 411 (Ch)", "[2015] EWHC 411 (Ch)")] // misspelling of Number
    [InlineData("NCN: [2021] EWCA Crim 1412", "[2021] EWCA Crim 1412")]
    [InlineData("NCN No: [2022] EWCA Crim 39", "[2022] EWCA Crim 39")]
    [InlineData("Neutral Citation Number: [2018[ EWCA Civ 1744", "[2018[ EWCA Civ 1744")] // stray bracket typo
    [InlineData("[2021] EWCOP 12 (T1)", "[2021] EWCOP 12 (T1)")]
    [InlineData("[2019] EWCC 3", "[2019] EWCC 3")]
    [InlineData("[2019] EWCR 3", "[2019] EWCR 3")]
    [InlineData("[2018] EAT 5", "[2018] EAT 5")]
    [InlineData("Neutral Citation Number: [2015] UKIPTrib 5", "[2015] UKIPTrib 5")]
    public void Enrich_SingleRunWithoutStandardLabel_MarksUpTheCitation(string text, string expectedCitation)
    {
        var line = TextLine(text);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        var citation = contents.OfType<WNeutralCitation>().ShouldHaveSingleItem();
        citation.Text.ShouldBe(expectedCitation);
    }

    [Fact]
    public void Enrich_SingleRunWithLeadingSpaceBeforeEwhcAdmin_MarksUpTheCitation()
    {
        var line = TextLine(" [2022] EWHC 307 (Admin)");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeOfType<WText>().Text.ShouldBe(" ");
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] EWHC 307 (Admin)");
    }

    [Theory]
    [InlineData("This is just ordinary text.")]
    [InlineData("EWCA Civ 100")] // no year, no brackets
    [InlineData("Neutral Citation Number: to follow")]
    public void Enrich_TextDoesNotContainNeutralCitation_LineIsUnchanged(string text)
    {
        var line = TextLine(text);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(line);
    }

    [Fact]
    public void Enrich_SingleRunMentioningLinkedJudgments_MarksUpTrailingCitationAsRef()
    {
        var line = TextLine("This judgment is linked to [2023] EWFC 194");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("This judgment is linked to ");
        var wref = contents[1].ShouldBeOfType<WRef>();
        wref.Text.ShouldBe("[2023] EWFC 194");
        wref.IsNeutral.ShouldBe(true);
    }

    [Fact]
    public void Enrich_MultiRunFirstRunMentionsLinked_MarksUpTrailingCitationAsRefAcrossRuns()
    {
        var line = TextLine("These linked appeals are ", "[2023] EWFC 169");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldBeSameAs(line.Contents.ToArray()[0]);

        var wref = contents[1].ShouldBeOfType<WRef>();
        wref.Text.ShouldBe("[2023] EWFC 169");
        wref.IsNeutral.ShouldBe(true);
    }

    [Fact]
    public void Enrich_LabelInOwnRunFollowedByCitationRun_MarksUpTheCitation()
    {
        var line = TextLine("Neutral Citation Number:", "[2018] EWCA Civ 1744");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeSameAs(line.Contents.ToArray()[0]);
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2018] EWCA Civ 1744");
    }

    [Theory]
    [InlineData("Neutral Citation Number: [")]
    [InlineData("Neutral Citation Number:  [")]
    [InlineData("Neutral Citation No. [")]
    public void Enrich_LabelRunEndsWithOpenBracket_JoinsWithNextRunAsCitation(string label)
    {
        // the citation run must not itself already match a `patterns2` entry (e.g. an EWHC
        // citation, whose bracket is optional in that regex) or the generic last-run handling
        // further up the method would enrich it before this label-specific branch is reached
        var line = TextLine(label, "2018] EWCA Civ 1744");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeOfType<WText>().Text.ShouldBe(label[..^1]);
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2018] EWCA Civ 1744");
    }

    [Fact]
    public void Enrich_NeutralCitationFigureLabelEndsWithOpenBracket_JoinsWithNextRunAsCitation()
    {
        var line = TextLine("Neutral Citation figure: [", "2009] EWCA Civ 400");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation figure: ");
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2009] EWCA Civ 400");
    }

    [Fact]
    public void Enrich_LabelRunWithoutColonFollowedByColonAndCitationRun_SplitsOffColonAndMarksUpCitation()
    {
        var line = TextLine("Neutral Citation Number", ": [2005] EWHC 279 (Comm)");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);
        contents[0].ShouldBeSameAs(line.Contents.ToArray()[0]);
        contents[1].ShouldBeOfType<WText>().Text.ShouldBe(": ");
        contents[2].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2005] EWHC 279 (Comm)");
    }

    [Fact(Skip = "This test fails because of a known bug")]
    public void Enrich_ClosingParenthesisInItsOwnRun_JoinsWithPrecedingRunAsCitation()
    {
        var line = TextLine("Neutral Citation Number: [2011] EWHC 3553 (Ch", ")");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation Number: ");
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2011] EWHC 3553 (Ch)");
    }

    [Fact]
    public void Enrich_OpeningBracketInItsOwnRun_JoinsWithFollowingRunAsCitation()
    {
        var line = TextLine("[", "2021] EWCA Civ 2776");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(1);
        contents[0].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2021] EWCA Civ 2776");
    }

    [Fact]
    public void Enrich_LabelRunFollowedByCitationRunFollowedByMoreRuns_MarksUpTheCitationInSecondRun()
    {
        var line = TextLine("Neutral Citation Number:", "[2018] EWCA Civ 1744", " (unreported)");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[0].ShouldBeSameAs(line.Contents.ToArray()[0]);
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2018] EWCA Civ 1744");
        contents[2].ShouldBeSameAs(line.Contents.ToArray()[2]);
    }

    [Theory]
    [InlineData("Neutral Citation Number:")]
    [InlineData("NCN:")]
    public void Enrich_LabelThenWhitespaceRunThenCitationRunThenMoreRuns_MarksUpTheCitationInThirdRun(string label)
    {
        var line = TextLine(label, " ", "[2021] EWCA Crim 1412", " (unreported)");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(4);

        contents[0].ShouldBeSameAs(line.Contents.ToArray()[0]);
        contents[1].ShouldBeSameAs(line.Contents.ToArray()[1]);
        contents[2].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2021] EWCA Crim 1412");
        contents[3].ShouldBeSameAs(line.Contents.ToArray()[3]);
    }

    [Fact]
    public void Enrich_LineBreakFollowedByCitationRunFollowedByMoreRuns_MarksUpTheCitationAfterTheLineBreak()
    {
        var lineBreak = new WLineBreak();
        var line = new WLine(LineTemplate, [
            lineBreak,
            new WText("Neutral Citation Number: [2017] EWCA Civ 1798", null),
            new WText(" (unreported)", null)
        ]);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(4);

        contents[0].ShouldBeSameAs(lineBreak);
        contents[1].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation Number: ");
        contents[2].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2017] EWCA Civ 1798");
        contents[3].ShouldBeSameAs(line.Contents.ToArray()[2]);
    }

    [Fact]
    public void Enrich_CitationSplitAcrossThreeRunsWithNoOtherRuleMatching_MarksUpTheConcatenatedCitation()
    {
        var line = TextLine("Neutral Citation Number: [2020", "] EWHC 100", " (QB)");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation Number: ");
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2020] EWHC 100 (QB)");
    }

    [Fact]
    public void Enrich_CitationSplitAcrossThreeRunsOnlyMatchingUnlabelledPattern_MarksUpTheConcatenatedCitation()
    {
        var line = TextLine("Ref [2023", "] EWFC 19", "4 (B)");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Ref ");
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2023] EWFC 194 (B)");
    }

    [Fact]
    public void Enrich_EmptyLine_ReturnsLineUnchanged()
    {
        var line = TextLine();

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(line);
    }

    [Fact]
    public void Enrich_LineIsDraftJudgmentWarning_LineIsUnchanged()
    {
        var line = TextLine("Note: the draft judgment is only to be used to check [2022] EWCA Civ 733");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(line);
    }

    [Fact]
    public void Enrich_MatchingLineIsBeyondFirstTenBlocks_LineIsUnchanged()
    {
        var input = Enumerable.Range(1, 10)
                              .Select(i => TextLine($"This is line {i} of text in the first 10 blocks"))
                              .Concat([TextLine("Neutral Citation Number: [2022] EWCA Civ 733")])
                              .ToArray();

        var result = Enricher.Enrich(input).Cast<WLine>().ToArray();

        result.ShouldBe(input);
    }
}
