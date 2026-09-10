using System.Collections.Generic;
using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.coa;

public class TestNeutralCitationEnricher : ParserTestBase
{
    private readonly NeutralCitationEnricherForTests neutralCitationEnricher = new();

    /// <summary>
    /// Wrapper class for testing protected NeutralCitation properties.
    /// </summary>
    private class NeutralCitationEnricherForTests : NeutralCitationEnricher
    {
        public IEnumerable<IInline> TriggerEnrich(params IInline[] line)
        {
            return base.Enrich(line);
        }
    }

    [Theory]
    [InlineData("[2022] EWCA Crim 733", "[2022] EWCA Crim 733")]
    [InlineData("[2021] EWHC [3505] (IPEC)", "[2021] EWHC [3505] (IPEC)")]
    [InlineData("[2024] EAT 123", "[2024] EAT 123")]
    [InlineData("[2023] EWCOP 45", "[2023] EWCOP 45")]
    [InlineData("[2022] EWFC 67", "[2022] EWFC 67")]
    [InlineData("[2021] EWHC 123 (Ch)", "[2021] EWHC 123 (Ch)")]
    [InlineData("[2020] EWCA Civ 123", "[2020] EWCA Civ 123")]
    [InlineData("[2019] EWHC 456 (Admin)", "[2019] EWHC 456 (Admin)")]
    [InlineData("[2018] EWCA Crim 789", "[2018] EWCA Crim 789")]
    [InlineData("[2017] EWHC 101 (Comm)", "[2017] EWHC 101 (Comm)")]
    [InlineData("[2016] EWHC 202 (Fam)", "[2016] EWHC 202 (Fam)")]
    [InlineData("[2015] EWHC 303 (QB)", "[2015] EWHC 303 (QB)")]
    [InlineData("[2014] EWHC 404 (KB)", "[2014] EWHC 404 (KB)")]
    public void Enrich_OneLineJustNcn_TransformsToWNeutralCitation(string input, string expectedNcn)
    {
        var text = Text(input);

        var result = neutralCitationEnricher.TriggerEnrich(text).ToArray();

        result.ShouldHaveSingleItem()
              .ShouldBeOfType<WNeutralCitation>()
              .Text.ShouldBe(expectedNcn);
    }

