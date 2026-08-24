using System.Linq;

using DocumentFormat.OpenXml.Wordprocessing;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.common.enrich;

public class TestPartyEnricher
{
    private static readonly PartyEnricher PartyEnricher = new();

    private static readonly WLine LineTemplate = new(null, new Paragraph());

    private static WLine TextLine(string text)
    {
        return new WLine(LineTemplate, [new WText(text, null)]);
    }

    private static WRow RowOf(params string[] cells)
    {
        return new WRow(TableOf(), null, null, cells.Select(c => CellOf(c)));
    }

    private static WCell CellOf(params string[] lines)
    {
        return new WCell(RowOf(), null, lines.Select(TextLine));
    }

    private static WTable TableOf(params string[][] rows)
    {
        return new WTable(null, null, null, rows.Select(RowOf));
    }

    [Theory]
    [InlineData("Claimant", PartyRole.Claimant)]
    [InlineData("Claimants", PartyRole.Claimant)]
    [InlineData("CLAIMANT", PartyRole.Claimant)]
    [InlineData("(Claimant)", PartyRole.Claimant)]
    [InlineData("First Claimant", PartyRole.Claimant)]
    [InlineData("1st Defendant", PartyRole.Defendant)]
    [InlineData("Third Respondent", PartyRole.Respondent)]
    [InlineData("Sixth Appellant", PartyRole.Appellant)]
    [InlineData("Applicant", PartyRole.Applicant)]
    [InlineData("Petitioner", PartyRole.Petitioner)]
    [InlineData("Interested Party", PartyRole.InterestedParty)]
    [InlineData("Interested Parties", PartyRole.InterestedParty)]
    [InlineData("Intervener", PartyRole.Intervener)]
    [InlineData("Interveners", PartyRole.Intervener)]
    [InlineData("Requested Person", PartyRole.RequestedPerson)]
    [InlineData("Requesting State", PartyRole.RequestingState)]
    [InlineData("Third Party", PartyRole.ThirdParty)]
    [InlineData("Part 20 Defendant", PartyRole.Defendant)]
    [InlineData("Claimant/Defendant", PartyRole.Claimant)]
    [InlineData("Appellant/Respondent", PartyRole.Appellant)]
    [InlineData("Respondent/Appellant", PartyRole.Appellant)]
    [InlineData("Defendant/Applicant", PartyRole.Applicant)]
    [InlineData("Claimant and Defendant", PartyRole.Claimant)]
    [InlineData("Not A Role", null)]
    [InlineData("Claimant/NotARole", null)]
    public void GetPartyRole_ParsesRoleFromFreeText(string text, PartyRole? expected)
    {
        var found = PartyEnricher.TryGetPartyRole(text, out var actual);

        found.ShouldBe(expected is not null);
        if (expected is not null)
        {
            actual.ShouldBe(expected.Value);
        }
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
    public void GetPartyRole_Cell_WithOneNonEmptyLine_ParsesRoleFromCellText(string text, PartyRole? expected)
    {
        var cell = CellOf(text);

        var found = PartyEnricher.TryGetPartyRole(cell, out var actual);

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
    public void GetPartyRole_Cell_WithTwoNonEmptyLines_ParsesRoleFromCombinedText(string first, string second,
        PartyRole expected)
    {
        var cell = CellOf(first, second);

        var found = PartyEnricher.TryGetPartyRole(cell, out var actual);

        found.ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Fact]
    public void GetPartyRole_Cell_WithThreeOrdinalDefendantLines_ReturnsDefendant()
    {
        var cell = CellOf("1st Defendant", "2nd Defendant", "3rd Defendant");

        var found = PartyEnricher.TryGetPartyRole(cell, out var actual);

        found.ShouldBeTrue();
        actual.ShouldBe(PartyRole.Defendant);
    }

    [Theory]
    [InlineData("REX")]
    [InlineData("R E X")]
    [InlineData("REGINA")]
    [InlineData("R E G I N A")]
    public void Enrich_ThreeLineRexOrReginaBlock_AssignsBeforeAndAfterTheVRoles(string sovereign)
    {
        var sovereignLine = TextLine(sovereign);
        var betweenMarker = TextLine("v");
        var defendantLine = TextLine("John Smith");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
            [
                sovereignLine,
                betweenMarker,
                defendantLine,
                afterMarker
            ]
        ).Cast<WLine>().ToArray();

        var crown = result[0].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        crown.Role.ShouldBe(PartyRole.BeforeTheV);
        crown.Text.ShouldBe(sovereign);

        result[1].ShouldBeSameAs(betweenMarker);

        var defendant = result[2].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        defendant.Role.ShouldBe(PartyRole.AfterTheV);
        defendant.Text.ShouldBe("John Smith");

        result[3].ShouldBeSameAs(afterMarker);
    }

    [Fact]
    public void Enrich_FourLineRexOrReginaBlock_AssignsBeforeAndAfterTheVRolesToBothDefendants()
    {
        var sovereignLine = TextLine("REX");
        var betweenMarker = TextLine("v");
        var defendant1 = TextLine("John Smith");
        var defendant2 = TextLine("Jane Smith");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
            [
                sovereignLine, betweenMarker, defendant1, defendant2, afterMarker
            ]
        ).Cast<WLine>().ToArray();

        result[0].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>()
                 .Role.ShouldBe(PartyRole.BeforeTheV);

        result[1].ShouldBeSameAs(betweenMarker);

        result[2].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>()
                 .Role.ShouldBe(PartyRole.AfterTheV);

        result[3].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>()
                 .Role.ShouldBe(PartyRole.AfterTheV);

        result[4].ShouldBeSameAs(afterMarker);
    }

