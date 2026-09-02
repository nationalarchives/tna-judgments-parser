using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.common.enrich;

public partial class TestPartyEnricher
{
    [Theory]
    [InlineData("(1st DEFENDANT)", PartyRole.Defendant)]
    [InlineData("(2nd DEFENDANT)", PartyRole.Defendant)]
    [InlineData("(3rd DEFENDANT)", PartyRole.Defendant)]
    [InlineData("(APPELLANT)", PartyRole.Appellant)]
    [InlineData("(APPELLANTS)", PartyRole.Appellant)]
    [InlineData("(CLAIMANTS)", PartyRole.Claimant)]
    [InlineData("(Claimant)", PartyRole.Claimant)]
    [InlineData("(DEFENDANTS)", PartyRole.Defendant)]
    [InlineData("(Defendant)", PartyRole.Defendant)]
    [InlineData("(FIRST DEFENDANT)", PartyRole.Defendant)]
    [InlineData("(INTERESTED PARTIES)", PartyRole.InterestedParty)]
    [InlineData("(INTERESTED PARTY)", PartyRole.InterestedParty)]
    [InlineData("(RESPONDENT)", PartyRole.Respondent)]
    [InlineData("(RESPONDENTS)", PartyRole.Respondent)]
    [InlineData("(SECOND DEFENDANT)", PartyRole.Defendant)]
    [InlineData("1st Appellant", PartyRole.Appellant)]
    [InlineData("1st Applicant", PartyRole.Applicant)]
    [InlineData("1st Respondent", PartyRole.Respondent)]
    [InlineData("2nd Applicant", PartyRole.Applicant)]
    [InlineData("2nd Respondent", PartyRole.Respondent)]
    [InlineData("3rd Respondent", PartyRole.Respondent)]
    [InlineData("Additional Claimant", PartyRole.Claimant)]
    [InlineData("Appellant / Claimant", PartyRole.Appellant)]
    [InlineData("Appellant / Third Defendant", PartyRole.Appellant)]
    [InlineData("Appellant", PartyRole.Appellant)]
    [InlineData("Appellant/ Defendant", PartyRole.Appellant)]
    [InlineData("Appellant/Respondent", PartyRole.Respondent)]
    [InlineData("Appellant/Appellant", PartyRole.Appellant)]
    [InlineData("Appellant/Applicant", PartyRole.Appellant)]
    [InlineData("Appellant/Claimant", PartyRole.Appellant)]
    [InlineData("Appellant/Defendant", PartyRole.Appellant)]
    [InlineData("Appellant/First Defendant", PartyRole.Appellant)]
    [InlineData("Appellants / Defendants", PartyRole.Appellant)]
    [InlineData("Appellants", PartyRole.Appellant)]
    [InlineData("Appellants/ Claimants", PartyRole.Appellant)]
    [InlineData("Appellants/Claimants", PartyRole.Appellant)]
    [InlineData("Applicant", PartyRole.Applicant)]
    [InlineData("Applicant/ Claimant", PartyRole.Applicant)]
    [InlineData("Applicant/Appellant", PartyRole.Appellant)]
    [InlineData("Applicants", PartyRole.Applicant)]
    [InlineData("Applicants/Claimants", PartyRole.Applicant)]
    [InlineData("Applicants/Defendants", PartyRole.Defendant)]
    [InlineData("Claimant / Appellant", PartyRole.Appellant)]
    [InlineData("Claimant / Defendant to Counterclaim", PartyRole.Claimant)]
    [InlineData("Claimant / Respondent", PartyRole.Respondent)]
    [InlineData("Claimant", PartyRole.Claimant)]
    [InlineData("Claimant/ Appellant", PartyRole.Appellant)]
    [InlineData("Claimant/ Respondent", PartyRole.Respondent)]
    [InlineData("Claimant/Appellant", PartyRole.Appellant)]
    [InlineData("Claimant/Applicant", PartyRole.Applicant)]
    [InlineData("Claimant/Part 20 Defendant", PartyRole.Claimant)]
    [InlineData("Claimant/Respondent", PartyRole.Respondent)]
    [InlineData("Claimants", PartyRole.Claimant)]
    [InlineData("Claimants/ Appellants", PartyRole.Appellant)]
    [InlineData("Claimants/Appellants", PartyRole.Appellant)]
    [InlineData("Claimants/Respondents", PartyRole.Respondent)]
    [InlineData("Clamaints/ Respondents", PartyRole.Respondent)]
    [InlineData("Defendant / Counterclaimant", PartyRole.Defendant)]
    [InlineData("Defendant / Respondent", PartyRole.Respondent)]
    [InlineData("Defendant", PartyRole.Defendant)]
    [InlineData("Defendant/ Appellant", PartyRole.Appellant)]
    [InlineData("Defendant/ Applicant", PartyRole.Applicant)]
    [InlineData("Defendant/ Respondent", PartyRole.Respondent)]
    [InlineData("Defendant/Appellant", PartyRole.Appellant)]
    [InlineData("Defendant/Part 20 Claimant", PartyRole.Defendant)]
    [InlineData("Defendant/Respondent", PartyRole.Respondent)]
    [InlineData("Defendants / Appellants", PartyRole.Appellant)]
    [InlineData("Defendants", PartyRole.Defendant)]
    [InlineData("Defendants/ Appellants", PartyRole.Appellant)]
    [InlineData("Defendants/ Respondents", PartyRole.Respondent)]
    [InlineData("Defendants/Appellants", PartyRole.Appellant)]
    [InlineData("Defendants/Appellants/", PartyRole.Appellant)]
    [InlineData("Defendants/Respondents", PartyRole.Respondent)]
    [InlineData("FIRST DEFENDANT’S SOLICITOR/APPELLANT", PartyRole.Appellant)]
    [InlineData("First Claimant", PartyRole.Claimant)]
    [InlineData("First Defendant", PartyRole.Defendant)]
    [InlineData("First Respondent", PartyRole.Respondent)]
    [InlineData("Fourth Respondent", PartyRole.Respondent)]
    [InlineData("Interested Parties", PartyRole.InterestedParty)]
    [InlineData("Interested Party", PartyRole.InterestedParty)]
    [InlineData("Interested parties", PartyRole.InterestedParty)]
    [InlineData("Intervener", PartyRole.Intervener)]
    [InlineData("Interveners", PartyRole.Intervener)]
    [InlineData("Petitioner", PartyRole.Petitioner)]
    [InlineData("Petitioner/Respondent", PartyRole.Respondent)]
    [InlineData("Petitioners", PartyRole.Petitioner)]
    [InlineData("Respond-ents/ Defendants", PartyRole.Respondent)]
    [InlineData("Respondent / Defendant", PartyRole.Respondent)]
    [InlineData("Respondent", PartyRole.Respondent)]
    [InlineData("Respondent/ Claimant", PartyRole.Respondent)]
    [InlineData("Respondent/ First Defendant", PartyRole.Respondent)]
    [InlineData("Respondent/Appellant", PartyRole.Appellant)]
    [InlineData("Respondent/Applicant", PartyRole.Applicant)]
    [InlineData("Respondent/Claimant", PartyRole.Respondent)]
    [InlineData("Respondent/Defendants", PartyRole.Respondent)]
    [InlineData("Respondent/Petitioner", PartyRole.Respondent)]
    [InlineData("Respondent/Respondent", PartyRole.Respondent)]
    [InlineData("Respondents / Claimants", PartyRole.Respondent)]
    [InlineData("Respondents Second and Third/ Defendants", PartyRole.Respondent)]
    [InlineData("Respondents", PartyRole.Respondent)]
    [InlineData("Respondents/ Defendants", PartyRole.Respondent)]
    [InlineData("Respondents/Claimants", PartyRole.Respondent)]
    [InlineData("Respondents/Defendants", PartyRole.Respondent)]
    [InlineData("Respondents/Respondents", PartyRole.Respondent)]
    [InlineData("Respondnet", PartyRole.Respondent)]
    [InlineData("Second Claimant", PartyRole.Claimant)]
    [InlineData("Second Defendant", PartyRole.Defendant)]
    [InlineData("Second Interested Party", PartyRole.InterestedParty)]
    [InlineData("Second Respondent", PartyRole.Respondent)]
    [InlineData("Third Defendant", PartyRole.Defendant)]
    [InlineData("Third Interested Party", PartyRole.InterestedParty)]
    [InlineData("Third Party", PartyRole.ThirdParty)]
    [InlineData("Third Party/Appellant", PartyRole.Appellant)]
    [InlineData("Third Respondent", PartyRole.Respondent)]
    [InlineData("requested person", PartyRole.RequestedPerson)]
    [InlineData("requested persons", PartyRole.RequestedPerson)]
    [InlineData("requesting state", PartyRole.RequestingState)]
    [InlineData("1st Defendant", PartyRole.Defendant)]
    [InlineData("CLAIMANT", PartyRole.Claimant)]
    [InlineData("Claimant and Defendant", PartyRole.Claimant)]
    [InlineData("Claimant/Defendant", PartyRole.Claimant)]
    [InlineData("Defendant/Applicant", PartyRole.Applicant)]
    [InlineData("Part 20 Defendant", PartyRole.Defendant)]
    [InlineData("Requested Person", PartyRole.RequestedPerson)]
    [InlineData("Requesting State", PartyRole.RequestingState)]
    [InlineData("Sixth Appellant", PartyRole.Appellant)]
    public void TryGetSinglePartyRole_ParsesRoleFromFreeText(string text, PartyRole expected)
    {
        var found = PartyEnricher.TryGetSinglePartyRole(out var actual, text);

        found.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Claimant/NotARole")]
    [InlineData("NotARole/Appellant")]
    [InlineData("Third not a role")]
    [InlineData("4th not and a role")]
    [InlineData("Not A Role")]
    public void TryGetSinglePartyRole_ReturnsFalseWhenNotRole(string text)
    {
        var found = PartyEnricher.TryGetSinglePartyRole(out _, text);

        found.ShouldBe(false);
    }

    [Theory]
    [InlineData("Claimant", "Interested Party")]
    [InlineData("Appellant", "Third party")]
    [InlineData("Respondents", "Third party")]
    [InlineData("Respondents/Defendants", "Interested parties")]
    public void TryGetSinglePartyRole_ReturnsFalseWhenLastRoleCannotBeCombined(params string[] inputRoleStrings)
    {
        var found = PartyEnricher.TryGetSinglePartyRole(out _, inputRoleStrings);

        found.ShouldBe(false);
    }

    [Theory]
    [InlineData("Appellant", PartyRole.Appellant)]
    [InlineData("Claimant", PartyRole.Claimant)]
    [InlineData("Applicant", PartyRole.Applicant)]
    [InlineData("Defendant", PartyRole.Defendant)]
    [InlineData("Respondent", PartyRole.Respondent)]
    [InlineData("Petitioner", PartyRole.Petitioner)]
    [InlineData("Interested Party", PartyRole.InterestedParty)]
    [InlineData("1st Claimant", PartyRole.Claimant)]
    [InlineData("Jane Doe", null)]
    public void TryGetSinglePartyRole_Cell_WithOneNonEmptyLine_ParsesRoleFromCellText(string text, PartyRole? expected)
    {
        var cell = CellOf(text);

        var found = PartyEnricher.TryGetSinglePartyRole(cell, out var actual);

        found.ShouldBe(expected is not null);
        if (expected is not null)
        {
            actual.ShouldBe(expected.Value);
        }
    }

    [Theory]
    [InlineData("Claimant/", "Respondent", PartyRole.Respondent)]
    [InlineData("Appellants/", "Claimants", PartyRole.Appellant)]
    [InlineData("Respondent", "Defendant", PartyRole.Respondent)]
    [InlineData("1st Respondent", "2nd Respondent", PartyRole.Respondent)]
    [InlineData("Defendant/", "Applicant", PartyRole.Applicant)]
    public void TryGetSinglePartyRole_Cell_WithTwoNonEmptyLines_ParsesRoleFromCombinedText(string first, string second,
        PartyRole expected)
    {
        var cell = CellOf(first, second);

        var found = PartyEnricher.TryGetSinglePartyRole(cell, out var actual);

        found.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Fact]
    public void TryGetSinglePartyRole_Cell_WithThreeOrdinalDefendantLines_ReturnsDefendant()
    {
        var cell = CellOf("1st Defendant", "2nd Defendant", "3rd Defendant");

        var found = PartyEnricher.TryGetSinglePartyRole(cell, out var actual);

        found.ShouldBeTrue();
        actual.ShouldBe(PartyRole.Defendant);
    }
}