    [Theory]
    [InlineData("Neutral Citation Number: [2026] EWCA Civ 972", "Neutral Citation Number: ", "[2026] EWCA Civ 972")]
    [InlineData("Neutral Citation: [2022] EWHC 1 (KB)", "Neutral Citation: ", "[2022] EWHC 1 (KB)")]
    [InlineData("Neutral Citation No. [2023] EWHC 2 (QB)", "Neutral Citation No. ", "[2023] EWHC 2 (QB)")]
    [InlineData("Neutral Citation Number [2024] EWCA Crim 3", "Neutral Citation Number ", "[2024] EWCA Crim 3")]
    [InlineData("NCN: [2021] EWCA Civ 1412", "NCN: ", "[2021] EWCA Civ 1412")]
    [InlineData("NCN: [2021] EWCA Crim 1412", "NCN: ", "[2021] EWCA Crim 1412")]
    [InlineData("NCN No: [2022] EWCA Crim 39", "NCN No: ", "[2022] EWCA Crim 39")]
    [InlineData("Neutral Citation Nunber: [2006] EWCA Civ 1507", "Neutral Citation Nunber: ", "[2006] EWCA Civ 1507")]
    [InlineData("Neutral Citation Numer: [2015] EWHC 411 (Ch)", "Neutral Citation Numer: ", "[2015] EWHC 411 (Ch)")]
    [InlineData("Neutral Citation Number: [2018[ EWCA Civ 1744", "Neutral Citation Number: ", "[2018[ EWCA Civ 1744")]
    [InlineData(" [2022] EWCA Crim 733", " ", "[2022] EWCA Crim 733")]
    [InlineData("Neutral Citation Number: [2025] UKIPTrib 1", "Neutral Citation Number: ", "[2025] UKIPTrib 1")]
    public void Enrich_OneLinePrefixAndNcn_TransformsToWTextPrefixAndWNeutralCitation(string input,
        string expectedPrefix, string expectedNcn)
    {
        var text = Text(input);

        var result = neutralCitationEnricher.TriggerEnrich(text).ToArray();

        result.Length.ShouldBe(2);

        result[0].ShouldBeOfType<WText>().Text.ShouldBe(expectedPrefix);
        result[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe(expectedNcn);
    }

    [Fact]
    public void Enrich_EmptyLine_ReturnsEmpty()
    {
        var result = neutralCitationEnricher.TriggerEnrich().ToArray();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Enrich_SingleNonTextElement_ReturnsUnchanged()
    {
        var lineBreak = new WLineBreak();

        var result = neutralCitationEnricher.TriggerEnrich(lineBreak).ToArray();

        result.ShouldHaveSingleItem().ShouldBeSameAs(lineBreak);
    }

    [Fact]
    public void Enrich_FirstTextContainsLinked_UsesCaseLawRefForEwfcCitationAtEnd()
    {
        var text = Text("These are linked judgments. [2023] EWFC 194");

        var result = neutralCitationEnricher.TriggerEnrich(text).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeOfType<WText>().Text.ShouldBe("These are linked judgments. ");
        var reference = result[1].ShouldBeOfType<WRef>();
        reference.Text.ShouldBe("[2023] EWFC 194");
        reference.IsNeutral.ShouldBe(true);
        reference.Type.ShouldBe(RefType.Case);
    }

    /// <summary>
    /// BUG: the "linked" check that guards CaseLawRef.EnrichFromEnd for the *last*-element path
    /// (mirroring the first-element check) can never actually be true. By the time it's reached,
    /// the first element is known not to contain "linked" (otherwise the first-element check
    /// above would already have returned). So a "linked" citation appearing only in a later
    /// element falls through to the plain WNeutralCitation replacement instead of CaseLawRef.
    /// </summary>
    [Fact]
    public void Enrich_LinkedTextInLastElementNotFirst_TreatedAsPlainCitationInsteadOfCaseLawRef_KnownBug()
    {
        var first = Text("Case note: ");
        var last = Text("this is linked. [2023] EWFC 194");

        var result = neutralCitationEnricher.TriggerEnrich(first, last).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeOfType<WText>().Text.ShouldBe("this is linked. ");
        // A plain WNeutralCitation, not the WRef that CaseLawRef.EnrichFromEnd would have produced.
        result[2].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2023] EWFC 194");
    }

    [Theory]
    [InlineData("Some heading: ", "[2022] EWCA Crim 733")]
    [InlineData("NCN: ", "[2021] EWCA Crim 1412")]
    public void Enrich_TwoElements_PrefixThenBareCitation_TransformsLastElement(string prefix, string ncn)
    {
        var first = Text(prefix);
        var last = Text(ncn);

        var result = neutralCitationEnricher.TriggerEnrich(first, last).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe(ncn);
    }

    [Fact]
    public void Enrich_TwoElements_LineBreakThenBareCitation_TransformsLastElement()
    {
        var lineBreak = new WLineBreak();
        var last = Text("[2022] EWCA Crim 733");

        var result = neutralCitationEnricher.TriggerEnrich(lineBreak, last).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(lineBreak);
        result[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] EWCA Crim 733");
    }

    [Fact]
    public void Enrich_LastElementNotWText_ReturnsUnchanged()
    {
        var first = Text("no citation here");
        var last = new WLineBreak();

        var result = neutralCitationEnricher.TriggerEnrich(first, last).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(last);
    }

    [Fact]
    public void Enrich_TwoPlainTextElements_NoSpecialPatternMatches_ReturnsUnchanged()
    {
        var first = Text("Hello");
        var second = Text("World");

        var result = neutralCitationEnricher.TriggerEnrich(first, second).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(second);
    }

    [Fact]
    public void Enrich_ThreeElements_NeutralCitationNumberColonPrefix_TransformsMiddleElement()
    {
        var first = Text("Neutral Citation Number: ");
        var second = Text("[2026] EWCA Civ 972");
        var third = Text(" see also");

        var result = neutralCitationEnricher.TriggerEnrich(first, second, third).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2026] EWCA Civ 972");
        result[2].ShouldBeSameAs(third);
    }