    [Theory]
    [InlineData("-", "-")]
    [InlineData("-----", "-----")]
    [InlineData("- - - -", "- - - -")]
    [InlineData("_____", "_____")]
    [InlineData("-----", "Computer Aided Transcript of Proceedings")]
    [InlineData("-----", "REPORTING RESTRICTIONS APPLY: see note")]
    public void Enrich_FiveLinePartyBlock_AssignsBeforeAndAfterTheVRoles(string beforeMarkerText,
        string afterMarkerText)
    {
        var beforeMarker = TextLine(beforeMarkerText);
        var claimantLine = TextLine("Jane Doe");
        var betweenMarker = TextLine("v");
        var defendantLine = TextLine("John Smith");
        var afterMarker = TextLine(afterMarkerText);

        var result = PartyEnricher.Enrich(
            [
                beforeMarker, claimantLine, betweenMarker, defendantLine, afterMarker
            ]
        ).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(beforeMarker);

        var claimant = result[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        claimant.Role.ShouldBe(PartyRole.BeforeTheV);
        claimant.Text.ShouldBe("Jane Doe");

        result[2].ShouldBeSameAs(betweenMarker);

        var defendant = result[3].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        defendant.Role.ShouldBe(PartyRole.AfterTheV);
        defendant.Text.ShouldBe("John Smith");

        result[4].ShouldBeSameAs(afterMarker);
    }

    [Fact]
    public void Enrich_ThreeLineInTheMatterOfBlock_WrapsMiddleLineAsDocTitle()
    {
        var beforeMarker = TextLine("-----");
        var matterLine = TextLine("IN THE MATTER OF SOME TRUST");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
        [
            beforeMarker, matterLine, afterMarker
        ]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(beforeMarker);

        result[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WDocTitle>().Text.ShouldBe("IN THE MATTER OF SOME TRUST");

        result[2].ShouldBeSameAs(afterMarker);
    }

    [Fact]
    public void Enrich_FourLineInTheMatterOfBlock_WrapsBothMiddleLinesAsDocTitles()
    {
        var beforeMarker = TextLine("-----");
        var matterLine1 = TextLine("IN THE MATTER OF");
        var matterLine2 = TextLine("SOME TRUST LIMITED");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
        [
            beforeMarker, matterLine1, matterLine2, afterMarker
        ]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(beforeMarker);

        result[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WDocTitle>()
                 .Text.ShouldBe("IN THE MATTER OF");

        result[2].Contents.ShouldHaveSingleItem().ShouldBeOfType<WDocTitle>()
                 .Text.ShouldBe("SOME TRUST LIMITED");

        result[3].ShouldBeSameAs(afterMarker);
    }

    [Fact]
    public void Enrich_StandaloneInTheMatterOfLine_IsWrappedAsDocTitleWithoutSurroundingMarkers()
    {
        var line = TextLine("IN THE MATTER OF A TRUST");

        var result = PartyEnricher.Enrich([line]).Cast<WLine>().ToArray();

        result.ShouldHaveSingleItem()
              .Contents.ShouldHaveSingleItem()
              .ShouldBeOfType<WDocTitle>()
              .Text.ShouldBe("IN THE MATTER OF A TRUST");
    }

    [Theory]
    [InlineData("v")]
    [InlineData("and")]
    public void Enrich_MultiLineBetweenBlock_AssignsRolesToNamesAndWrapsRoleLabels(string betweenMarker)
    {
        var beforeMarker = TextLine("BETWEEN");
        var claimantName = TextLine("Jane Doe");
        var claimantRole = TextLine("Claimant");
        var between = TextLine(betweenMarker);
        var defendantName = TextLine("John Smith");
        var defendantRole = TextLine("Defendant");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
        [
            beforeMarker, claimantName, claimantRole, between, defendantName, defendantRole, afterMarker
        ]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(beforeMarker);

        var claimantParty = result[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        claimantParty.Role.ShouldBe(PartyRole.Claimant);
        claimantParty.Text.ShouldBe("Jane Doe");

        result[2].Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
                 .Role.ShouldBe(PartyRole.Claimant);

        result[3].ShouldBeSameAs(between);

        var defendantParty = result[4].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        defendantParty.Role.ShouldBe(PartyRole.Defendant);
        defendantParty.Text.ShouldBe("John Smith");

        result[5].Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
                 .Role.ShouldBe(PartyRole.Defendant);

        result[6].ShouldBeSameAs(afterMarker);
    }

    [Fact]
    public void Enrich_FiveLinePartyBlock_NameWithLeadingTab_WrapsRemainderAsASingleParty()
    {
        var beforeMarker = TextLine("-----");
        var claimantLine = new WLine(LineTemplate,
            [new WTab(new TabChar()), new WText("Jane", null), new WText(" Doe", null)]);
        var betweenMarker = TextLine("v");
        var defendantLine = TextLine("John Smith");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
            [
                beforeMarker, claimantLine, betweenMarker, defendantLine, afterMarker
            ]
        ).Cast<WLine>().ToArray();

        var resultLine = result[1];
        resultLine.Contents.ElementAt(0).ShouldBeOfType<WTab>();

        var claimant = resultLine.Contents.ElementAt(1).ShouldBeOfType<WParty2>();
        claimant.Role.ShouldBe(PartyRole.BeforeTheV);
        claimant.Text.ShouldBe("Jane Doe");
    }

    [Fact]
    public void Enrich_LinesWithNoPartyPattern_ReturnsBlocksUnchanged()
    {
        var greeting = TextLine("Hello");
        var world = TextLine("World");

        var result = PartyEnricher.Enrich([greeting, world]).ToArray();

        result[0].ShouldBeSameAs(greeting);
        result[1].ShouldBeSameAs(world);
    }

    [Fact]
    public void Enrich_FiveLinePartyBlock_NameWithMultipleTextRuns_WrapsAsASingleParty()
    {
        var beforeMarker = TextLine("-----");
        var claimantLine = new WLine(LineTemplate, [new WText("Jane", null), new WText(" Doe", null)]);
        var betweenMarker = TextLine("v");
        var defendantLine = TextLine("John Smith");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
            [
                beforeMarker, claimantLine, betweenMarker, defendantLine, afterMarker
            ]
        ).Cast<WLine>().ToArray();

        var claimant = result[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty2>();
        claimant.Role.ShouldBe(PartyRole.BeforeTheV);
        claimant.Text.ShouldBe("Jane Doe");
    }

    [Fact]
    public void Enrich_MultiLineBetweenBlock_WithInPrivateLine_PreservesItAndAssignsRoles()
    {
        var beforeMarker = TextLine("-----");
        var inPrivate = TextLine("IN PRIVATE");
        var claimantName = TextLine("Jane Doe");
        var claimantRole = TextLine("Claimant");
        var between = TextLine("v");
        var defendantName = TextLine("John Smith");
        var defendantRole = TextLine("Defendant");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
        [
            beforeMarker, inPrivate, claimantName, claimantRole, between, defendantName, defendantRole, afterMarker
        ]).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(beforeMarker);

        result[1].ShouldBeSameAs(inPrivate);

        result[2].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>()
                 .Role.ShouldBe(PartyRole.Claimant);

        result[3].Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
                 .Role.ShouldBe(PartyRole.Claimant);

        result[4].ShouldBeSameAs(between);

        result[5].Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>()
                 .Role.ShouldBe(PartyRole.Defendant);

        result[6].Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
                 .Role.ShouldBe(PartyRole.Defendant);

        result[7].ShouldBeSameAs(afterMarker);
    }

    [Fact]
    public void Enrich_MultiLineBlock_WithNameAndRoleTabbedOnTheSameLine_AssignsRolesToEachSide()
    {
        var beforeMarker = TextLine("BETWEEN");
        var claimantNameAndRole = new WLine(LineTemplate,
        [
            new WText("Jane Doe", null), new WTab(new TabChar()), new WText("Claimant", null)
        ]);
        var between = TextLine("v");
        var defendantNameAndRole = new WLine(LineTemplate,
        [
            new WText("John Smith", null), new WTab(new TabChar()), new WText("Defendant", null)
        ]);
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
            [
                beforeMarker, claimantNameAndRole, between, defendantNameAndRole, afterMarker
            ]
        ).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(beforeMarker);

        var claimantLine = result[1];
        var claimantParty = claimantLine.Contents.ElementAt(0).ShouldBeOfType<WParty>();
        claimantParty.Role.ShouldBe(PartyRole.Claimant);
        claimantParty.Text.ShouldBe("Jane Doe");
        claimantLine.Contents.ElementAt(2).ShouldBeOfType<WRole>().Role.ShouldBe(PartyRole.Claimant);

        result[2].ShouldBeSameAs(between);

        var defendantLine = result[3];
        var defendantParty = defendantLine.Contents.ElementAt(0).ShouldBeOfType<WParty>();
        defendantParty.Role.ShouldBe(PartyRole.Defendant);
        defendantParty.Text.ShouldBe("John Smith");
        defendantLine.Contents.ElementAt(2).ShouldBeOfType<WRole>().Role.ShouldBe(PartyRole.Defendant);

        result[4].ShouldBeSameAs(afterMarker);
    }

    [Fact]
    public void Enrich_TwoCellTableRow_AssignsPartyFromNameCellAndRoleFromRoleCell()
    {
        var table = TableOf(["Jane Doe", "Claimant"]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var cells = resultTable.TypedRows.ShouldHaveSingleItem().TypedCells;

        var party = cells[0].Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
                            .Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        party.Role.ShouldBe(PartyRole.Claimant);
        party.Text.ShouldBe("Jane Doe");

        cells[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
                .Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
                .Role.ShouldBe(PartyRole.Claimant);
    }

    [Fact]
    public void Enrich_TwoCellTableRow_NameCellWithNumberedParties_SplitsIntoTwoParties()
    {
        var table = TableOf(["(1)John Smith(2)Jane Doe", "Claimant"]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var nameLine = resultTable.TypedRows.ShouldHaveSingleItem()
                                  .TypedCells[0].Contents.ShouldHaveSingleItem()
                                  .ShouldBeOfType<WLine>();

        var party1 = nameLine.Contents.ElementAt(0).ShouldBeOfType<WParty>();
        party1.Role.ShouldBe(PartyRole.Claimant);
        party1.Text.ShouldBe("(1)John Smith");

        var party2 = nameLine.Contents.ElementAt(1).ShouldBeOfType<WParty>();
        party2.Role.ShouldBe(PartyRole.Claimant);
        party2.Text.ShouldBe("(2)Jane Doe");
    }

    [Fact]
    public void Enrich_ThreeCellTableRow_WithEmptyFirstCellAndRoleInThirdCell_AssignsPartyAndRole()
    {
        var table = TableOf(["", "Jane Doe", "Claimant"]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var cells = resultTable.TypedRows.ShouldHaveSingleItem().TypedCells;

        var party = cells[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
                            .Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        party.Role.ShouldBe(PartyRole.Claimant);
        party.Text.ShouldBe("Jane Doe");

        cells[2].Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
                .Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
                .Role.ShouldBe(PartyRole.Claimant);
    }

    [Fact]
    public void Enrich_ThreeCellTableRow_WithInTheMatterOfInSecondCell_WrapsItAsDocTitle()
    {
        var table = TableOf(["", "IN THE MATTER OF A TRUST", ""]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var cells = resultTable.TypedRows.ShouldHaveSingleItem().TypedCells;
        cells[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
                .Contents.ShouldHaveSingleItem().ShouldBeOfType<WDocTitle>()
                .Text.ShouldBe("IN THE MATTER OF A TRUST");
    }

    [Fact]
    public void Enrich_ThreeCellTableRow_WithTwoDistinctRolesInThirdCell_AssignsRolesToNamesAndLabels()
    {
        var table = new WTable(null, null, null,
        [
            new WRow(TableOf(), null, null,
                [CellOf(""), CellOf("Jane Doe", "and", "John Smith"), CellOf("Claimant", "", "Intervener")])
        ]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var cells = resultTable.TypedRows.ShouldHaveSingleItem().TypedCells;

        var namesCellLines = cells[1].Contents.ToArray();

        var claimantParty = namesCellLines[0].ShouldBeOfType<WLine>()
                                             .Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        claimantParty.Role.ShouldBe(PartyRole.Claimant);
        claimantParty.Text.ShouldBe("Jane Doe");

        var intervenerParty = namesCellLines[2].ShouldBeOfType<WLine>()
                                               .Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        intervenerParty.Role.ShouldBe(PartyRole.Intervener);
        intervenerParty.Text.ShouldBe("John Smith");

        var rolesCellLines = cells[2].Contents.ToArray();
        rolesCellLines[0].ShouldBeOfType<WLine>()
                         .Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
                         .Role.ShouldBe(PartyRole.Claimant);
        rolesCellLines[2].ShouldBeOfType<WLine>()
                         .Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
                         .Role.ShouldBe(PartyRole.Intervener);
    }

    [Fact]
    public void Enrich_ThreeRowTableWithNoRoleLabels_AssignsBeforeAndAfterTheVToNames()
    {
        var table = TableOf(
            ["", "Jane Doe", ""],
            ["", "v", ""],
            ["", "John Smith", ""]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var rows = resultTable.TypedRows;

        var claimant = rows[0].TypedCells[1]
                              .Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
                              .Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        claimant.Role.ShouldBe(PartyRole.BeforeTheV);
        claimant.Text.ShouldBe("Jane Doe");

        rows[1].TypedCells[1]
               .Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
               .Contents.ShouldHaveSingleItem().ShouldBeOfType<WText>()
               .Text.ShouldBe("v");

        var defendant = rows[2].TypedCells[1]
                               .Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
                               .Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        defendant.Role.ShouldBe(PartyRole.AfterTheV);
        defendant.Text.ShouldBe("John Smith");
    }

    [Fact]
    public void Enrich_ThreeCellTableRow_WithRoleOnlyInFollowingRow_AssignsPartyFromLookahead()
    {
        var table = TableOf(
            ["", "Jane Doe", ""],
            ["", "", "Claimant"]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var rows = resultTable.TypedRows;

        var party = rows[0].TypedCells[1]
                           .Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
                           .Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty>();
        party.Role.ShouldBe(PartyRole.Claimant);
        party.Text.ShouldBe("Jane Doe");

        rows[1].TypedCells[2]
               .Contents.ShouldHaveSingleItem().ShouldBeOfType<WLine>()
               .Contents.ShouldHaveSingleItem().ShouldBeOfType<WRole>()
               .Role.ShouldBe(PartyRole.Claimant);
    }
}
