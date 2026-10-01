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
    public void InTheHighCourtOfJustice_MatchesExpectedText(string text)
    {
        CourtTypeHeaderRegexes.InTheHighCourtOfJustice().IsMatch(text).ShouldBeTrue();
    }

    [Theory]
    [InlineData("THE HIGH COURT OF JUSTICE")] // missing "IN"
    [InlineData("IN THE COURT OF JUSTICE")] // missing "HIGH"
    [InlineData("IN THE HIGH COURT OF JUSTICES")] // wrong plural
    [InlineData("IN THE HIGH COURT OF JUSTICE EXTRA")] // trailing content
    [InlineData("IN THE HIGH COURT OF JUSTICE ")] // trailing whitespace
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
}