    /// <summary>
    /// BUG: this path builds the WNeutralCitation from the *entire* second element
    /// ("[" + fText2.Text), not from the matched group. Any text trailing the citation in the
    /// second element is silently swallowed into the citation instead of being left as plain text.
    /// </summary>
    [Fact]
    public void Enrich_OpenBracketAppendedToPrefix_TrailingTextIncorrectlyIncludedInCitation_KnownBug()
    {
        var first = Text("Neutral Citation Number: [");
        var second = Text("2022] EWCA Civ 733 (unreported)");

        var result = neutralCitationEnricher.TriggerEnrich(first, second).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation Number: ");
        // Should be "[2022] EWCA Civ 733", but the trailing "(unreported)" leaks in.
        result[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] EWCA Civ 733 (unreported)");
    }

    [Fact]
    public void Enrich_PrefixWithoutColonThenColonSpaceAndCitation_TransformsSecondElement()
    {
        var first = Text("Neutral Citation Number");
        var second = Text(": [2022] EWCA Civ 733");

        var result = neutralCitationEnricher.TriggerEnrich(first, second).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeOfType<WText>().Text.ShouldBe(": ");
        result[2].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] EWCA Civ 733");
    }

    [Fact]
    public void Enrich_FigurePrefixWithOpenBracket_TransformsSecondElement()
    {
        var first = Text("Neutral Citation figure: [");
        var second = Text("2022] EWCA Civ 733");

        var result = neutralCitationEnricher.TriggerEnrich(first, second).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation figure: ");
        result[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] EWCA Civ 733");
    }

    /// <summary>
    /// BUG: this path combines both elements into one match, but only does `line.Skip(1)`
    /// (dropping just the first element) instead of `line.Skip(2)`. The original closing-paren
    /// element is left dangling, duplicated, after the newly-built citation.
    /// </summary>
    [Fact]
    public void Enrich_TrailingCloseParenAsSecondElement_LeavesOriginalParenDangling_KnownBug()
    {
        var first = Text("Neutral Citation Number: [2011] EWHC 3553 (Ch");
        var second = Text(")");

        var result = neutralCitationEnricher.TriggerEnrich(first, second).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation Number: ");
        result[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2011] EWHC 3553 (Ch)");
        // The original ")" element re-appears instead of being consumed.
        result[2].ShouldBeSameAs(second);
    }

    [Fact]
    public void Enrich_OpenBracketAsFirstElement_CombinesAndTransforms()
    {
        var first = Text("[");
        var second = Text("2022] EWCA Civ 733");

        var result = neutralCitationEnricher.TriggerEnrich(first, second).ToArray();

        result.ShouldHaveSingleItem().ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] EWCA Civ 733");
    }

    [Fact]
    public void Enrich_WhitespaceSecondElement_RecognisedPrefixOnFirst_TransformsThirdElement()
    {
        var first = Text("Neutral Citation Number:");
        var second = Text(" ");
        var third = Text("[2022] EWCA Civ 733");
        var fourth = Text(" see also");

        var result = neutralCitationEnricher.TriggerEnrich(first, second, third, fourth).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(second);
        result[2].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] EWCA Civ 733");
        result[3].ShouldBeSameAs(fourth);
    }

    [Fact]
    public void Enrich_WhitespaceSecondElement_NoThirdElement_ReturnsUnchanged()
    {
        var first = Text("NCN:");
        var second = Text(" ");

        var result = neutralCitationEnricher.TriggerEnrich(first, second).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(second);
    }

    [Fact]
    public void Enrich_WhitespaceSecondElement_PrefixNotRecognised_ReturnsUnchanged()
    {
        var first = Text("Something Else:");
        var second = Text(" ");
        var third = Text("[2022] EWCA Civ 733");
        var fourth = Text(" filler");

        var result = neutralCitationEnricher.TriggerEnrich(first, second, third, fourth).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(second);
        result[2].ShouldBeSameAs(third);
        result[3].ShouldBeSameAs(fourth);
    }

    [Fact]
    public void Enrich_WhitespaceSecondElement_ThirdElementDoesNotMatch_ReturnsUnchanged()
    {
        var first = Text("NCN:");
        var second = Text(" ");
        var third = Text("not a valid citation");

        var result = neutralCitationEnricher.TriggerEnrich(first, second, third).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(second);
        result[2].ShouldBeSameAs(third);
    }

    [Theory]
    [InlineData("Neutral Citation Number: [", "not a valid citation")] // L true, M false
    [InlineData("Neutral Citation Number", ": not valid")] // N true, O false
    [InlineData("Neutral Citation figure: [", "not valid")] // P true, Q false
    [InlineData("not a citation (", ")")] // R true, S false
    [InlineData("[", "not valid")] // T true, U false
    public void Enrich_TwoElements_RecognisedPrefixButInvalidCitation_ReturnsUnchanged(string firstText,
        string secondText)
    {
        var first = Text(firstText);
        var second = Text(secondText);

        var result = neutralCitationEnricher.TriggerEnrich(first, second).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(second);
    }

    [Fact]
    public void Enrich_LineBreakThenFullCitationText_TransformsSecondElement()
    {
        var lineBreak = new WLineBreak();
        var second = Text("Neutral Citation Number: [2020] EWCA Civ 100");
        var third = Text(" trailing");

        var result = neutralCitationEnricher.TriggerEnrich(lineBreak, second, third).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(lineBreak);
        result[1].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation Number: ");
        result[2].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2020] EWCA Civ 100");
        result[3].ShouldBeSameAs(third);
    }

    [Fact]
    public void Enrich_LineBreakThenNonMatchingText_ReturnsUnchanged()
    {
        var lineBreak = new WLineBreak();
        var second = Text("not a citation at all");
        var third = Text(" trailing");

        var result = neutralCitationEnricher.TriggerEnrich(lineBreak, second, third).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(lineBreak);
        result[1].ShouldBeSameAs(second);
        result[2].ShouldBeSameAs(third);
    }

    [Fact]
    public void Enrich_ThreeTextElementsSplitAcrossRuns_ConcatenatedTextMatches_ReturnsReplacement()
    {
        var first = Text("Neutral Cit");
        var second = Text("ation Number: [2020] EWCA Civ");
        var third = Text(" 100");

        var result = neutralCitationEnricher.TriggerEnrich(first, second, third).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeOfType<WText>().Text.ShouldBe("Neutral Citation Number: ");
        result[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2020] EWCA Civ 100");
    }

    [Fact]
    public void Enrich_ThreeElements_SecondNotWText_ReturnsUnchanged()
    {
        var first = Text("a");
        var lineBreak = new WLineBreak();
        var third = Text("b");

        var result = neutralCitationEnricher.TriggerEnrich(first, lineBreak, third).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(lineBreak);
        result[2].ShouldBeSameAs(third);
    }

    [Fact]
    public void Enrich_ThreeTextElements_NoMatchInConcatenatedText_ReturnsUnchanged()
    {
        var first = Text("Hello");
        var second = Text(" there");
        var third = Text(" friend");

        var result = neutralCitationEnricher.TriggerEnrich(first, second, third).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(first);
        result[1].ShouldBeSameAs(second);
        result[2].ShouldBeSameAs(third);
    }
}
