#nullable enable

using System.Linq;

using DocumentFormat.OpenXml.Wordprocessing;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Judgments.Parse.UKSC;

using Xunit;

namespace test.parsers.uksc;

public class TestJudgeEnricher : ParserTestBase
{
    private static readonly JudgeEnricher Enricher = new();

    private static readonly WLine JudgmentJudgesTemplate = new(null,
        new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "JudgmentJudges" })));

    private static WLine JudgmentJudgesLine(params IInline[] contents)
    {
        return new WLine(JudgmentJudgesTemplate, contents);
    }

    [Theory]
    [InlineData("Lord Alpha, President")]
    [InlineData("Lord Bravo, Deputy President")]
    [InlineData("Lord Charlie-Delta")]
    [InlineData("Lady Echo")]
    public void Enrich_JudgeInJudgmentJudgesLine_MarksUpJudge(string judgeName)
    {
        IBlock[] blocks = [JudgmentJudgesLine(new WText(judgeName, BoldStyling))];

        var result = Enricher.Enrich(blocks).ToArray();

        result.ShouldHaveSingleItem().ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem()
              .ShouldMatch<WJudge>(judgeName, BoldStyling);
    }

    [Fact]
    public void Enrich_SeveralJudgesInJudgmentJudgesLine_MarksUpEachJudge()
    {
        IInline[] lineContents =
        [
            new WText("before", ItalicStyling),
            new WLineBreak(),
            new WLineBreak(),
            new WText("Lord Alpha, President", BoldStyling),
            new WLineBreak(),
            new WText("Lady Echo", BoldStyling),
            new WLineBreak(),
            new WText("Lord Foxtrot", BoldStyling)
        ];
        IBlock[] blocks = [JudgmentJudgesLine(lineContents)];

        var result = Enricher.Enrich(blocks).ToArray();

        var contents = result.ShouldHaveSingleItem().ShouldBeOfType<WLine>().Contents.ToArray();
        contents.Length.ShouldBe(8);
        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldBeSameAs(lineContents[1]);
        contents[2].ShouldBeSameAs(lineContents[2]);
        contents[3].ShouldMatch<WJudge>("Lord Alpha, President", BoldStyling);
        contents[4].ShouldBeSameAs(lineContents[4]);
        contents[5].ShouldMatch<WJudge>("Lady Echo", BoldStyling);
        contents[6].ShouldBeSameAs(lineContents[6]);
        contents[7].ShouldMatch<WJudge>("Lord Foxtrot", BoldStyling);
    }

    [Fact]
    public void Enrich_LinesPrecedingJudgmentJudgesLine_AreLeftUnchanged()
    {
        IBlock[] blocks =
        [
            TextLine("JUDGMENT"),
            JudgmentJudgesLine(new WText("Lord Alpha, President", BoldStyling))
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem()
                 .ShouldMatch<WJudge>("Lord Alpha, President", BoldStyling);
    }

    [Fact]
    public void Enrich_LinesFollowingJudgmentJudgesLine_AreLeftUnchanged()
    {
        IBlock[] blocks =
        [
            JudgmentJudgesLine(new WText("Lord Alpha, President", BoldStyling)),
            JudgmentJudgesLine(new WText("Lord Golf", BoldStyling))
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(2);
        result[0].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem()
                 .ShouldMatch<WJudge>("Lord Alpha, President", BoldStyling);
        result[1].ShouldBeSameAs(blocks[1]);
    }

    [Fact]
    public void Enrich_NoJudgmentJudgesLine_LeavesBlocksUnchanged()
    {
        IBlock[] blocks = [StyledLine("Lord Alpha, President", BoldStyling)];

        var result = Enricher.Enrich(blocks);

        result.ShouldBe(blocks);
    }

    [Fact]
    public void Enrich_JudgmentJudgesLineWithoutJudge_LeavesBlocksUnchanged()
    {
        IBlock[] blocks = [JudgmentJudgesLine(new WText("JUDGMENT GIVEN ON", BoldStyling))];

        var result = Enricher.Enrich(blocks);

        result.ShouldBe(blocks);
    }

    [Fact]
    public void Enrich_JudgmentJudgesLineAfterTable_LeavesBlocksUnchanged()
    {
        IBlock[] blocks =
        [
            TableOf(["JUDGMENT"]),
            JudgmentJudgesLine(new WText("Lord Alpha, President", BoldStyling))
        ];

        var result = Enricher.Enrich(blocks);

        result.ShouldBe(blocks);
    }
}
