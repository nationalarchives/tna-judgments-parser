#nullable enable

using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

using NeutralCitationEnricher = UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT.NeutralCitationEnricher;

namespace test.parsers.ukut;

public class TestNeutralCitationEnricher : ParserTestBase
{
    private static readonly NeutralCitationEnricher Enricher = new();

    [Theory]
    [InlineData("Neutral Citation Number: [2023] UKUT 168 (LC)", "[2023] UKUT 168 (LC)")]
    [InlineData("UT Neutral Citation Number: [2023] UKUT 168 (IAC)", "[2023] UKUT 168 (IAC)")]
    [InlineData("NCN: [2023] UKUT 168 (AAC)", "[2023] UKUT 168 (AAC)")]
    [InlineData("NCN: [2023] UKFTT 4 (GRC)", "[2023] UKFTT 4 (GRC)")]
    [InlineData("NCN: [2025] UKFTT 12 (HESC)", "[2025] UKFTT 12 (HESC)")]
    [InlineData("Neutral Citation Number: [2013] UKAIT 12", "[2013] UKAIT 12")]
    public void Enrich_SingleRunWithLabel_MarksUpTheCitation(string text, string expectedCitation)
    {
        var line = StyledLine(text, BoldStyling);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[0].ShouldMatch<WText>(text[..^expectedCitation.Length], BoldStyling);
        contents[1].ShouldMatch<WNeutralCitation>(expectedCitation, BoldStyling);
    }

    [Theory]
    [InlineData("[2023] UKUT 168 (LC)")]
    [InlineData("[2023] UKFTT 4 (GRC)")]
    public void Enrich_SingleRunWithoutLabel_MarksUpTheCitation(string expectedCitation)
    {
        var line = StyledLine(expectedCitation, ItalicStyling);

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].Contents.ToArray().ShouldHaveSingleItem()
                 .ShouldMatch<WNeutralCitation>(expectedCitation, ItalicStyling);
    }

    [Fact]
    public void Enrich_CitationAtEndOfLine_MarksUpTheCitation()
    {
        var line = TextLine("Appeal No. UA-2021-000123 [2022] UKUT 1 (IAC)");

        var result = Enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);
        contents[1].ShouldBeOfType<WNeutralCitation>().Text.ShouldBe("[2022] UKUT 1 (IAC)");
    }
}
