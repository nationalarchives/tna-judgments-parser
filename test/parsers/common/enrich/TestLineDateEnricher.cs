#nullable enable

using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parsers;

using Xunit;

namespace test.parsers.common.enrich;

public class TestLineDateEnricher : ParserTestBase
{
    public static TheoryData<string, IBlock> LinesWithoutRecognisableDate()
    {
        return new TheoryData<string, IBlock>
        {
            { "empty line", TextLine() },
            { "no date", TextLine("Judgment handed down") },
            { "date not at end of line", TextLine("5 May 2020 and more") },
            { "day zero", TextLine("Date: 0 May 2020") },
            { "last inline is not text", new WLine(LineTemplate, [Text("5 May 2020"), Tab()]) },
            { "second to last inline is not text", new WLine(LineTemplate, [Tab(), Text("5 May 2020 and more")]) },
            {
                "first of last three inlines is not text",
                new WLine(LineTemplate, [Tab(), Text("th"), Text(" May 2020")])
            },
            {
                "middle of last three inlines is not text",
                new WLine(LineTemplate, [Text("5"), Tab(), Text(" May 2020")])
            },
            { "last of last three inlines is not text", new WLine(LineTemplate, [Text("5"), Text("th"), Tab()]) },
            { "ordinal split without day number", TextLine("Date: x", "th", " May 2020") },
            { "ordinal split without month and year", TextLine("5", "th", " 2020") }
        };
    }

    public static TheoryData<string[]> WholeLineDateRuns()
    {
        return
        [
            new[] { "5 May", " 2020" },
            new[] { "5", "th", " May 2020" },
            new[] { "5", " May", " 2020" }
        ];
    }

    [Theory]
    [MemberData(nameof(LinesWithoutRecognisableDate))]
    public void Enrich_LineWithoutRecognisableDate_ReturnsOriginalLine(string scenario, IBlock block)
    {
        var line = block.ShouldBeOfType<WLine>();

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        result.ShouldBeSameAs(line, scenario);
    }

    [Fact]
    public void Enrich_WholeLineIsOneRun_WrapsItInDocDate()
    {
        var line = StyledLine("5 May 2020", BoldStyling);

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        var docDate = result.Contents.ShouldHaveSingleItem().ShouldBeOfType<WDocDate>();
        docDate.Date.ShouldBe("2020-05-05");

        docDate.Contents.ShouldHaveSingleItem().ShouldMatch<WText>("5 May 2020", BoldStyling);
    }

    [Theory]
    [MemberData(nameof(WholeLineDateRuns))]
    public void Enrich_WholeLineIsSeveralRuns_CombinesAllRunsIntoDocDate(string[] runs)
    {
        var line = TextLine(runs);
        var original = line.Contents.ToArray();

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        var docDate = result.Contents.ShouldHaveSingleItem().ShouldBeOfType<WDocDate>();
        docDate.Date.ShouldBe("2020-05-05");

        var dateContents = docDate.Contents.ToArray();
        dateContents.Length.ShouldBe(original.Length);
        for (var i = 0; i < original.Length; i++)
        {
            dateContents[i].ShouldBeSameAs(original[i]);
        }
    }

    [Fact]
    public void Enrich_Always_PassesNameAndPriorityToDocDate()
    {
        var line = TextLine("5 May 2020");

        var result = LineDateEnricher.Enrich(line, "hearing", 0);

        var docDate = result.Contents.ShouldHaveSingleItem().ShouldBeOfType<WDocDate>();
        docDate.Name.ShouldBe("hearing");
        docDate.Priority.ShouldBe(0);
    }

    [Fact]
    public void Enrich_DateAfterTextInSameRun_SplitsRunAndPreservesProperties()
    {
        var line = StyledLine("Date of decision: 5 May 2020", BoldStyling);

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        var lineContents = result.Contents.ToArray();
        lineContents.Length.ShouldBe(2);
        lineContents[0].ShouldMatch<WText>("Date of decision: ", BoldStyling);

        var docDate = lineContents[1].ShouldBeOfType<WDocDate>();
        docDate.Date.ShouldBe("2020-05-05");

        docDate.Contents.ShouldHaveSingleItem().ShouldMatch<WText>("5 May 2020", BoldStyling);
    }

    [Fact]
    public void Enrich_DateInLastRunAfterEarlierRuns_KeepsEarlierRunsUntouched()
    {
        var line = TextLine("Date: ", "5 May 2020");
        var label = line.Contents.First();

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        var lineContents = result.Contents.ToArray();
        lineContents.Length.ShouldBe(2);
        lineContents[0].ShouldBeSameAs(label);

        var docDate = lineContents[1].ShouldBeOfType<WDocDate>();
        docDate.Contents.ShouldHaveSingleItem().ShouldBeOfType<WText>().Text.ShouldBe("5 May 2020");
    }

    [Fact]
    public void Enrich_DateFollowedByTrailingSpaces_MarksUpDateOnly()
    {
        var line = TextLine("Date: 5 May 2020  ");

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        var docDate = result.Contents.OfType<WDocDate>().ShouldHaveSingleItem();
        docDate.Date.ShouldBe("2020-05-05");

        docDate.Contents.ShouldHaveSingleItem().ShouldBeOfType<WText>().Text.ShouldBe("5 May 2020");
    }

    [Theory]
    [InlineData("5 Feb 2020", "2020-02-05")]
    [InlineData("31 December 1999", "1999-12-31")]
    [InlineData("12 MARCH 2021", "2021-03-12")]
    public void Enrich_MonthNameVariants_ParsesDate(string text, string expected)
    {
        var line = TextLine($"Date: {text}");

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        var docDate = result.Contents.OfType<WDocDate>().ShouldHaveSingleItem();
        docDate.Date.ShouldBe(expected);

        docDate.Contents.ShouldHaveSingleItem().ShouldBeOfType<WText>().Text.ShouldBe(text);
    }

    [Fact]
    public void Enrich_DateSplitAcrossLastTwoRuns_CombinesBothRunsIntoDocDate()
    {
        var line = TextLine("Date: ", "5 May", " 2020");
        var original = line.Contents.ToArray();

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        var lineContents = result.Contents.ToArray();
        lineContents.Length.ShouldBe(2);

        var docDate = lineContents[1].ShouldBeOfType<WDocDate>();
        docDate.Date.ShouldBe("2020-05-05");

        var dateContents = docDate.Contents.ToArray();
        dateContents.Length.ShouldBe(2);
        dateContents[0].ShouldBeSameAs(original[1]);
        dateContents[1].ShouldBeSameAs(original[2]);
    }

    [Fact]
    public void Enrich_OrdinalDateSplitAcrossThreeRunsAfterText_SplitsFirstRunBeforeDay()
    {
        var line = TextLine("Date: 5", "th", " May 2020");
        var original = line.Contents.ToArray();

        var result = LineDateEnricher.Enrich(line, "decision", 1);

        var lineContents = result.Contents.ToArray();
        lineContents.Length.ShouldBe(2);
        lineContents[0].ShouldBeOfType<WText>().Text.ShouldBe("Date: ");

        var docDate = lineContents[1].ShouldBeOfType<WDocDate>();
        docDate.Date.ShouldBe("2020-05-05");

        var dateContents = docDate.Contents.ToArray();
        dateContents.Length.ShouldBe(3);
        dateContents[0].ShouldBeOfType<WText>().Text.ShouldBe("5");
        dateContents[1].ShouldBeSameAs(original[1]);
        dateContents[2].ShouldBeSameAs(original[2]);
    }
}
