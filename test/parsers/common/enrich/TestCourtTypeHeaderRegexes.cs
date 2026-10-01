using Shouldly;

using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.common.enrich;

public class TestCourtTypeHeaderRegexes
{
    [Theory]
    [InlineData("IN THE HIGH COURT OF JUSTICE")]
    [InlineData("in the high court of justice")]
    [InlineData("IN THE HIGH COURTS OF JUSTICE")] // S in EWHC/Ch/2009/2692
    [InlineData("THE HIGH COURT OF JUSTICE")] // missing "IN"
    public void InTheHighCourtOfJustice_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.InTheHighCourtOfJustice().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("IN THE COURT OF JUSTICE")] // missing "HIGH"
    [InlineData("IN THE HIGH COURT OF JUSTICES")] // wrong plural
    [InlineData("IN THE HIGH COURT OF JUSTICE EXTRA")] // trailing content
    public void InTheHighCourtOfJustice_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.InTheHighCourtOfJustice().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("BUSINESS AND PROPERTY COURT")]
    [InlineData("BUSINESS AND PROPERTY COURTS")]
    [InlineData("BUSINESS & PROPERTY COURTS")]
    [InlineData("IN BUSINESS AND PROPERTY COURTS")]
    [InlineData("THE BUSINESS AND PROPERTY COURTS")]
    [InlineData("IN THE BUSINESS AND PROPERTY COURTS")]
    public void BusinessAndPropertyCourts_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.BusinessAndPropertyCourts().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES")] // too much trailing content
    [InlineData("BUSINESS PROPERTY COURTS")] // missing "AND"/"&"
    [InlineData("PROPERTY COURTS")] // missing "BUSINESS"
    public void BusinessAndPropertyCourts_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.BusinessAndPropertyCourts().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES")]
    [InlineData("BUSINESS & PROPERTY COURT OF ENGLAND & WALES")]
    [InlineData("IN THE BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES")]
    [InlineData("BUSINESS AND PROPERTY COURTS OF ENGLAND AN WALES")] // missing D in EWHC/QB/2017/2921
    public void BusinessAndPropertyCourtsOfEnglandAndWales_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("BUSINESS AND PROPERTY COURTS")] // missing "OF ENGLAND AND WALES"
    [InlineData("BUSINESS AND PROPERTY COURTS OF WALES")] // missing "ENGLAND"
    [InlineData("BUSINESS AND PROPERTY COURTS OF ENGLAND AND SCOTLAND")] // wrong country
    public void BusinessAndPropertyCourtsOfEnglandAndWales_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.BusinessAndPropertyCourtsOfEnglandAndWales().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("OF ENGLAND AND WALES")]
    [InlineData("OF ENGLAND & WALES")]
    [InlineData("of england and wales")]
    public void OfEnglandAndWales_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.OfEnglandAndWales().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("ENGLAND AND WALES")] // missing "OF"
    [InlineData("OF ENGLAND AND WALES EXTRA")] // trailing content
    public void OfEnglandAndWales_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.OfEnglandAndWales().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("QUEEN'S BENCH DIVISION")]
    [InlineData("QUEENS BENCH DIVISION")] // no apostrophe in EWHC/Admin/2009/573
    [InlineData("QUEEN’S BENCH DIVISION")] // curly apostrophe
    [InlineData("queen's bench division")]
    public void QueensBenchDivision_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.QueensBenchDivision().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("KING'S BENCH DIVISION")] // King's, not Queen's (this is handled outside the regexes)
    [InlineData("QUEEN'S BENCH DIVISION OF ENGLAND")] // trailing content
    [InlineData("BENCH DIVISION")] // missing "QUEEN'S"
    public void QueensBenchDivision_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.QueensBenchDivision().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("COMMERCIAL COURT")]
    [InlineData("commercial court")]
    [InlineData("COMMERCIAL COURT IN MANCHESTER")] // regional venue
    [InlineData("COMMERCIAL COURT, ROLLS BUILDING, LONDON")] // comma-separated venue
    public void CommercialCourt_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.CommercialCourt().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("COMMERCIAL COURT (QBD)")] // trailing content
    [InlineData("COMMERCIAL COURTS")] // wrong plural
    [InlineData("COURT")] // missing "COMMERCIAL"
    public void CommercialCourt_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.CommercialCourt().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("BUSINESS AND PROPERTY DIVISION")]
    [InlineData("BUSINESS & PROPERTY DIVISION")]
    [InlineData("business and property division")]
    [InlineData("BUSINESS AND PROPERTY DIVISION IN MANCHESTER")] // regional
    [InlineData("BUSINESS & PROPERTY DIVISION IN LEEDS")] // regional, ampersand
    [InlineData("business and property division in bristol")] // regional, lowercase
    [InlineData("BUSINESS AND PROPERTY DIVISION IN NEWCASTLE UPON TYNE")] // regional, multi-word city
    public void BusinessAndPropertyDivision_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.BusinessAndPropertyDivision().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("BUSINESS AND PROPERTY COURTS")] // wrong suffix
    [InlineData("BUSINESS PROPERTY DIVISION")] // missing "AND"/"&"
    [InlineData("BUSINESS AND PROPERTY DIVISION EXTRA")] // trailing content
    [InlineData("PROPERTY DIVISION")] // missing "BUSINESS AND"
    [InlineData("BUSINESS AND PROPERTY DIVISION IN")] // missing the region name
    [InlineData("BUSINESS AND PROPERTY DIVISION MANCHESTER")] // missing "IN"
    public void BusinessAndPropertyDivision_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.BusinessAndPropertyDivision().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("FINANCIAL LIST")]
    [InlineData("financial list")]
    [InlineData("FINANCIAL LIST IN LEEDS")] // regional venue
    public void FinancialList_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.FinancialList().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("FINANCIAL LIST (ChD)")] // trailing content
    [InlineData("FINANCIAL LISTS")] // wrong plural
    [InlineData("LIST")] // missing "FINANCIAL"
    public void FinancialList_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.FinancialList().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("ADMIRALTY COURT")]
    [InlineData("admiralty court")]
    [InlineData("ADMIRALTY COURT IN LIVERPOOL")] // regional venue
    public void AdmiraltyCourt_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.AdmiraltyCourt().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("ADMIRALTY COURT (QBD)")] // trailing content
    [InlineData("ADMIRALTY COURTS")] // wrong plural
    [InlineData("COURT")] // missing "ADMIRALTY"
    public void AdmiraltyCourt_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.AdmiraltyCourt().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("APPEALS LIST")]
    [InlineData("appeals list")]
    [InlineData("APPEALS LIST, ROLLS BUILDING, LONDON")] // official example, see practice note link above
    public void ChanceryAppeals_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.AppealsList().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("APPEALS LIST (ChD)")] // trailing content
    [InlineData("APPEAL LIST")] // wrong singular
    [InlineData("APPEALS")] // missing "CHANCERY"
    public void ChanceryAppeals_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.AppealsList().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("BUSINESS LIST")]
    [InlineData("business list")]
    [InlineData("BUSINESS LIST IN WALES")] // official example
    public void BusinessList_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.BusinessList().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("BUSINESS LIST (ChD)")] // trailing content
    [InlineData("BUSINESS LISTS")] // wrong plural
    [InlineData("LIST")] // missing "BUSINESS"
    public void BusinessList_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.BusinessList().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("LONDON CIRCUIT COMMERCIAL COURT")]
    [InlineData("london circuit commercial court")]
    [InlineData("LONDON CIRCUIT COMMERCIAL COURT, ROLLS BUILDING")] // comma-separated venue
    public void LondonCircuitCommercialCourt_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.LondonCircuitCommercialCourt().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("LONDON CIRCUIT COMMERCIAL COURT (QBD)")] // trailing content
    [InlineData("LONDON MERCANTILE COURT")] // different court
    [InlineData("CIRCUIT COMMERCIAL COURT")] // missing "LONDON"
    public void LondonCircuitCommercialCourt_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.LondonCircuitCommercialCourt().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("COMPETITION LIST")]
    [InlineData("competition list")]
    [InlineData("COMPETITION LIST IN MANCHESTER")] // regional venue
    public void CompetitionList_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.CompetitionList().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("COMPETITION LIST (ChD)")] // trailing content
    [InlineData("COMPETITION LISTS")] // wrong plural
    [InlineData("LIST")] // missing "COMPETITION"
    public void CompetitionList_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.CompetitionList().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("INSOLVENCY AND COMPANIES LIST")]
    [InlineData("insolvency and companies list")]
    [InlineData("INSOLVENCY AND COMPANIES LIST IN LEEDS")] // regional venue
    public void InsolvencyAndCompaniesList_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.InsolvencyAndCompaniesList().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("INSOLVENCY AND COMPANIES LIST (ChD)")] // trailing content
    [InlineData("INSOLVENCY AND COMPANIES COURT LIST")] // different (older) list name
    [InlineData("COMPANIES LIST")] // missing "INSOLVENCY AND"
    public void InsolvencyAndCompaniesList_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.InsolvencyAndCompaniesList().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("INTELLECTUAL PROPERTY LIST")]
    [InlineData("intellectual property list")]
    [InlineData("INTELLECTUAL PROPERTY LIST IN BIRMINGHAM")] // regional venue
    public void IntellectualPropertyList_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.IntellectualPropertyList().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("INTELLECTUAL PROPERTY LIST (ChD)")] // trailing content
    [InlineData("INTELLECTUAL PROPERTY ENTERPRISE COURT")] // different court
    [InlineData("PROPERTY LIST")] // missing "INTELLECTUAL"
    public void IntellectualPropertyList_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.IntellectualPropertyList().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("INTELLECTUAL PROPERTY ENTERPRISE COURT")]
    [InlineData("intellectual property enterprise court")]
    [InlineData("INTELLECTUAL PROPERTY ENTERPRISE COURT IN MANCHESTER")] // regional venue
    public void IntellectualPropertyEnterpriseCourt_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.IntellectualPropertyEnterpriseCourt().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("INTELLECTUAL PROPERTY ENTERPRISE COURT (IPEC)")] // trailing content
    [InlineData("INTELLECTUAL PROPERTY LIST")] // different court
    [InlineData("ENTERPRISE COURT")] // missing "INTELLECTUAL PROPERTY"
    public void IntellectualPropertyEnterpriseCourt_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.IntellectualPropertyEnterpriseCourt().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("PATENTS COURT")]
    [InlineData("patents court")]
    [InlineData("PATENTS COURT IN BRISTOL")] // regional venue
    public void PatentsCourt_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.PatentsCourt().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("PATENTS COURT (Ch)")] // trailing content: BPD variant is exact, unlike the legacy prefix-tolerant one
    [InlineData("PATENTS COURTS")] // wrong plural
    [InlineData("COURT")] // missing "PATENTS"
    public void PatentsCourt_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.PatentsCourt().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("PROPERTY, TRUSTS AND PROBATE LIST")]
    [InlineData("PROPERTY, TRUSTS & PROBATE LIST")]
    [InlineData("property, trusts and probate list")]
    [InlineData("PROPERTY TRUSTS AND PROBATE LIST IN BIRMINGHAM")] // official example: no comma, regional venue
    public void PropertyTrustsAndProbateList_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.PropertyTrustsAndProbateList().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("PROPERTY, TRUSTS AND PROBATE LIST (ChD)")] // trailing content
    [InlineData("PROPERTY, TRUSTS AND PROBATE")] // missing "LIST"
    [InlineData("TRUSTS AND PROBATE LIST")] // missing "PROPERTY,"
    public void PropertyTrustsAndProbateList_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.PropertyTrustsAndProbateList().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("REVENUE LIST")]
    [InlineData("revenue list")]
    [InlineData("REVENUE LIST IN CARDIFF")] // regional venue
    public void RevenueList_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.RevenueList().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("REVENUE LIST (ChD)")] // trailing content
    [InlineData("REVENUE LISTS")] // wrong plural
    [InlineData("LIST")] // missing "REVENUE"
    public void RevenueList_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.RevenueList().IsMatch(text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("TECHNOLOGY AND CONSTRUCTION COURT")]
    [InlineData("TECHNOLOGY & CONSTRUCTION COURT")]
    [InlineData("technology and construction court")]
    [InlineData("TECHNOLOGY AND CONSTRUCTION COURT IN MANCHESTER")] // regional venue
    public void TechnologyAndConstructionCourt_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.TechnologyAndConstructionCourt().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("TECHNOLOGY AND CONSTRUCTION COURT (QBD)")] // trailing content
    [InlineData("TECHNOLOGY CONSTRUCTION COURT")] // missing "AND"/"&"
    [InlineData("CONSTRUCTION COURT")] // missing "TECHNOLOGY AND"
    public void TechnologyAndConstructionCourt_DoesNotMatchUnexpectedText(string text)
    {
        CourtTypeHeaderRegexes.TechnologyAndConstructionCourt().IsMatch(text).ShouldBeFalse();
    }
}
