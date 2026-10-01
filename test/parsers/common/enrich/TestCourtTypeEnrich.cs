using System;
using System.Linq;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.common.enrich;

public class TestCourtTypeEnrich : ParserTestBase
{
    private static readonly CourtType CourtTypeEnricher = new();

    [InlineData("IN THE COURT OF PROTECTION", Courts.EwcopCourtCode)] // Single line
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                FAMILY DIVISION
                """, Courts.EwhcFamilyCourtCode)] // Two lines
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                QUEEN'S BENCH DIVISION
                THE ADMINISTRATIVE COURT
                """, Courts.EwhcQbdAdminCourtCode)] // Three lines
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                KING'S BENCH DIVISION
                THE ADMINISTRATIVE COURT
                """, Courts.EwhcKbdAdminCourtCode)] // KBD combos are generated from QBD
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                """, Courts.EwhcBpdCourtCode)] // BPD court fallback, no specific list
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                ADMIRALTY COURT
                """, Courts.EwhcBpdAdmiraltyCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION IN MANCHESTER
                APPEALS LIST
                """, Courts.EwhcBpdAppealsCourtCode)] // regional BPD
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                BUSINESS LIST
                """, Courts.EwhcBpdBusinessCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                BUSINESS LIST IN WALES
                """, Courts.EwhcBpdBusinessCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                LONDON CIRCUIT COMMERCIAL COURT
                """, Courts.EwhcBpdCommercialCircuitCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                COMMERCIAL COURT
                """, Courts.EwhcBpdCommercialCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                COMMERCIAL COURT
                FINANCIAL LIST
                """, Courts.EwhcBpdCommercialFinancialCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                COMPETITION LIST
                """, Courts.EwhcBpdCompetitionCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                INSOLVENCY AND COMPANIES LIST
                """, Courts.EwhcBpdInsolvencyAndCompaniesCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                INTELLECTUAL PROPERTY LIST
                """, Courts.EwhcBpdIntellectualPropertyCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                INTELLECTUAL PROPERTY ENTERPRISE COURT
                """, Courts.EwhcBpdIpecCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                PATENTS COURT
                """, Courts.EwhcBpdPatentsCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                PROPERTY, TRUSTS AND PROBATE LIST
                """, Courts.EwhcBpdPropertyTrustsProbateCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                PROPERTY TRUSTS AND PROBATE LIST IN BIRMINGHAM
                """, Courts.EwhcBpdPropertyTrustsProbateCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                REVENUE LIST
                """, Courts.EwhcBpdRevenueCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY DIVISION
                TECHNOLOGY AND CONSTRUCTION COURT
                """, Courts.EwhcBpdTccCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS & PROPERTY COURTS OF ENGLAND AND WALES
                TECHNOLOGY AND CONSTRUCTION COURT (KBD)
                """, Courts.EwhcKbdTccCourtCode)] // ampersand
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                QUEEN'S BENCH DIVISION
                ADMINISTRATIVE COURT
                PLANNING COURT
                """, Courts.EwhcQbdPlanningCourtCode)] // four lines
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                KING'S BENCH DIVISION
                BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES
                COMMERCIAL COURT
                """, Courts.EwhcKbdCommercialCourtCode)]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY COURTS
                OF ENGLAND AND WALES
                QUEEN'S BENCH DIVISION
                COMMERCIAL COURT
                """, Courts.EwhcQbdCommercialCourtCode)] // five lines
    [Theory]
    [InlineData("""
                IN THE HIGH COURT OF JUSTICE
                BUSINESS AND PROPERTY COURTS
                OF ENGLAND AND WALES
                COMMERCIAL COURT
                QUEEN'S BENCH DIVISION
                FINANCIAL LIST
                """, Courts.EwhcQbdCommercialFinancialCourtCode)] // six lines
    public void Enrich_GivenInputLines_ReturnsExpectedCourtCode(string input, string expectedCourtCode)
    {
        var inputLines = input.Split(Environment.NewLine);
        var result = CourtTypeEnricher.Enrich(inputLines.Select(l => TextLine(l))).ToArray();

        result.Length.ShouldBe(inputLines.Length);

        for (var i = 0; i < inputLines.Length; i++)
        {
            var courtTypeAnnotation = result[i].ShouldBeOfType<WLine>()
                                               .Contents.ShouldHaveSingleItem()
                                               .ShouldBeOfType<WCourtType>();
            courtTypeAnnotation.Text.ShouldBe(inputLines[i]);
            courtTypeAnnotation.Code.ShouldBe(expectedCourtCode);
        }
    }

    [Fact]
    public void Enrich_MatchNotAtStartOfDocument_LeavesPrecedingBlockUntouchedAndWrapsMatch()
    {
        var preamble = TextLine("Some unrelated preceding text.");
        var courtLine = TextLine("IN THE COURT OF PROTECTION");

        var result = CourtTypeEnricher.Enrich([preamble, courtLine]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(preamble);
        result[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WCourtType>()
                 .Code.ShouldBe(Courts.EwcopCourtCode);
    }

    [Fact]
    public void Enrich_NoMatchingCourtHeader_ReturnsOriginalBlocksUnchanged()
    {
        IBlock[] blocks =
        [
            TextLine("This is just some ordinary paragraph."),
            TextLine("So is this one.")
        ];

        var result = CourtTypeEnricher.Enrich(blocks);

        result.ShouldBeSameAs(blocks);
    }

    [Fact]
    public void Enrich_CourtHeaderInsideTableCell_WrapsLineInWCourtType()
    {
        var cell = CellWithOneLineOf("IN THE COURT OF PROTECTION");
        var row = RowOf([cell]);
        var table = TableOf([row]);

        var result = CourtTypeEnricher.Enrich([table]).Cast<WTable>().Single();

        var enrichedCell = result.TypedRows.Single().TypedCells.Single();
        var enrichedLine = enrichedCell.Contents.Cast<WLine>().Single();
        enrichedLine.Contents.ShouldHaveSingleItem().ShouldBeOfType<WCourtType>()
                    .Code.ShouldBe(Courts.EwcopCourtCode);
    }
}
