#nullable enable

using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.coa;

public class TestNeutralCitation : ParserTestBase
{
    private static readonly NeutralCitationEnricher Enricher = new();

    [Theory]
    [InlineData("Neutral Citation Number: [2022] EWCA Civ 733", "[2022] EWCA Civ 733")]
    [InlineData("Neutral Citation Number: [2007] EWHC 197 (Comm)", "[2007] EWHC 197 (Comm)")]
    [InlineData("Neutral Citation Number: [2003] EWHC 301 Admin", "[2003] EWHC 301 Admin")]
    [InlineData("Neutral Citation Number: [2006] EWCH 2373 (Admin)", "[2006] EWCH 2373 (Admin)")] // misspelling of EWHC
    [InlineData("Neutral Citation Number: [2022] EHWC 950 (Ch)", "[2022] EHWC 950 (Ch)")] // misspelling of EWHC
    [InlineData("Neutral Citation Number: [2026] EWHC 1234 (BP)", "[2026] EWHC 1234 (BP)")]
    [InlineData("Neutral Citation Number: [2021] EWCOP 12", "[2021] EWCOP 12")]
    [InlineData("Neutral Citation Number: [2020] EWFC 45 (B)", "[2020] EWFC 45 (B)")]
    [InlineData("Neutral Citation Number: [2017] EWCA 1798 (Civ)", "[2017] EWCA 1798 (Civ)")]
    [InlineData("Neutral Citation Number: [2017] EWCA 1798 Civ", "[2017] EWCA 1798 Civ")]
    [InlineData("Neutral Citation Number: [2019] EWCC 3", "[2019] EWCC 3")]
    [InlineData("Neutral Citation Number: [2019] EWCR 3", "[2019] EWCR 3")]
    [InlineData("Neutral Citation Number: [2021] EAT 5", "[2021] EAT 5")]
    [InlineData("Neutral Citation Nunber: [2006] EWCA Civ 1507", "[2006] EWCA Civ 1507")] // misspelling of Number
    [InlineData("Neutral Citation Numer: [2015] EWHC 411 (Ch)", "[2015] EWHC 411 (Ch)")] // misspelling of Number
    [InlineData("Neutral Citation Number: [2015] UKIPTrib 5", "[2015] UKIPTrib 5")]
    [InlineData("NCN: [2021] EWCA Crim 1412", "[2021] EWCA Crim 1412")]
    [InlineData("NCN No: [2022] EWCA Crim 39", "[2022] EWCA Crim 39")]
    [InlineData("Neutral Citation Number: [2018[ EWCA Civ 1744", "[2018[ EWCA Civ 1744")] // stray bracket typo
    [InlineData("NUETRAL CITATION NUMBER: [2006] EWHC 1609 (Ch)", "[2006] EWHC 1609 (Ch)")] // typo in neutral
    [InlineData("Neutral Citaion Number: [2013] EWHC 3865 (Admin)", "[2013] EWHC 3865 (Admin)")] // typo in citation
    [InlineData("NC NUMBER: [2019] EWCA Civ 1779", "[2019] EWCA Civ 1779")]
    [InlineData("NC. [2019] EWHC 818 (Fam)", "[2019] EWHC 818 (Fam)")]
    [InlineData("NC: [2026] EWCA Crim 184", "[2026] EWCA Crim 184")]
    [InlineData("Neutral Citation Number: [2022] EWCA Civ 733", "[2022] EWCA Civ 733")] // NBSP between prefix and citation
    [InlineData("NCN: [2021] EWCA Crim 1412", "[2021] EWCA Crim 1412")] // NBSP between short prefix and citation
    [InlineData("Neutral Citation Number: [2007] EWHC 197 (Comm)", "[2007] EWHC 197 (Comm)")] // NBSP inside the citation itself
    public void Enrich_SingleRunWithNeutralCitationLabel_MarksUpTheCitation(string text, string expectedCitation)
    {
        var line = StyledLine(text, BoldStyling);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldMatch<WText>(text[..^expectedCitation.Length], BoldStyling);
        contents[1].ShouldMatch<WNeutralCitation>(expectedCitation, BoldStyling);
    }

