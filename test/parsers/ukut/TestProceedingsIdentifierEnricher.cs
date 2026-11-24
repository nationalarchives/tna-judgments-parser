#nullable enable

using System.Linq;

using Moq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

using Xunit;

namespace test.parsers.ukut;

public class TestProceedingsIdentifierEnricher : ParserTestBase
{
    private readonly Mock<IProceedingsIdentifierTypeTracker> typeTracker = new();
    private readonly ProceedingsIdentifierEnricher enricher;

    public TestProceedingsIdentifierEnricher()
    {
        enricher = new ProceedingsIdentifierEnricher(typeTracker.Object);
    }

    [Fact]
    public void Enrich_LineEndingInProceedingsIdentifier_MarksItUpAsProceedingsIdentifier()
    {
        typeTracker.Setup(t => t.GetCurrentType()).Returns("Appeal No");
        var inputLine = TextLine("Appeal No. ", "CIS/111/2021");

        var result = enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var inputLineContents = inputLine.Contents.ToArray();
        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(2);

        contents[0].ShouldBeSameAs(inputLineContents[0]);
        var proceedingsIdentifier = contents[1].ShouldBeOfType<WProceedingsIdentifier>();
        proceedingsIdentifier.Text.ShouldBe("CIS/111/2021");
        proceedingsIdentifier.Type.ShouldBe("Appeal No");
    }

    [Fact]
    public void Enrich_TextContainingProceedingsIdentifier_MarksItUpAsProceedingsIdentifier()
    {
        typeTracker.Setup(t => t.GetCurrentType()).Returns("Case Reference");
        var inputLine = TextLine("Case Reference: FT/D/2026/0123 (some other information)");

        var result = enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Case Reference: ");
        var proceedingsIdentifier = contents[1].ShouldBeOfType<WProceedingsIdentifier>();
        proceedingsIdentifier.Text.ShouldBe("FT/D/2026/0123");
        proceedingsIdentifier.Type.ShouldBe("Case Reference");
        contents[2].ShouldBeOfType<WText>().Text.ShouldBe(" (some other information)");
    }

    [Fact]
    public void Enrich_TextContainingMultipleProceedingsIdentifiers_MarksUpEachProceedingsIdentifier()
    {
        typeTracker.Setup(t => t.GetCurrentType()).Returns("Appeal No");
        var inputLine = TextLine("Appeal Nos: 2026-01234.EA, 2026-02345.EA, 2026-03456.EA ");

        var result = enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(7);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe("Appeal Nos: ");
        var proceedingsIdentifier1 = contents[1].ShouldBeOfType<WProceedingsIdentifier>();
        proceedingsIdentifier1.Text.ShouldBe("2026-01234.EA");
        proceedingsIdentifier1.Type.ShouldBe("Appeal No");
        contents[2].ShouldBeOfType<WText>().Text.ShouldBe(", ");
        var proceedingsIdentifier2 = contents[3].ShouldBeOfType<WProceedingsIdentifier>();
        proceedingsIdentifier2.Text.ShouldBe("2026-02345.EA");
        proceedingsIdentifier2.Type.ShouldBe("Appeal No");
        contents[4].ShouldBeOfType<WText>().Text.ShouldBe(", ");
        var proceedingsIdentifier3 = contents[5].ShouldBeOfType<WProceedingsIdentifier>();
        proceedingsIdentifier3.Text.ShouldBe("2026-03456.EA");
        proceedingsIdentifier3.Type.ShouldBe("Appeal No");
        contents[6].ShouldBeOfType<WText>().Text.ShouldBe(" ");
    }

    [Theory]
    [InlineData(", ")]
    [InlineData(" & ")]
    public void Enrich_LineContainingMultipleProceedingsIdentifiers_MarksUpEachProceedingsIdentifier(string separator)
    {
        typeTracker.Setup(t => t.GetCurrentType()).Returns("Appeal number");
        var inputLine = TextLine("Appeal number: ", "[2009] 1234 PT", separator, "[2009] 2345 PT", separator,
            "[2009] 3456 PT", " ");

        var result = enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        var inputLineContents = inputLine.Contents.ToArray();
        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(7);

        contents[0].ShouldBeSameAs(inputLineContents[0]);
        var proceedingsIdentifier1 = contents[1].ShouldBeOfType<WProceedingsIdentifier>();
        proceedingsIdentifier1.Text.ShouldBe("[2009] 1234 PT");
        proceedingsIdentifier1.Type.ShouldBe("Appeal number");
        contents[2].ShouldBeSameAs(inputLineContents[2]);
        var proceedingsIdentifier2 = contents[3].ShouldBeOfType<WProceedingsIdentifier>();
        proceedingsIdentifier2.Text.ShouldBe("[2009] 2345 PT");
        proceedingsIdentifier2.Type.ShouldBe("Appeal number");
        contents[4].ShouldBeSameAs(inputLineContents[4]);
        var proceedingsIdentifier3 = contents[5].ShouldBeOfType<WProceedingsIdentifier>();
        proceedingsIdentifier3.Text.ShouldBe("[2009] 3456 PT");
        proceedingsIdentifier3.Type.ShouldBe("Appeal number");
        contents[6].ShouldBeSameAs(inputLineContents[6]);
    }

