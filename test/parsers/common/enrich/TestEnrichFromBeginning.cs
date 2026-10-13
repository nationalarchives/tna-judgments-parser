#nullable enable

using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Wordprocessing;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

using Xunit;

namespace test.parsers.common.enrich;

public class TestEnrichFromBeginning : ParserTestBase
{
    private sealed class EnrichedByTest(string text, RunProperties properties) : WText(text, properties);

    private static IInline DoEnrich(IList<IInline> matchedItems)
    {
        var text = IInline.ToString(matchedItems);
        var properties = matchedItems.OfType<WText>().FirstOrDefault()?.properties;

        return new EnrichedByTest(text, properties!);
    }

    [Fact]
    public void Enrich_PatternDoesNotMatch_ReturnsOriginalSequenceAndNeverInvokesConstructor()
    {
        IInline[] input = [new WText("nothing to see here", BoldStyling)];
        var invoked = false;

        var result = EnrichFromBeginning.Enrich(input, @"^(\d{4})", _ =>
        {
            invoked = true;
            return null;
        });

        result.ShouldBeSameAs(input);
        invoked.ShouldBeFalse();
    }

    [Fact]
    public void Enrich_MatchSpansExactlyOneWholeRun_PassesOriginalRunByReferenceWithNoSplitting()
    {
        IInline[] input = [new WText("ID12345", BoldStyling)];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result.ShouldHaveSingleItem().ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
    }