    [Theory]
    [InlineData("[2022] EWCA (Crim) 733")] // crim in brackets
    [InlineData("[2022] EWCA 733 civ")] // civ not in brackets
    [InlineData("[2003] EWHC 320 (Admlty.)")] // trailing period after Admlty
    [InlineData("[2021] EWHC [3505] (IPEC)")] // number itself in brackets
    [InlineData("[2021] EWCOP 12 (T1)")]
    [InlineData("[2019] EWCC 3")]
    [InlineData("[2019] EWCR 3")]
    [InlineData("[2018] EAT 5")]
    [InlineData("[2026] EWHC 1234 (BP)")]
    [InlineData("[2022] EWCA Civ 733")] // NBSP throughout, no label
    public void Enrich_SingleRunWithoutLabel_MarksUpTheCitation(string expectedCitation)
    {
        var line = StyledLine(expectedCitation, ItalicStyling);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.ShouldHaveSingleItem().ShouldMatch<WNeutralCitation>(expectedCitation, ItalicStyling);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(" ")]
    public void Enrich_SingleRunWithLeadingWhitespace_MarksUpTheCitation(string leadingWhitespace)
    {
        var line = TextLine(leadingWhitespace + "[2022] EWHC 307 (Admin)");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeOfType<WText>().Text.ShouldBe(leadingWhitespace);
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] EWHC 307 (Admin)");
    }

    [Theory]
    [InlineData("This is just ordinary text.")]
    [InlineData("EWCA Civ 100")] // no year, no brackets
    [InlineData("Neutral Citation Number: to follow")]
    [InlineData("Ref [2023] EWFC 194 (B)")]
    public void Enrich_TextDoesNotContainNeutralCitation_LineIsUnchanged(string text)
    {
        var line = TextLine(text);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(line);
    }

    [Fact]
    public void Enrich_SingleRunMentioningLinkedJudgments_MarksUpTrailingCitationAsRefAndPreservesRunProperties()
    {
        var line = StyledLine("This judgment is linked to [2023] EWFC 194", BoldStyling);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldMatch<WText>("This judgment is linked to ", BoldStyling);

        contents[1].ShouldMatch<WRef>("[2023] EWFC 194", BoldStyling)
                   .IsNeutral.ShouldBe(true);
    }

    [Fact]
    public void Enrich_FirstOfTwoRunsMentionsLinkedJudgments_MarksUpTrailingCitationAsRefAndPreservesEachRunsProperties()
    {
        IInline[] lineContents =
        [
            new WText("These linked appeals are ", BoldStyling),
            new WText("[2023] EWFC 169", ItalicStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldBeSameAs(lineContents[0]);

        contents[1].ShouldMatch<WRef>("[2023] EWFC 169", ItalicStyling)
                   .IsNeutral.ShouldBe(true);
    }

    [Fact]
    public void Enrich_LabelRunThenExactCitationRun_MarksUpCitationAndPreservesEachRunsProperties()
    {
        IInline[] lineContents =
        [
            new WText("Neutral Citation Number:", BoldStyling),
            new WText("[2018] EWCA Civ 1744", ItalicStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldMatch<WNeutralCitation>("[2018] EWCA Civ 1744", ItalicStyling);
    }

    [Theory]
    [InlineData("Neutral Citation Number: [")]
    [InlineData("Neutral Citation Number:  [")]
    [InlineData("Neutral Citation No. [")]
    [InlineData("Neutral Citation figure: [")]
    public void Enrich_LabelRunEndsWithOpenBracket_JoinsWithNextRunAsCitation(string label)
    {
        var line = TextLine(label, "2018] EWCA Civ 1744");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe(label[..^1]);
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2018] EWCA Civ 1744");
    }

    [Fact]
    public void Enrich_CitationRunHasTrailingWhitespace_SplitsOffTrailingWhitespaceAndPreservesEachRunsProperties()
    {
        IInline[] lineContents =
        [
            new WText("Neutral Citation: ", BoldStyling),
            new WText("[2009] EWCA Civ 400     ", ItalicStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);
        contents[0].ShouldBeSameAs(lineContents[0]);

        contents[1].ShouldMatch<WNeutralCitation>("[2009] EWCA Civ 400", ItalicStyling);
        contents[2].ShouldMatch<WText>("     ", ItalicStyling);
    }

    [Fact]
    public void Enrich_LabelRunWithoutColonThenColonAndCitationRun_SplitsOffColonAndPreservesEachRunsProperties()
    {
        IInline[] lineContents =
        [
            new WText("Neutral Citation Number", BoldStyling),
            new WText(": [2005] EWHC 279 (Comm)", ItalicStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[0].ShouldBeSameAs(lineContents[0]);

        contents[1].ShouldMatch<WText>(": ", ItalicStyling);
        contents[2].ShouldMatch<WNeutralCitation>("[2005] EWHC 279 (Comm)", ItalicStyling);
    }

    [Fact]
    public void Enrich_ClosingParenthesisInItsOwnRun_JoinsAsCitationWithFirstRunsPropertiesOnly()
    {
        // known limitation: the closing-parenthesis run's formatting is silently dropped
        IInline[] lineContents =
        [
            new WText("Neutral Citation Number: [2011] EWHC 3553 (Ch", BoldStyling),
            new WText(")", ItalicStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldMatch<WText>("Neutral Citation Number: ", BoldStyling);
        contents[1].ShouldMatch<WNeutralCitation>("[2011] EWHC 3553 (Ch)", BoldStyling);
    }

    [Fact]
    public void Enrich_OpeningBracketInItsOwnRun_JoinsAsCitationWithFirstRunsPropertiesOnly()
    {
        // known limitation: the second run's formatting is silently dropped
        IInline[] lineContents =
        [
            new WText("[", BoldStyling),
            new WText("2021] EWCA Civ 2776", ItalicStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].Contents.ShouldHaveSingleItem().ShouldMatch<WNeutralCitation>("[2021] EWCA Civ 2776", BoldStyling);
    }

    [Fact]
    public void Enrich_LabelThenCitationThenTrailingRun_MarksUpCitationAndPreservesEachRunsProperties()
    {
        IInline[] lineContents =
        [
            new WText("Neutral Citation Number:", BoldStyling),
            new WText("[2018] EWCA Civ 1744", ItalicStyling),
            new WText(" (unreported)", UnderlineStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[0].ShouldBeSameAs(lineContents[0]);

        contents[1].ShouldMatch<WNeutralCitation>("[2018] EWCA Civ 1744", ItalicStyling);

        contents[2].ShouldBeSameAs(lineContents[2]);
    }

    [Theory]
    [InlineData("Neutral Citation Number:")]
    [InlineData("NCN:")]
    public void
        Enrich_LabelThenWhitespaceRunThenCitationRunThenMoreRuns_MarksUpTheCitationAndPreservesEachRunsProperties(
            string label)
    {
        IInline[] lineContents =
        [
            new WText(label, BoldStyling),
            new WText(" ", UnderlineStyling),
            new WText("[2021] EWCA Crim 1412", ItalicStyling),
            new WText(" (unreported)", BoldStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(4);

        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldBeSameAs(lineContents[1]);
        contents[2].ShouldMatch<WNeutralCitation>("[2021] EWCA Crim 1412", ItalicStyling);
        contents[3].ShouldBeSameAs(lineContents[3]);
    }

    [Fact]
    public void Enrich_LineBreakThenLabelAndCitationInOneRun_MarksUpCitationAndPreservesEachRunsProperties()
    {
        IInline[] lineContents =
        [
            new WLineBreak(),
            new WText("Neutral Citation Number: [2017] EWCA Civ 1798", BoldStyling),
            new WText(" (unreported)", ItalicStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(4);

        contents[0].ShouldBeSameAs(lineContents[0]);

        contents[1].ShouldMatch<WText>("Neutral Citation Number: ", BoldStyling);
        contents[2].ShouldMatch<WNeutralCitation>("[2017] EWCA Civ 1798", BoldStyling);

        contents[3].ShouldBeSameAs(lineContents[2]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void Enrich_WhitespaceThenLineBreakThenCitation_MarksUpTheCitationAfterTheLineBreak(string whitespace)
    {
        IInline[] lineContents =
        [
            Text(whitespace),
            new WLineBreak(),
            Text("Neutral Citation Number: [2017] EWCA Civ 1798")
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(4);

        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldBeSameAs(lineContents[1]);
        contents[2].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation Number: ");
        contents[3].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2017] EWCA Civ 1798");
    }

    [Fact]
    public void Enrich_CitationSplitAcrossThreeRuns_MarksUpConcatenatedCitationWithFirstRunsPropertiesOnly()
    {
        IInline[] lineContents =
        [
            new WText("Neutral Citation Number: [2020", BoldStyling),
            new WText("] EWHC 100", ItalicStyling),
            new WText(" (QB)", UnderlineStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldMatch<WText>("Neutral Citation Number: ", BoldStyling);
        contents[1].ShouldMatch<WNeutralCitation>("[2020] EWHC 100 (QB)", BoldStyling);
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

    [Fact]
    public void Enrich_SingleRunWithLabelAndTrailingText_PreservesRunProperties()
    {
        var line = StyledLine("Neutral Citation Number: [2022] EWCA Civ 733 (unreported)", BoldStyling);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[0].ShouldMatch<WText>("Neutral Citation Number: ", BoldStyling);
        contents[1].ShouldMatch<WNeutralCitation>("[2022] EWCA Civ 733", BoldStyling);
        contents[2].ShouldMatch<WText>(" (unreported)", BoldStyling);
    }

    [Fact]
    public void Enrich_ClosingParenthesisInItsOwnRunThenMoreText_CitationPropertiesComeOnlyFromTheFirstRun()
    {
        // known limitation: the trailing run's formatting is dropped from the citation (though
        // it is correctly preserved on the separate trailing WText that follows the citation)
        IInline[] lineContents =
        [
            new WText("Neutral Citation Number: [2011] EWHC 3553 (Ch", BoldStyling),
            new WText(") and some other information", ItalicStyling)
        ];
        var line = new WLine(LineTemplate, lineContents);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[1].ShouldMatch<WNeutralCitation>("[2011] EWHC 3553 (Ch)", BoldStyling);
        contents[2].ShouldMatch<WText>(" and some other information", ItalicStyling);
    }
}