    [Theory]
    [InlineData("UT/2020/0043")]
    [InlineData("UA-2021-000019-T")]
    [InlineData("UA-2026-000123/45-HB")]
    [InlineData("DC/12345/2026")]
    [InlineData("EA/2023/0132")]
    [InlineData("cis/444/2021")]
    [InlineData("EA-2022-001495-NK")] // EAT
    [InlineData("EA-2022-000091-JOJ")] // EAT
    [InlineData("UA-2026-000079-PIP")] // UT Administrative Appeals Chamber
    [InlineData("FT/D/2026/0398")] // FTT GRC - Transport
    [InlineData("FT/EA/2024/0220")] // FTT GRC - Information rights
    [InlineData("FT/EA/2025/0478/GDPR")] // FTT GRC - Information rights
    [InlineData("FT/IMS/2025/0013")] // FTT GRC - Immigration services
    [InlineData("FT/PEN/2026/0112")] // FTT GRC - Pensions
    [InlineData("FT/CA/2025/0026")] // FTT GRC - Charity
    [InlineData("LON/00BK/LSC/2023/0354")] // UT Lands Chamber
    [InlineData("2026-01951.EA")] // FTT Care Standards
    [InlineData("2025-01627.ISO-W")] // FTT Care Standards
    [InlineData("2026-00123.EY-SUS")] // FTT Care Standards
    [InlineData("2025-01701.PHL")] // FTT Primary Health Lists
    [InlineData("[2010]1808.SW")] // FTT Care Standards (older format)
    [InlineData("[2009] 1658 PT")] // FTT Care Standards (older format)
    [InlineData("[2002] 7.PC")] // FTT Care Standards (older format)
    [InlineData("[2018] 3498.EY-SUS")] // FTT Care Standards (older format)
    [InlineData("TC 10013")] // FTT Tax Chamber - case number
    [InlineData("TC09969")] // FTT Tax Chamber - case number
    [InlineData("REF/2024/0045 & 0046")]
    [InlineData("REF 2026/0234")] // FTT Property Chamber - Land Registration
    [InlineData("LC-2025-123")]
    [InlineData("UT 2026-000034")]
    public void Enrich_RealTribunalProceedingsIdentifierFormats_AreMarkedUp(string proceedingsIdentifier)
    {
        var line = TextLine(proceedingsIdentifier);

        var result = enricher.Enrich([line]).Cast<WLine>().ToArray();

        var enrichedIdentifier = result[0].Contents.ShouldHaveSingleItem().ShouldBeOfType<WProceedingsIdentifier>();
        enrichedIdentifier.Text.ShouldBe(proceedingsIdentifier);
    }

    [Theory]
    [InlineData("UT/2020/0043")] //SlashGroupRegex
    [InlineData("UA-2021-000019-T")] //DashGroupRegex
    [InlineData("2026-01951.EA")] // DigitDottedRegex
    [InlineData("[2010]1808.SW")] // OldCareStandardsRegex
    [InlineData("TC 1234")] //UkfttTaxChamberCaseNumberRegex
    public void Enrich_ProceedingsIdentifierSurroundedBySpaces_SplitsOffLeadingAndTrailingWhitespace(
        string proceedingsIdentifier)
    {
        var line = TextLine($" {proceedingsIdentifier} ");

        var result = enricher.Enrich([line]).Cast<WLine>().ToArray();

        var contents = result[0].Contents.ToArray();
        contents.Length.ShouldBe(3);

        contents[0].ShouldBeOfType<WText>().Text.ShouldBe(" ");
        contents[1].ShouldBeOfType<WProceedingsIdentifier>().Text.ShouldBe(proceedingsIdentifier);
        contents[2].ShouldBeOfType<WText>().Text.ShouldBe(" ");
    }