    [Fact]
    public void Enrich_MatchStartsMidRun_SplitsLeadingRunAndPreservesOriginalPropertiesOnBothHalves()
    {
        IInline[] input = [new WText("Ref: ID12345", BoldStyling)];

        var result = EnrichFromBeginning.Enrich(input, @"(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldMatch<WText>("Ref: ", BoldStyling);
        result[1].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
    }

    [Fact]
    public void Enrich_MatchEndsMidRun_SplitsTrailingRunAndPreservesOriginalPropertiesOnBothHalves()
    {
        IInline[] input = [new WText("ID12345 and more text", ItalicStyling)];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldMatch<EnrichedByTest>("ID12345", ItalicStyling);
        result[1].ShouldMatch<WText>(" and more text", ItalicStyling);
    }

    [Fact]
    public void Enrich_MatchSpansTwoWholeRunsWithDifferentProperties_PassesBothOriginalRunsWithDistinctProperties()
    {
        IInline[] input =
        [
            new WText("ID123", BoldStyling),
            new WText("45", ItalicStyling)
        ];
        IList<IInline>? captured = null;

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", matchedItems =>
        {
            captured = matchedItems;
            return null;
        }).ToArray();

        captured.ShouldNotBeNull()
                .Count.ShouldBe(2);

        captured[0].ShouldBeSameAs(input[0]);
        captured[1].ShouldBeSameAs(input[1]);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Enrich_MatchStraddlesRunBoundary_SplitsBothRunsPreservingEachOwnersProperties()
    {
        IInline[] input =
        [
            new WText("Ref: ID12", BoldStyling),
            new WText("345 more", ItalicStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, @"(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldMatch<WText>("Ref: ", BoldStyling);
        result[1].ShouldMatch<EnrichedByTest>("ID12345",
            BoldStyling); // DoEnrich() takes properties from the first inside run
        result[2].ShouldMatch<WText>(" more", ItalicStyling);
    }

    [Fact]
    public void Enrich_MatchStartsAtRunBoundary_PassesPrecedingRunThroughUnchangedWithNoSplitting()
    {
        IInline[] input =
        [
            new WText("Ref: ", BoldStyling),
            new WText("ID12345", ItalicStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, @"(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldBeSameAs(input[0]);
        result[1].ShouldMatch<EnrichedByTest>("ID12345", ItalicStyling);
    }

    [Fact]
    public void Enrich_MatchEndsAtRunBoundary_PassesFollowingRunThroughUnchangedWithNoSplitting()
    {
        IInline[] input =
        [
            new WText("ID12345", BoldStyling),
            new WText(" and more", ItalicStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
        result[1].ShouldBeSameAs(input[1]);
    }

    [Fact]
    public void Enrich_MatchSpansThreeWholeRuns_PassesAllThreeOriginalRunsWithoutSplitting()
    {
        IInline[] input =
        [
            new WText("ID1", BoldStyling),
            new WText("23", ItalicStyling),
            new WText("45", UnderlineStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result.ShouldHaveSingleItem().ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
    }

    [Fact]
    public void Enrich_NonTextInlinePrecedesMatch_PassesItThroughUnchangedAndStillSplitsTheMatchingRun()
    {
        IInline[] input = [new WLineBreak(), new WText("ID12345 more", BoldStyling)];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldBeSameAs(input[0]);
        result[1].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
        result[2].ShouldMatch<WText>(" more", BoldStyling);
    }

    [Fact]
    public void Enrich_NonTextInlineFollowsMatch_PassesItThroughUnchanged()
    {
        IInline[] input = [new WText("ID12345", BoldStyling), new WLineBreak()];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
        result[1].ShouldBeSameAs(input[1]);
    }

    [Fact]
    public void Enrich_NonTextInlineWithinMatch_IsIncludedInWrappedText()
    {
        IInline[] input =
        [
            new WText("ID123", BoldStyling),
            new WLineBreak(),
            new WText("45", ItalicStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result.ShouldHaveSingleItem().ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
    }

    [Fact]
    public void Enrich_TabPrecedesMatch_PassesTabThroughUnchanged()
    {
        IInline[] input = [Tab(), new WText("ID12345", BoldStyling)];

        var result = EnrichFromBeginning.Enrich(input, @"(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldBeSameAs(input[0]);
        result[1].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
    }

    [Fact]
    public void Enrich_EmptyTextRunPrecedesMatch_IsPreservedWithoutAffectingPositionArithmetic()
    {
        IInline[] input =
        [
            new WText("", BoldStyling),
            new WText("ID12345", ItalicStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldBeSameAs(input[0]);
        result[1].ShouldMatch<EnrichedByTest>("ID12345", ItalicStyling);
    }

    [Fact]
    public void Enrich_OptionalTrailingPatternComponentInLaterRun_IncludesItInWrappedText()
    {
        IInline[] input =
        [
            new WText("(ABC", BoldStyling),
            new WText(") more", ItalicStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, @"^(\(ABC\)?)", DoEnrich).ToArray();

        result[0].ShouldMatch<EnrichedByTest>("(ABC)", BoldStyling);
        result[1].ShouldMatch<WText>(" more", ItalicStyling);
    }

    [Fact]
    public void Enrich_ZeroWidthInlineAtEndOfMatchSpanningRuns_IsLeftAfterTheWrappedText()
    {
        IInline[] input =
        [
            new WText("ID123", BoldStyling),
            new WText("45", ItalicStyling),
            new WLineBreak()
        ];

        var result = EnrichFromBeginning.Enrich(input, @"^(ID\d{5})", DoEnrich).ToArray();

        result[0].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
        result[1].ShouldBeSameAs(input[2]);
    }

    [Fact]
    public void Enrich_IndexRangeInsideSingleRun_SplitsRunAtBothEndsPreservingProperties()
    {
        IInline[] input = [new WText("Ref: ID12345 more", BoldStyling)];

        var result = EnrichFromBeginning.Enrich(input, 5, 12, DoEnrich);

        result.Length.ShouldBe(3);
        result[0].ShouldMatch<WText>("Ref: ", BoldStyling);
        result[1].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
        result[2].ShouldMatch<WText>(" more", BoldStyling);
    }

    [Fact]
    public void Enrich_IndexRangeOnRunBoundaries_PassesSurroundingRunsThroughUnchanged()
    {
        IInline[] input =
        [
            new WText("Ref: ", BoldStyling),
            new WText("ID12345", ItalicStyling),
            new WText(" more", UnderlineStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, 5, 12, DoEnrich);

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(input[0]);
        result[1].ShouldMatch<EnrichedByTest>("ID12345", ItalicStyling);
        result[2].ShouldBeSameAs(input[2]);
    }

    [Fact]
    public void Enrich_IndexRangeStraddlingRunBoundaries_SplitsBothRunsPreservingEachOwnersProperties()
    {
        IInline[] input =
        [
            new WText("Ref: ID12", BoldStyling),
            new WText("345 more", ItalicStyling)
        ];

        var result = EnrichFromBeginning.Enrich(input, 5, 12, DoEnrich);

        result.Length.ShouldBe(3);
        result[0].ShouldMatch<WText>("Ref: ", BoldStyling);
        result[1].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
        result[2].ShouldMatch<WText>(" more", ItalicStyling);
    }

    [Fact]
    public void Enrich_IndexRangeFollowingLineBreakAndTab_CountsTabAsOneCharacterAndLineBreakAsNone()
    {
        IInline[] input = [new WLineBreak(), Tab(), new WText("Ref: ID12345", BoldStyling)];

        var result = EnrichFromBeginning.Enrich(input, 6, 13, DoEnrich);

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(input[0]);
        result[1].ShouldBeSameAs(input[1]);
        result[2].ShouldMatch<WText>("Ref: ", BoldStyling);
        result[3].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
    }

    [Fact]
    public void Enrich_IndexRangeWhenConstructorReturnsNull_RemovesTheRangeFromTheResult()
    {
        IInline[] input = [new WText("Ref: ID12345 more", BoldStyling)];

        var result = EnrichFromBeginning.Enrich(input, 5, 12, _ => null);

        result.Length.ShouldBe(2);
        result[0].ShouldMatch<WText>("Ref: ", BoldStyling);
        result[1].ShouldMatch<WText>(" more", BoldStyling);
    }

    [Fact]
    public void Enrich_WLineOverload_NoMatch_ReturnsSameLineInstance()
    {
        var line = TextLine("ordinary text");

        var result = EnrichFromBeginning.Enrich(line, @"^(\d{4})", _ => null);

        result.ShouldBeSameAs(line);
    }

    [Fact]
    public void Enrich_WLineOverload_WithMatch_ReturnsNewLineWithWrappedElementPreservingProperties()
    {
        var line = StyledLine("ID12345", BoldStyling);

        var result = EnrichFromBeginning.Enrich(line, @"^(ID\d{5})", DoEnrich);

        result.Contents.ShouldHaveSingleItem()
              .ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
    }

    [Fact]
    public void Enrich_WLineOverload_MatchRequiresSplitting_ReturnsNewLineWithBeforeWrappedAndAfterContents()
    {
        var line = StyledLine("Ref: ID12345 more", BoldStyling);

        var result = EnrichFromBeginning.Enrich(line, @"(ID\d{5})", DoEnrich);

        var contents = result.Contents.ToArray();
        contents[0].ShouldMatch<WText>("Ref: ", BoldStyling);
        contents[1].ShouldMatch<EnrichedByTest>("ID12345", BoldStyling);
        contents[2].ShouldMatch<WText>(" more", BoldStyling);
    }
}
