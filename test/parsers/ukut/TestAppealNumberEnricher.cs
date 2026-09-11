#nullable enable

using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

using Xunit;

namespace test.parsers.ukut;

public class TestAppealNumberEnricher : ParserTestBase
{
    private static readonly AppealNumberEnricher Enricher = new();

    [Fact]
    public void Enrich_LineEndingInAppealNumber_MarksItUpAsWAppealNo()
    {
        var inputLine = TextLine("Appeal No. ", "CIS/111/2021");

        var result = Enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldBeSameAs(inputLine.Contents.ToArray()[0]);
        contents[1].ShouldBeOfType<WAppealNo>().Text.ShouldBe("CIS/111/2021");
    }

    [Fact]
    public void Enrich_TextEndingWithAppealNumber_MarksItUpAsWAppealNo()
    {
        var inputLine = TextLine("Appeal reference: CIS/222/2021");

        var result = Enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Appeal reference: ");
        contents[1].ShouldBeOfType<WAppealNo>().Text.ShouldBe("CIS/222/2021");
    }

    [Fact]
    public void Enrich_TextContainingAppealNumber_MarksItUpAsWAppealNo()
    {
        var inputLine = TextLine("Appeal number: CIS/333/2021 (some other information)");

        var result = Enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Appeal number: ");
        contents[1].ShouldBeOfType<WAppealNo>().Text.ShouldBe("CIS/333/2021");
        contents[2].ShouldBeOfType<WText>().Text.ShouldBe(" (some other information)");
    }

    [Fact]
    public void Enrich_TextContainingMultipleAppealNumbers_MarksUpEachWAppealNo()
    {
        var inputLine = TextLine("Appeal number: HU/12345/2026, HU/23456/2026, HU/34567/2026 ");

        var result = Enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(7);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Appeal number: ");
        contents[1].ShouldBeOfType<WAppealNo>().Text.ShouldBe("HU/12345/2026");
        contents[2].ShouldBeOfType<WText>().Text.ShouldBe(", ");
        contents[3].ShouldBeOfType<WAppealNo>().Text.ShouldBe("HU/23456/2026");
        contents[4].ShouldBeOfType<WText>().Text.ShouldBe(", ");
        contents[5].ShouldBeOfType<WAppealNo>().Text.ShouldBe("HU/34567/2026");
        contents[6].ShouldBeOfType<WText>().Text.ShouldBe(" ");
    }

    [Fact]
    public void Enrich_LineContainingMultipleAppealNumbers_MarksUpEachWAppealNo()
    {
        var inputLine = TextLine("Appeal number: ", "HU/12345/2026", ", ", "HU/23456/2026", ", ", "HU/34567/2026", " ");

        var result = Enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var inputLineContents = inputLine.Contents.ToArray();
        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(7);

        contents[0].ShouldBeSameAs(inputLineContents[0]);
        contents[1].ShouldBeOfType<WAppealNo>().Text.ShouldBe("HU/12345/2026");
        contents[2].ShouldBeSameAs(inputLineContents[2]);
        contents[3].ShouldBeOfType<WAppealNo>().Text.ShouldBe("HU/23456/2026");
        contents[4].ShouldBeSameAs(inputLineContents[4]);
        contents[5].ShouldBeOfType<WAppealNo>().Text.ShouldBe("HU/34567/2026");
        contents[6].ShouldBeSameAs(inputLineContents[6]);
    }

    [Theory]
    [InlineData("UT/2020/0043")]
    [InlineData("UA-2021-000019-T")]
    [InlineData("DC/12345/2026")]
    [InlineData("EA/2023/0132")]
    [InlineData("cis/444/2021")]
    public void Enrich_RealTribunalAppealNumberFormats_AreMarkedUp(string appealNumber)
    {
        var line = TextLine(appealNumber);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var appealNo = result[0].Contents.ShouldHaveSingleItem().ShouldBeOfType<WAppealNo>();
        appealNo.Text.ShouldBe(appealNumber);
    }

    [Fact]
    public void Enrich_AppealNumberSurroundedBySpaces_SplitsOffLeadingAndTrailingWhitespace()
    {
        var line = TextLine(" CIS/555/2021 ");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe(" ");
        contents[1].ShouldBeOfType<WAppealNo>().Text.ShouldBe("CIS/555/2021");
        contents[2].ShouldBeOfType<WText>().Text.ShouldBe(" ");
    }

    [Theory]
    [InlineData("TC12345")] // case number format, not an appeal number
    [InlineData("CIS")] // no separator/digits at all
    [InlineData("Something else")]
    public void Enrich_TextDoesNotMatchAppealNumberPattern_LineIsUnchanged(string text)
    {
        var line = TextLine(text);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(line);
    }

    [Fact]
    public void Enrich_EmptyLine_ReturnsLineUnchanged()
    {
        var line = TextLine();

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(line);
    }

    [Fact]
    public void Enrich_MultipleMatchingLines_EveryOneIsEnriched()
    {
        IBlock[] input =
        [
            TextLine("CIS/666/2021"),
            TextLine("Some other heading"),
            TextLine("UT/2020/0043")
        ];

        var result = Enricher.Enrich(input).Cast<WLine>().ToArray();

        result[0].Contents.ShouldHaveSingleItem().ShouldBeOfType<WAppealNo>().Text.ShouldBe("CIS/666/2021");
        result[1].ShouldBeSameAs(input[1]);
        result[2].Contents.ShouldHaveSingleItem().ShouldBeOfType<WAppealNo>().Text.ShouldBe("UT/2020/0043");
    }
}