    [Theory]
    [InlineData("CIS")] // no separator/digits at all
    [InlineData("Something else")]
    [InlineData("[2025] UKFTT 01448 (PC)")] // ncn
    [InlineData("Neutral Citation: [2026] UKUT 00123 (TCC)")] // ncn
    [InlineData("Neutral Citation Number: [2026] UKFTT 00123 (HESC)")] // ncn
    [InlineData("[2017] 1 WLR 1234")] // case citation
    [InlineData("[2014] 2 P&CR 5")] // case citation
    [InlineData("[1996] AC 155 (HL)")] // case citation
    [InlineData("IN-THE-UPPER-TRIBUNAL")] // dash-separated letters only, no digits
    [InlineData("10 May 2022")] // date, not a case number
    [InlineData("Heard 10 May 2022 at")] // date, not a case number
    [InlineData("dated 23 May 2023")] // date, not a case number
    [InlineData("COURTS AND ENFORCEMENT ACT 2007")] // legislation title, not a case number
    [InlineData("Finance Act 2020")] // legislation title, not a case number
    [InlineData("within the meaning of s306(1) FA 2004")] // legislation citation, not a case number
    public void Enrich_TextDoesNotMatchProceedingsIdentifierPattern_LineIsUnchanged(string text)
    {
        var line = TextLine(text);

        var result = enricher.Enrich([line]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(line);
    }

    [Fact]
    public void Enrich_EmptyLine_ReturnsLineUnchanged()
    {
        var line = TextLine();

        var result = enricher.Enrich([line]).Cast<WLine>().ToArray();

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

        var result = enricher.Enrich(input).Cast<WLine>().ToArray();

        result[0].Contents.ShouldHaveSingleItem().ShouldBeOfType<WProceedingsIdentifier>().Text
                 .ShouldBe("CIS/666/2021");
        result[1].ShouldBeSameAs(input[1]);
        result[2].Contents.ShouldHaveSingleItem().ShouldBeOfType<WProceedingsIdentifier>().Text
                 .ShouldBe("UT/2020/0043");
    }

    [Fact]
    public void Enrich_ProceedingsIdentifier_SetsTypeFromTypeTracker()
    {
        typeTracker.Setup(t => t.GetCurrentType()).Returns("Appeal No");
        var inputLine = TextLine("Appeal No. ", "CIS/111/2021");

        var result = enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        result[0].Contents.Last().ShouldBeOfType<WProceedingsIdentifier>().Type.ShouldBe("Appeal No");
    }

    [Fact]
    public void Enrich_TypeTrackerHasNoType_ProceedingsIdentifierTypeIsNull()
    {
        typeTracker.Setup(t => t.GetCurrentType()).Returns((string?)null);
        var inputLine = TextLine("CIS/111/2021");

        var result = enricher.Enrich([inputLine]).Cast<WLine>().ToArray();

        result[0].Contents.ShouldHaveSingleItem().ShouldBeOfType<WProceedingsIdentifier>().Type.ShouldBeNull();
    }

    [Fact]
    public void Enrich_MultipleProceedingsIdentifiersInOneText_AsksTypeTrackerForEachOne()
    {
        typeTracker.Setup(t => t.GetCurrentType()).Returns("Appeal No");
        var inputLine = TextLine("2026-01234.EA, 2026-02345.EA, 2026-03456.EA");

        enricher.Enrich([inputLine]).ToArray();

        typeTracker.Verify(t => t.GetCurrentType(), Times.Exactly(3));
    }

    [Fact]
    public void Enrich_Blocks_ResetsTypeTrackerOnceBeforeEnrichingAnyLine()
    {
        enricher.Enrich([TextLine("CIS/111/2021"), TextLine("CIS/222/2021")]);

        typeTracker.Invocations.Select(i => i.Method.Name)
                   .Where(name => name is nameof(IProceedingsIdentifierTypeTracker.Reset)
                                      or nameof(IProceedingsIdentifierTypeTracker.StartLine))
                   .ShouldBe([
                       nameof(IProceedingsIdentifierTypeTracker.Reset),
                       nameof(IProceedingsIdentifierTypeTracker.StartLine),
                       nameof(IProceedingsIdentifierTypeTracker.StartLine)
                   ]);
    }

    [Fact]
    public void Enrich_Line_RecordsTextBeforeAskingForType()
    {
        enricher.Enrich([TextLine("Appeal No. ", "CIS/111/2021", " (trailing)")]);

        typeTracker.Invocations.Select(i => i.Method.Name)
                   .Where(name => name is nameof(IProceedingsIdentifierTypeTracker.Record)
                                      or nameof(IProceedingsIdentifierTypeTracker.GetCurrentType))
                   .ShouldBe([
                       nameof(IProceedingsIdentifierTypeTracker.Record),
                       nameof(IProceedingsIdentifierTypeTracker.GetCurrentType),
                       nameof(IProceedingsIdentifierTypeTracker.Record)
                   ]);
    }

    [Fact]
    public void Enrich_TextAroundIdentifierInOneRun_RecordsEachPieceOfText()
    {
        var inputLine = TextLine("Case Reference: FT/D/2026/0123 (some other information)");

        enricher.Enrich([inputLine]);

        typeTracker.Verify(t => t.Record(It.Is<IInline>(i => ((WText)i).Text == "Case Reference: ")), Times.Once);
        typeTracker.Verify(t => t.Record(It.Is<IInline>(i => ((WText)i).Text == " (some other information)")),
            Times.Once);
    }

    [Fact]
    public void Enrich_LineContainingTab_RecordsTheTab()
    {
        var tab = Tab();
        IInline[] lineContents = [Text("Appeal No:"), tab, Text("UA-2026-000123-T")];
        var inputLine = new WLine(LineTemplate, lineContents);

        enricher.Enrich([inputLine]);

        typeTracker.Verify(t => t.Record(tab), Times.Once);
    }

    [Fact]
    public void Enrich_TextWithoutProceedingsIdentifier_RecordsTheOriginalInline()
    {
        var text = Text("Some other heading");

        enricher.Enrich([new WLine(LineTemplate, [text])]);

        typeTracker.Verify(t => t.Record(text), Times.Once);
    }
}
