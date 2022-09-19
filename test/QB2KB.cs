
using Xunit;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw.Test {

public class QB2KB {

    [Theory]
    [InlineData("[2022] EWHC 2169 (KB)", "ewhc/kb/2022/2169")]
    [InlineData("  [2022]   EWHC    2169    Kb)   ", "ewhc/kb/2022/2169")]
    public void TestNeutralCitation(string ncn, string id) {
        string normalized = Citations.Normalize(ncn);
        string generated = Citations.MakeUriComponent(normalized);
        Assert.Equal(id, generated);
    }

    [Theory]
    [InlineData("IN THE HIGH COURT OF JUSTICE/QUEEN'S BENCH DIVISION/ADMINISTRATIVE COURT/PLANNING COURT", "EWHC-QBD-Planning")]
    [InlineData("IN THE HIGH COURT OF JUSTICE/KING'S BENCH DIVISION/ADMINISTRATIVE COURT/PLANNING COURT", "EWHC-KBD-Planning")]
    [InlineData("IN THE HIGH COURT OF JUSTICE/KING'S BENCH DIVISION/ADMINISTRATIVE COURT", "EWHC-KBD-Admin")]
    [InlineData("IN THE HIGH COURT OF JUSTICE/KING'S BENCH DIVISION/TECHNOLOGY AND CONSTRUCTION COURT", "EWHC-KBD-TCC")]
    public void TestCourtType(string combined, string code) {
        string[] lines = combined.Split('/');
        Court? court = UK.Gov.NationalArchives.CaseLaw.CourtTypeTest.Test(lines);
        Assert.Equal(code, court.HasValue ? court.Value.Code : "-none-");
    }

}

}
