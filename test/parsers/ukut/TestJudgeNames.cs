#nullable enable

using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

using Xunit;

namespace test.parsers.ukut;

public class TestJudgeNames : ParserTestBase
{
    private static readonly JudgeNames Enricher = new();

    [Theory]
    [InlineData("TRIBUNAL JUDGE ALPHA")]
    [InlineData("TRIBUNAL JUDGE BRAVO QC")]
    [InlineData("UPPER TRIBUNAL JUDGE CHARLIE")]
    public void Enrich_JudgeOnLineAfterBefore_MarksUpJudge(string judgeName)
    {
        IBlock[] blocks =
        [
            TextLine("Before"),
            StyledLine(judgeName, BoldStyling),
            TextLine("Between")
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>(judgeName, BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_JudgeOnLineAfterDecidedBy_MarksUpJudge()
    {
        IBlock[] blocks =
        [
            TextLine("Decided by:"),
            StyledLine("TRIBUNAL JUDGE DELTA", BoldStyling),
            TextLine("Between")
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem()
                 .ShouldMatch<WJudge>("TRIBUNAL JUDGE DELTA", BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_JudgeSplitAcrossRuns_MarksUpEachRunAsJudge()
    {
        IBlock[] blocks =
        [
            TextLine("Before"),
            new WLine(LineTemplate,
                [new WText("TRIBUNAL JUDGE ", BoldStyling), new WText("echo foxtrot", ItalicStyling)])
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(blocks[0]);
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WJudge>("TRIBUNAL JUDGE ", BoldStyling);
        contents[1].ShouldMatch<WJudge>("echo foxtrot", ItalicStyling);
    }

    [Fact]
    public void Enrich_LinesPrecedingBefore_AreLeftUnchanged()
    {
        IBlock[] blocks =
        [
            TextLine("Judgment date: 24 July 2023"),
            TextLine("Before"),
            StyledLine("TRIBUNAL JUDGE ALPHA", BoldStyling)
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeSameAs(blocks[1]);
        result[2].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem()
                 .ShouldMatch<WJudge>("TRIBUNAL JUDGE ALPHA", BoldStyling);
    }

    [Theory]
    [InlineData("Tribunal Judge Alpha")]
    [InlineData("Upper Tribunal Judge Charlie")]
    public void Enrich_BeforeAndJudgeInSameRun_MarksUpJudge(string judgeName)
    {
        IBlock[] blocks = [StyledLine("Before: " + judgeName, BoldStyling)];

        var result = Enricher.Enrich(blocks).ToArray();

        var contents = result.ShouldHaveSingleItem().ShouldBeOfType<WLine>().Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WText>("Before: ", BoldStyling);
        contents[1].ShouldMatch<WJudge>(judgeName, BoldStyling);
    }

    [Fact]
    public void Enrich_BeforeAndJudgeSeparatedByTabOnSameLine_MarksUpJudge()
    {
        IInline[] lineContents =
        [
            new WText("Before:", BoldStyling),
            Tab(),
            new WText("Tribunal Judge Alpha", UnderlineStyling)
        ];
        IBlock[] blocks = [new WLine(LineTemplate, lineContents)];

        var result = Enricher.Enrich(blocks).ToArray();

        var contents = result.ShouldHaveSingleItem().ShouldBeOfType<WLine>().Contents.ToArray();
        contents.Length.ShouldBe(3);
        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldBeSameAs(lineContents[1]);
        contents[2].ShouldMatch<WJudge>("Tribunal Judge Alpha", UnderlineStyling);
    }

    [Fact]
    public void Enrich_NoBeforeLine_LeavesBlocksUnchanged()
    {
        IBlock[] blocks =
        [
            StyledLine("TRIBUNAL JUDGE ALPHA", BoldStyling),
            TextLine("Between")
        ];

        var result = Enricher.Enrich(blocks);

        result.ShouldBe(blocks);
    }

    [Fact]
    public void Enrich_BeforeFollowedByNonJudge_LeavesBlocksUnchanged()
    {
        IBlock[] blocks =
        [
            TextLine("Before"),
            TextLine("TRIBUNAL MEMBER GOLF")
        ];

        var result = Enricher.Enrich(blocks);

        result.ShouldBe(blocks);
    }
}
