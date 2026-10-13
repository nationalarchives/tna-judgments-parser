#nullable enable

using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.common.enrich;

public class TestJudge : ParserTestBase
{
    private static readonly Judge Enricher = new();

    [Theory]
    [InlineData("LORD JUSTICE ALPHA")]
    [InlineData("LADY JUSTICE BRAVO")]
    [InlineData("MR JUSTICE CHARLIE")]
    [InlineData("Mr Justice Delta")]
    [InlineData("MRS JUSTICE ECHO DBE")]
    [InlineData("THE HONOURABLE MR JUSTICE FOXTROT")]
    [InlineData("THE HONOURABLE MRS JUSTICE GOLF")]
    [InlineData("The Honourable Mr Justice Hotel India")]
    [InlineData("THE HON. MR JUSTICE JULIETT")]
    [InlineData("THE HON MR JUSTICE KILO LIMA CBE")]
    [InlineData("HIS HONOUR JUDGE MIKE")]
    [InlineData("HHJ NOVEMBER")]
    public void Enrich_JudgeOnLineAfterBefore_MarksUpJudge(string judgeName)
    {
        IBlock[] blocks =
        [
            TextLine("Before :"),
            StyledLine(judgeName, BoldStyling),
            TextLine("- - - - - - - - - - - - - - - - - - - - -")
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>(judgeName, BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_SeveralJudgesBetweenBeforeAndDashes_MarksUpEachJudge()
    {
        IBlock[] blocks =
        [
            TextLine("Before :"),
            StyledLine("LORD JUSTICE OSCAR PAPA", BoldStyling),
            StyledLine("LORD JUSTICE QUEBEC", BoldStyling),
            TextLine("and "),
            StyledLine("LORD JUSTICE ROMEO SIERRA", BoldStyling),
            TextLine("- - - - - - - - - - - - - - - - - - - - -")
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(6);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents
                 .ShouldHaveSingleItem().ShouldMatch<WJudge>("LORD JUSTICE OSCAR PAPA", BoldStyling);
        result[2].ShouldBeOfType<WLine>().Contents
                 .ShouldHaveSingleItem().ShouldMatch<WJudge>("LORD JUSTICE QUEBEC", BoldStyling);
        result[3].ShouldBeSameAs(blocks[3]);
        result[4].ShouldBeOfType<WLine>().Contents
                 .ShouldHaveSingleItem().ShouldMatch<WJudge>("LORD JUSTICE ROMEO SIERRA", BoldStyling);
        result[5].ShouldBeSameAs(blocks[5]);
    }

    [Fact]
    public void Enrich_LinesPrecedingBefore_AreLeftUnchanged()
    {
        IBlock[] blocks =
        [
            TextLine("Date: 15th April 2003"),
            TextLine("Before :"),
            StyledLine("LORD JUSTICE OSCAR PAPA", BoldStyling),
            TextLine("- - - - - - - - - - - - - - - - - - - - -")
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeSameAs(blocks[1]);
        result[2].ShouldBeOfType<WLine>().Contents
                 .ShouldHaveSingleItem().ShouldMatch<WJudge>("LORD JUSTICE OSCAR PAPA", BoldStyling);
        result[3].ShouldBeSameAs(blocks[3]);
    }

    [Fact]
    public void Enrich_BeforeAndJudgeInSeparateRunsOfSameLine_MarksUpJudge()
    {
        IInline[] lineContents =
        [
            new WText("Before: ", BoldStyling),
            new WText("HHJ Tango sitting as a s9 Deputy High Court Judge", UnderlineStyling)
        ];
        IBlock[] blocks = [new WLine(LineTemplate, lineContents)];

        var result = Enricher.Enrich(blocks).ToArray();

        var contents = result.ShouldHaveSingleItem().ShouldBeOfType<WLine>().Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldBeSameAs(lineContents[0]);
        contents[1].ShouldMatch<WJudge>("HHJ Tango sitting as a s9 Deputy High Court Judge", UnderlineStyling);
    }

    [Theory]
    [InlineData("Uniform Victor QC", "Sitting As A Deputy Judge Of The High Court")]
    [InlineData("MISS WHISKEY XRAY KC", "(Sitting as a Deputy High Court Judge)")]
    [InlineData("Mr Yankee Zulu, KC", "Sitting as a Deputy High Court Judge")]
    public void Enrich_CounselOnLineAfterBeforeFollowedBySittingAsLine_MarksUpCounselAsJudge(string judgeName,
        string sittingAs)
    {
        IBlock[] blocks =
        [
            TextLine("Before :"),
            StyledLine(judgeName, BoldStyling),
            TextLine(sittingAs),
            TextLine("- - - - - - - - - - - - - - - - - - - - -")
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(4);
        result[0].ShouldBeSameAs(blocks[0]);
        result[1].ShouldBeOfType<WLine>().Contents.ShouldHaveSingleItem().ShouldMatch<WJudge>(judgeName, BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
        result[3].ShouldBeSameAs(blocks[3]);
    }

    [Theory]
    [InlineData("JOHN DOE KC", " SITTING AS A DEPUTY HIGH COURT JUDGE")]
    [InlineData("Richard Roe QC", " (sitting as a Deputy Judge of the High Court)")]
    [InlineData("Mr Joe Bloggs KC", " sitting as a Deputy Judge of the High Court")]
    public void Enrich_CounselAndSittingAsInSameRun_MarksUpOnlyCounselAsJudge(string judgeName, string sittingAs)
    {
        IBlock[] blocks =
        [
            TextLine("Before :"),
            StyledLine(judgeName + sittingAs, BoldStyling),
            TextLine("- - - - - - - - - - - - - - - - - - - - -")
        ];

        var result = Enricher.Enrich(blocks).ToArray();

        result.Length.ShouldBe(3);
        result[0].ShouldBeSameAs(blocks[0]);
        var contents = result[1].ShouldBeOfType<WLine>().Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WJudge>(judgeName, BoldStyling);
        contents[1].ShouldMatch<WText>(sittingAs, BoldStyling);
        result[2].ShouldBeSameAs(blocks[2]);
    }

    [Fact]
    public void Enrich_NoBeforeLine_LeavesBlocksUnchanged()
    {
        IBlock[] blocks =
        [
            StyledLine("MR JUSTICE CHARLIE", BoldStyling),
            TextLine("- - - - - - - - - - - - - - - - - - - - -")
        ];

        var result = Enricher.Enrich(blocks);

        result.ShouldBe(blocks);
    }

    [Theory]
    [InlineData("Between :")]
    [InlineData("- - - - - - - - - - - - - - - - - - - - -")]
    public void Enrich_BeforeFollowedByNonJudge_LeavesBlocksUnchanged(string nonJudge)
    {
        IBlock[] blocks =
        [
            TextLine("Before :"),
            TextLine(nonJudge)
        ];

        var result = Enricher.Enrich(blocks);

        result.ShouldBe(blocks);
    }
}
