using System.Linq;

using DocumentFormat.OpenXml.Wordprocessing;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Xunit;

namespace test.parsers.common.enrich;

public partial class TestPartyEnricher
{
    private static readonly PartyEnricher PartyEnricher = new();

    private static readonly WLine LineTemplate = new(null, new Paragraph());

    private static WLine TextLine(params string[] text)
    {
        return new WLine(LineTemplate, text.Select(t => new WText(t, null)));
    }

    private static WRow RowOf(params string[] cells)
    {
        return new WRow(TableOf(), null, null, cells.Select(c => CellOf(c)));
    }

    private static WRow RowOf(WCell[] cells)
    {
        return new WRow(TableOf(), null, null, cells);
    }

    private static WCell CellOf(params string[] lines)
    {
        return new WCell(RowOf(), null, lines.Select(l => TextLine(l)));
    }

    private static WCell CellWithOneLineOf(params string[] text)
    {
        return new WCell(RowOf(), null, [TextLine(text)]);
    }

    private static WTable TableOf(params string[][] rows)
    {
        return new WTable(null, null, null, rows.Select(RowOf));
    }

    private static WTable TableOf(WRow[] rows)
    {
        return new WTable(null, null, null, rows);
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

        result[1].Contents.ShouldHaveSingleItem().ShouldBeOfType<WDocTitle>().Text
                 .ShouldBe("IN THE MATTER OF SOME TRUST");

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
            [Tab(), new WText("Jane", null), new WText(" Doe", null)]);
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
            new WText("Jane Doe", null), Tab(), new WText("Claimant", null)
        ]);
        var between = TextLine("v");
        var defendantNameAndRole = new WLine(LineTemplate,
        [
            new WText("John Smith", null), Tab(), new WText("Defendant", null)
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
    public void Enrich_MultiLineBlock_WithLeadingTabsBeforeNameAndRole_AssignsRolesToEachSide()
    {
        var beforeMarker = TextLine("BETWEEN");
        var claimantNameAndRole = new WLine(LineTemplate,
            [Tab(), Tab(), new WText("Jane Doe", null), Tab(), new WText("Claimant", null)]
            );
        var between = TextLine("v");
        var defendantNameAndRole = new WLine(LineTemplate,
        [
            new WText("John Smith", null), Tab(), new WText("Defendant", null)
        ]);
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
            [
                beforeMarker, claimantNameAndRole, between, defendantNameAndRole, afterMarker
            ]
        ).Cast<WLine>().ToArray();

        var claimantLine = result[1];
        claimantLine.Contents.ElementAt(0).ShouldBeOfType<WTab>();
        claimantLine.Contents.ElementAt(1).ShouldBeOfType<WTab>();

        var claimantParty = claimantLine.Contents.ElementAt(2).ShouldBeOfType<WParty>();
        claimantParty.Role.ShouldBe(PartyRole.Claimant);
        claimantParty.Text.ShouldBe("Jane Doe");
        claimantLine.Contents.ElementAt(4).ShouldBeOfType<WRole>().Role.ShouldBe(PartyRole.Claimant);

        var defendantLine = result[3];
        var defendantParty = defendantLine.Contents.ElementAt(0).ShouldBeOfType<WParty>();
        defendantParty.Role.ShouldBe(PartyRole.Defendant);
        defendantParty.Text.ShouldBe("John Smith");
        defendantLine.Contents.ElementAt(2).ShouldBeOfType<WRole>().Role.ShouldBe(PartyRole.Defendant);
    }

    private static WTab Tab()
    {
        return new WTab(new TabChar());
    }

    public static TheoryData<string, IBlock> NonMatchingNameAndRoleLines()
    {
        return new TheoryData<string, IBlock>
        {
            {
                "too few content items", new WLine(LineTemplate,
                [
                    new WText("John Smith", null), Tab()
                ])
            },
            {
                "extra text before the name", new WLine(LineTemplate,
                [
                    new WText("Extra", null),
                    new WText("John Smith", null), Tab(), new WText("Defendant", null)
                ])
            },
            {
                "unrecognised role text", new WLine(LineTemplate,
                [
                    new WText("John Smith", null), Tab(), new WText("Not A Role", null)
                ])
            },
            {
                "no tab before the role", new WLine(LineTemplate,
                [
                    new WText("John Smith", null), new WText(" ", null), new WText("Defendant", null)
                ])
            },
            {
                "name is missing before the tab", new WLine(LineTemplate,
                [
                    Tab(), Tab(), new WText("Defendant", null)
                ])
            },
            {
                "role is missing after the tab", new WLine(LineTemplate,
                [
                    new WText("John Smith", null), Tab(), Tab()
                ])
            }
        };
    }

    [Theory]
    [MemberData(nameof(NonMatchingNameAndRoleLines))]
    public void Enrich_MultiLineBlock_WithMalformedNameAndRoleTabbedLine_LeavesBlockUnchanged(string scenario,
        IBlock defendantNameAndRoleLine)
    {
        var beforeMarker = TextLine("BETWEEN");
        var claimantNameAndRole = new WLine(LineTemplate,
        [
            new WText("Jane Doe", null), Tab(), new WText("Claimant", null)
        ]);
        var between = TextLine("v");
        var afterMarker = TextLine("-----");

        var result = PartyEnricher.Enrich(
            [
                beforeMarker, claimantNameAndRole, between, defendantNameAndRoleLine, afterMarker
            ]
        ).Cast<WLine>().ToArray();

        result[0].ShouldBeSameAs(beforeMarker, scenario);
        result[1].ShouldBeSameAs(claimantNameAndRole, scenario);
        result[2].ShouldBeSameAs(between, scenario);
        result[3].ShouldBeSameAs(defendantNameAndRoleLine, scenario);
        result[4].ShouldBeSameAs(afterMarker, scenario);
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

    [Theory]
    [InlineData("NNB Generation Company (SZC) Limited", " -and- ", PartyRole.Claimant)]
    [InlineData("John Smith", " (formerly known as John Doe)", PartyRole.Appellant)]
    public void Enrich_TwoCellTableRow_NameCellWithTrailingNonPartyTextRun_LeavesNonPartyTextRunUnwrapped(string partyName, string otherText, PartyRole role)
    {
        var table = TableOf([
            RowOf([
                CellWithOneLineOf(partyName, otherText),
                CellOf(role.ToString())
            ])
        ]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var resultCell = resultTable.TypedRows.ShouldHaveSingleItem()
                                      .TypedCells[0].Contents.ShouldHaveSingleItem()
                                      .ShouldBeOfType<WLine>();

        var party = resultCell.Contents.ElementAt(0).ShouldBeOfType<WParty>();
        party.Role.ShouldBe(role);
        party.Text.ShouldBe(partyName);

        resultCell.Contents.ElementAt(1).ShouldBeOfType<WText>().Text.ShouldBe(otherText);
    }

    [Fact]
    public void Enrich_TwoCellTableRow_NameCellWithTwoQualifyingTextRuns_WrapsBothRunsAsOneParty()
    {
        var table = TableOf([
            RowOf([
                CellWithOneLineOf("Big Company ", "Limited"), CellOf("Claimant")
            ])
        ]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var resultCell = resultTable.TypedRows.ShouldHaveSingleItem()
                                      .TypedCells[0].Contents.ShouldHaveSingleItem()
                                      .ShouldBeOfType<WLine>();

        var party = resultCell.Contents.ShouldHaveSingleItem().ShouldBeOfType<WParty2>();
        party.Role.ShouldBe(PartyRole.Claimant);
        party.Text.ShouldBe("Big Company Limited");
    }

    [Fact]
    public void Enrich_TwoCellTableRow_NameCellWithNumberedTabbedName_AssignsPartyToNameAfterTab()
    {
        var cellWithNumberedTabbedName = new WCell(RowOf(), null, [
            new WLine(LineTemplate, [
                new WText("1.", null),
                Tab(),
                new WText("John Smith", null)
            ])
        ]);

        var table = TableOf([
            RowOf([cellWithNumberedTabbedName, CellOf("Claimant")])
        ]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var resultCell = resultTable.TypedRows.ShouldHaveSingleItem()
                                      .TypedCells[0].Contents.ShouldHaveSingleItem()
                                      .ShouldBeOfType<WLine>();

        resultCell.Contents.ElementAt(0).ShouldBeOfType<WText>().Text.ShouldBe("1.");
        resultCell.Contents.ElementAt(1).ShouldBeOfType<WTab>();

        var party = resultCell.Contents.ElementAt(2).ShouldBeOfType<WParty>();
        party.Role.ShouldBe(PartyRole.Claimant);
        party.Text.ShouldBe("John Smith");
    }

    [Fact]
    public void Enrich_TwoCellTableRow_NameCellIsBareAndMarker_DoesNotWrapEitherRunAsParty()
    {
        var table = TableOf([
            RowOf([
                CellWithOneLineOf("- and ", "–"),
                CellOf("Defendant")
            ])
        ]);

        var result = PartyEnricher.Enrich([table]);

        var resultTable = result.ShouldHaveSingleItem().ShouldBeOfType<WTable>();
        var resultCell = resultTable.TypedRows.ShouldHaveSingleItem()
                                      .TypedCells[0].Contents.ShouldHaveSingleItem()
                                      .ShouldBeOfType<WLine>();

        resultCell.Contents.Count().ShouldBe(2);
        resultCell.Contents.ElementAt(0).ShouldBeOfType<WText>().Text.ShouldBe("- and ");
        resultCell.Contents.ElementAt(1).ShouldBeOfType<WText>().Text.ShouldBe("–");
    }
}
