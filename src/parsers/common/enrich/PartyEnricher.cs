using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse;

// there are "third" paries in EWCA/Civ/2015/631

internal class PartyEnricher : Enricher
{
    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        throw new NotSupportedException();
    }

    protected override WLine Enrich(WLine line)
    {
        throw new NotSupportedException();
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        var before = blocks.ToArray();
        var after = new List<IBlock>();
        var i = 0;
        while (i < before.Length)
        {
            var nextBlocks = before[i..];
            if (TryEnrichInTheMatterOf3(nextBlocks, out var enriched)
                || TryEnrichInTheMatterOf4(nextBlocks, out enriched)
                || TryEnrichThreeLinePartyBlock(nextBlocks, out enriched)
                || TryEnrichFourLinePartyBlock(nextBlocks, out enriched)
                || TryEnrichFiveLinePartyBlock(nextBlocks, out enriched)
                || TryEnrichMultiLinePartyBlock(nextBlocks, false, out enriched)
                || TryEnrichMultiLinePartyBlockWithInlineRoles(nextBlocks, out enriched)
                || TryEnrichMultiLinePartyBlockWithTwoGroupsBeforeV(nextBlocks, out enriched))
            {
                after.AddRange(enriched);
                i += enriched.Length;
                break;
            }

            after.Add(before[i] switch
            {
                WTable table => EnrichTable(table),
                WLine line => EnrichLineWithDocTitle(line),
                _ => before[i]
            });
            i += 1;
        }

        after.AddRange(before.Skip(i));
        return after;
    }

    private static bool TryEnrichInTheMatterOf3(IBlock[] before, out WLine[] enriched)
    {
        if (before is [WLine line1, WLine line2, WLine line3, ..]
            && IsBeforePartyMarker(line1)
            && IsInTheMatterOf1(line2)
            && IsAfterPartyMarker(line3))
        {
            enriched =
            [
                line1,
                MakeDocTitle(line2),
                line3
            ];
            return true;
        }

        enriched = null;
        return false;
    }

    private static bool TryEnrichInTheMatterOf4(IBlock[] before, out WLine[] enriched)
    {
        if (before is [WLine line1, WLine line2, WLine line3, WLine line4, ..]
            && IsBeforePartyMarker(line1)
            && IsInTheMatterOf1(line2)
            && IsInTheMatterOf2(line3)
            && IsAfterPartyMarker(line4))
        {
            enriched =
            [
                line1,
                MakeDocTitle(line2),
                MakeDocTitle(line3),
                line4
            ];
            return true;
        }

        enriched = null;
        return false;
    }

    private static bool IsRexOrRegina(WLine line)
    {
        var content = Regex.Replace(line.NormalizedContent, @"\s+", "");
        return content.Equals("REX", StringComparison.OrdinalIgnoreCase)
            || content.Equals("REGINA", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryEnrichThreeLinePartyBlock(IBlock[] before, out WLine[] enriched)
    {
        if (before is [WLine line1, WLine line2, WLine line3, WLine line4, ..]
            && IsRexOrRegina(line1)
            && IsV(line2)
            && IsPartyName(line3)
            && IsAfterPartyMarker(line4))
        {
            enriched =
            [
                MakeParty(line1, PartyRole.BeforeTheV),
                line2,
                MakeParty(line3, PartyRole.AfterTheV)
            ];
            return true;
        }

        enriched = null;
        return false;
    }

    private static bool TryEnrichFourLinePartyBlock(IBlock[] before, out WLine[] enriched)
    {
        if (before is [WLine line1, WLine line2, WLine line3, WLine line4, WLine line5, ..]
            && IsRexOrRegina(line1)
            && IsV(line2)
            && IsPartyName(line3)
            && IsPartyName(line4)
            && IsAfterPartyMarker(line5))
        {
            enriched =
            [
                MakeParty(line1, PartyRole.BeforeTheV),
                line2,
                MakeParty(line3, PartyRole.AfterTheV),
                MakeParty(line4, PartyRole.AfterTheV)
            ];
            return true;
        }

        enriched = null;
        return false;
    }

    private static bool TryEnrichFiveLinePartyBlock(IBlock[] before, out WLine[] enriched)
    {
        if (before is [WLine line1, WLine line2, WLine line3, WLine line4, WLine line5, ..]
            && IsBeforePartyMarker(line1)
            && IsPartyName(line2)
            && (IsV(line3) || IsAnd(line3))
            && IsPartyName(line4)
            && IsAfterPartyMarker(line5))
        {
            enriched =
            [
                line1,
                MakeParty(line2, PartyRole.BeforeTheV),
                line3,
                MakeParty(line4, PartyRole.AfterTheV),
                line5
            ];
            return true;
        }

        enriched = null;
        return false;
    }

    private sealed class BlockCursor(IBlock[] collection)
    {
        private int i;

        public bool NextLineMatches(Func<WLine, bool> test)
        {
            return i < collection.Length
                && collection[i] is WLine line
                && test(line);
        }

        public WLine ReadNextLine()
        {
            return (WLine)collection[i++];
        }

        public bool TryReadNextLine(out WLine result)
        {
            if (i < collection.Length && collection[i] is WLine line)
            {
                result = line;
                i++;
                return true;
            }

            result = null;
            return false;
        }

        public void AdvanceCursor(int num)
        {
            i += num;
        }

        public IBlock[] PeekRemaining()
        {
            return collection[i..];
        }
    }


    private static bool TryEnrichMultiLinePartyBlock(IBlock[] rest, bool successive, out WLine[] enriched)
    {
        enriched = null;
        var blockCursor = new BlockCursor(rest);

        if (!blockCursor.TryReadNextLine(out var beforeLine)
            || (!IsBeforePartyMarker(beforeLine)
                && !IsBeforePartyMarker2(beforeLine)
                && !(successive && IsBeforePartyMarker3(beforeLine))))
        {
            return false;
        }

        List<WLine> result = [beforeLine];

        if (blockCursor.NextLineMatches(inPrivate =>
                inPrivate.NormalizedContent.Equals("IN PRIVATE", StringComparison.OrdinalIgnoreCase)))
        {
            // EWHC/Admin/2012/2822
            result.Add(blockCursor.ReadNextLine());
        }

        if (blockCursor.NextLineMatches(IsBeforePartyMarker2))
        {
            result.Add(blockCursor.ReadNextLine());
        }

        if (!TryEnrichPartyNamesWithRoleLabel(blockCursor.PeekRemaining(), out var firstGroupOfParites))
        {
            return false;
        }
        result.AddRange(firstGroupOfParites);
        blockCursor.AdvanceCursor(firstGroupOfParites.Length);

        /* no "v" or "and" in EWHC/Comm/2013/3920 */
        if (blockCursor.NextLineMatches(l => IsV(l) || IsAnd(l)))
        {
            result.Add(blockCursor.ReadNextLine());
        }

        if (!TryEnrichPartyNamesWithRoleLabel(blockCursor.PeekRemaining(), out var secondGroupOfParites))
        {
            return false;
        }
        result.AddRange(secondGroupOfParites);
        blockCursor.AdvanceCursor(secondGroupOfParites.Length);

        if (blockCursor.NextLineMatches(IsAnd))
        {
            result.Add(blockCursor.ReadNextLine());
        }

        if (TryEnrichPartyNamesWithRoleLabel(blockCursor.PeekRemaining(), out var thirdGroupOfParites))
        {
            result.AddRange(thirdGroupOfParites);
            blockCursor.AdvanceCursor(thirdGroupOfParites.Length);
        }

        if (TryEnrichPartyNamesWithRoleLabel(blockCursor.PeekRemaining(), out var fourthGroupOfParites))
        {
            result.AddRange(fourthGroupOfParites);
            blockCursor.AdvanceCursor(fourthGroupOfParites.Length);
        }

        if (blockCursor.NextLineMatches(IsAfterPartyMarker))
        {
            result.Add(blockCursor.ReadNextLine());
            enriched = result.ToArray();
            return true;
        }

        if (TryEnrichMultiLinePartyBlock(blockCursor.PeekRemaining(), true, out var another))
        {
            result.AddRange(another);
        }

        enriched = result.ToArray();
        return true;
    }

    private static bool TryEnrichMultiLinePartyBlockWithInlineRoles(IBlock[] rest, out WLine[] enriched)
    {
        // EWHC/Admin/2018/3311
        enriched = null;
        var blockCursor = new BlockCursor(rest);

        if (!blockCursor.NextLineMatches(l => IsBeforePartyMarker(l) || IsBeforePartyMarker2(l)))
        {
            return false;
        }
        List<WLine> result = [blockCursor.ReadNextLine()];

        if (blockCursor.NextLineMatches(IsBeforePartyMarker2))
        {
            result.Add(blockCursor.ReadNextLine());
        }

        if (!blockCursor.TryReadNextLine(out var partyLine1) || !TryMakePartyAndRole(partyLine1, out var party1))
        {
            return false;
        }
        result.Add(party1);

        if (!blockCursor.NextLineMatches(l => IsV(l) || IsAnd(l)))
        {
            return false;
        }
        result.Add(blockCursor.ReadNextLine());

        if (!blockCursor.TryReadNextLine(out var partyLine2) || !TryMakePartyAndRole(partyLine2, out var party2))
        {
            return false;
        }
        result.Add(party2);

        if (!blockCursor.NextLineMatches(IsAfterPartyMarker))
        {
            return false;
        }
        result.Add(blockCursor.ReadNextLine());

        enriched = result.ToArray();
        return true;
    }

    /* this one has two types of parties before the v */
    private static bool TryEnrichMultiLinePartyBlockWithTwoGroupsBeforeV(IBlock[] rest, out WLine[] enriched)
    {
        // EWHC/Admin/2015/897
        enriched = null;
        var blockCursor = new BlockCursor(rest);

        if (!blockCursor.NextLineMatches(IsBeforePartyMarker))
        {
            return false;
        }

        List<WLine> result = [blockCursor.ReadNextLine()];

        /* between */
        if (!blockCursor.NextLineMatches(IsBeforePartyMarker2))
        {
            return false;
        }
        result.Add(blockCursor.ReadNextLine());

        if (!TryEnrichPartyNamesWithRoleLabel(blockCursor.PeekRemaining(), out var firstGroupOfParites))
        {
            return false;
        }
        result.AddRange(firstGroupOfParites);
        blockCursor.AdvanceCursor(firstGroupOfParites.Length);

        /* and */
        if (!blockCursor.NextLineMatches(IsAnd))
        {
            return false;
        }
        result.Add(blockCursor.ReadNextLine());

        if (!TryEnrichPartyNamesWithRoleLabel(blockCursor.PeekRemaining(), out var secondGroupOfParites))
        {
            return false;
        }
        result.AddRange(secondGroupOfParites);
        blockCursor.AdvanceCursor(secondGroupOfParites.Length);

        /* v */
        if (!blockCursor.NextLineMatches(IsV))
        {
            return false;
        }
        result.Add(blockCursor.ReadNextLine());

        if (!TryEnrichPartyNamesWithRoleLabel(blockCursor.PeekRemaining(), out var thirdGroupOfParites))
        {
            return false;
        }
        result.AddRange(thirdGroupOfParites);
        blockCursor.AdvanceCursor(thirdGroupOfParites.Length);

        if (!blockCursor.NextLineMatches(IsAfterPartyMarker))
        {
            return false;
        }
        result.Add(blockCursor.ReadNextLine());

        enriched = result.ToArray();
        return true;
    }

    private static bool TryEnrichPartyNamesWithRoleLabel(IBlock[] inputBlocks, out WLine[] enriched)
    {
        if (inputBlocks.Length == 0 || inputBlocks[0] is not WLine firstPartyLine || !IsPartyName(firstPartyLine))
        {
            enriched = null;
            return false;
        }

        List<WLine> foundPartyNames = [firstPartyLine];
        List<WLine> linesNotToEnrich = [];
        foreach (var block in inputBlocks.Skip(1))
        {
            switch (block)
            {
                case WLine line when TryGetSinglePartyRole(line.NormalizedContent, out var role):
                    {
                        enriched =
                        [
                            .. foundPartyNames.Select(l => MakeParty(l, role)),
                            .. linesNotToEnrich,
                            MakeRole(line, role)
                        ];
                        return true;
                    }
                case WLine line when IsPartyName(line):
                    {
                        foundPartyNames.Add(line);
                        break;
                    }
                case WLine line when IsAnonymityDirection(line):
                    {
                        linesNotToEnrich.Add(line);
                        break;
                    }
                default:
                    {
                        enriched = null;
                        return false;
                    }
            }
        }

        enriched = null;
        return false;
    }

    private static bool IsBeforePartyMarker(WLine line)
    {
        var normalized = line.NormalizedContent;
        if (Regex.IsMatch(normalized, @"^-( -)+$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^-+$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^_+$"))
        {
            return true;
        }

        return false;
    }

    private static bool IsBeforePartyMarker2(WLine line)
    {
        var normalized = line.NormalizedContent;
        normalized = Regex.Replace(normalized, @"\s+", "").TrimEnd(':', '-');
        if (normalized.Equals("BETWEEN", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsBeforePartyMarker3(WLine line)
    {
        var normalized = line.NormalizedContent;
        normalized = Regex.Replace(normalized, @"\s+", "").TrimEnd(':');
        if (normalized.Equals("AND BETWEEN", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsInTheMatterOfSomething(WCell cell)
    {
        return cell.Contents.ToArray() is [WLine line] && IsInTheMatterOfSomething(line);
    }

    private static bool IsInTheMatterOfSomething(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length != 1)
        {
            return false;
        }

        if (lineContents[0] is not WText wText)
        {
            return false;
        }

        if (Regex.IsMatch(wText.Text, "^IN THE MATTER OF [A-Z]", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(wText.Text, "^RE: [A-Z]", RegexOptions.IgnoreCase)) // EWCA/Crim/2007/14
        {
            return true;
        }

        return false;
    }

    private static bool IsInTheMatterOf1(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length == 1)
        {
            if (lineContents[0] is not WText wText)
            {
                return false;
            }

            return Regex.IsMatch(wText.Text.Trim(), "IN THE MATTER OF", RegexOptions.IgnoreCase);
        }

        if (lineContents.Length == 3)
        {
            // EWCA/Civ/2008/1303
            if (lineContents[0] is not WText wText)
            {
                return false;
            }

            if (lineContents[1] is not WLineBreak)
            {
                return false;
            }

            return Regex.IsMatch(wText.Text.Trim(), "IN THE MATTER OF", RegexOptions.IgnoreCase);
        }

        return false;
    }

    private static bool IsInTheMatterOf2(WLine line)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length != 1)
        {
            return false;
        }

        if (lineContents[0] is not WText)
        {
            return false;
        }

        return true;
    }

    private static WLine MakeDocTitle(WLine line)
    {
        var lineContents = line.Contents.ToArray();

        var docTitle = new WDocTitle((WText)lineContents[0]);
        var contents = lineContents.Skip(1).Prepend(docTitle);

        return WLine.Make(line, contents);
    }

    private static bool IsPartyName(WLine line)
    {
        if (IsBeforePartyMarker(line)
            || IsBeforePartyMarker2(line)
            || IsV(line)
            || IsAnd(line)
            || IsAfterPartyMarker(line)
            || IsPartyRole(line)
            || IsAnonymityDirection(line)
           )
        {
            return false;
        }

        var lineContents = line.Contents.ToArray();
        return lineContents.All(inline => inline is ITextOrWhitespace) &&
            lineContents.Any(inline => inline is WText wText && IsNotBlank(wText));
    }

    private static bool IsAnonymityDirection(WLine line)
    {
        return line.NormalizedContent.Contains("ANONYMITY DIRECTION", StringComparison.OrdinalIgnoreCase);
    }

    private static WLine MakeParty(WLine line, PartyRole? role)
    {
        var lineContents = line.Contents.ToArray();
        if (lineContents.Length == 1)
        {
            var text = (WText)lineContents[0];
            var party = new WParty(text) { Role = role };
            // use MakeOrSplitParty
            return WLine.Make(line, [party]);
        }

        if (lineContents.All(inline => inline is WText) &&
            lineContents.Cast<WText>().Any(IsNotBlank))
        {
            var party = new WParty2(lineContents.Cast<WText>()) { Role = role };
            return WLine.Make(line, [party]);
        }

        if (lineContents.All(inline => inline is ITextOrWhitespace) &&
            lineContents.Any(inline => inline is WText wText && IsNotBlank(wText)))
        {
            var before = lineContents.TakeWhile(inline => inline is not IFormattedText).ToArray();
            var main = lineContents.Skip(before.Length);
            var party = new WParty2(main.Cast<ITextOrWhitespace>()) { Role = role };
            var contents = before.Append(party);
            return WLine.Make(line, contents);
        }

        if (lineContents.Length == 2)
        {
            if (string.IsNullOrWhiteSpace(((WText)lineContents[1]).Text))
            {
                var party = new WParty((WText)lineContents[0]) { Role = role };
                return WLine.Make(line, [party, lineContents[1]]);
            }
            else
            {
                var party = new WParty((WText)lineContents[1]) { Role = role };
                return WLine.Make(line, [lineContents[0], party]);
            }
        }

        if (lineContents.Length == 3)
        {
            var party = new WParty2(lineContents.Cast<IFormattedText>()) { Role = role };
            return WLine.Make(line, [party]);
        }

        if (lineContents.Length == 4)
        {
            var first = (WTab)lineContents[0];
            var party = new WParty2(lineContents.Skip(1).Cast<IFormattedText>()) { Role = role };
            return WLine.Make(line, [first, party]);
        }

        throw new EnricherException($"Couldn't make {role} party for line {line}");
    }

    private static WLine MakeRole(WLine line, PartyRole role)
    {
        return WLine.Make(line, [new WRole { Role = role, Contents = line.Contents }]);
    }

    private static readonly Dictionary<string, PartyRole> PartyRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Appellant"] = PartyRole.Appellant, // EWCA/Civ/2003/1686
        ["Appellants"] = PartyRole.Appellant,

        ["Applicant"] = PartyRole.Applicant,
        ["Applicants"] = PartyRole.Applicant,
        ["Counterclaimant"] = PartyRole.Claimant,
        ["Defendant to Counterclaim"] = PartyRole.Claimant,
        ["Claimant"] = PartyRole.Claimant,
        ["Claimants"] = PartyRole.Claimant,
        ["Clamaint"] = PartyRole.Claimant,
        ["Clamaints"] = PartyRole.Claimant,

        ["Defendant"] = PartyRole.Defendant,
        ["Defendants"] = PartyRole.Defendant,
        ["DEFENDANT’S SOLICITOR"] = PartyRole.Defendant, // EWCA/Civ/2006/1032

        ["Interested Parties"] = PartyRole.InterestedParty,
        ["Interested Party"] = PartyRole.InterestedParty,

        ["Intervener"] = PartyRole.Intervener,
        ["Interveners"] = PartyRole.Intervener,

        ["Petitioner"] = PartyRole.Petitioner,
        ["Petitioners"] = PartyRole.Petitioner,

        ["requested person"] = PartyRole.RequestedPerson, // [2022] EWHC 273 (Admin)
        ["requested persons"] = PartyRole.RequestedPerson, // [2022] EWHC 273 (Admin)

        ["requesting state"] = PartyRole.RequestingState,

        ["Respond-ent"] = PartyRole.Respondent,
        ["Respond-ents"] = PartyRole.Respondent,
        ["Respondent"] = PartyRole.Respondent, // EWCA/Civ/2003/1686
        ["Respondents Second and Third"] = PartyRole.Respondent,
        ["Respondents"] = PartyRole.Respondent,
        ["Respondnet"] = PartyRole.Respondent, // EWHC/Admin/2010/3393
        ["Respondnets"] = PartyRole.Respondent,

        ["Third Party"] = PartyRole.ThirdParty
    };

    private static bool IsPartyRole(WLine line)
    {
        return TryGetSinglePartyRole(line.NormalizedContent, out _);
    }

    private static bool TryMakePartyAndRole(WLine line, out WLine enriched)
    {
        if (line.Contents.ToArray() is [.. var startingTabs, WText partyNameText, WTab tab, WText roleText]
            && startingTabs.All(l => l is WTab)
            && TryGetSinglePartyRole(roleText.Text, out var role))
        {
            var contents = startingTabs.Concat(
            [
                new WParty(partyNameText.Text, partyNameText.properties) { Role = role },
                tab,
                new WRole { Role = role, Contents = [roleText] }
            ]);

            enriched = WLine.Make(line, contents);
            return true;
        }

        enriched = null;
        return false;
    }

    private static bool IsAfterPartyMarker(WLine line)
    {
        if (IsBeforePartyMarker(line))
        {
            return true;
        }

        var content = line.NormalizedContent;
        return content.StartsWith("Computer Aided Transcript", StringComparison.OrdinalIgnoreCase)
            || content.StartsWith("REPORTING RESTRICTIONS APPLY:", StringComparison.OrdinalIgnoreCase);
    }

    /* tables */

    private WTable EnrichTable(WTable table)
    {
        IEnumerable<WRow> rows = null;
        if (table.TypedRows.Count == 3 && TryEnrichThreeRowsWithNoRoles(table.TypedRows, out var threeRows))
        {
            rows = threeRows;
        }

        rows ??= EnrichRows(table.TypedRows);

        return new WTable(table.Main, table.Properties, table.Grid, rows);
    }

    private List<WRow> EnrichRows(List<WRow> originalRows)
    {
        var enrichedRows = new List<WRow>();
        for (var i = 0; i < originalRows.Count; i++)
        {
            var enrichedRow = EnrichRow(originalRows[i]);

            if (originalRows[i] == enrichedRow && i != originalRows.Count - 1)
            {
                enrichedRow = EnrichRowWithPartyRoleFromNextRow(originalRows[i], originalRows[i + 1]);
            }

            enrichedRows.Add(enrichedRow);
        }

        return enrichedRows;
    }

    private static bool IsCellWithContent(ICell cell)
    {
        return !IsEmptyCell(cell);
    }

    private static bool IsEmptyCell(ICell cell)
    {
        return cell.Contents.All(block => block is WLine line && IsEmptyLine(line));
    }

    private static bool IsEmptyLine(WLine line)
    {
        return string.IsNullOrWhiteSpace(line.NormalizedContent);
    }

    private static bool IsLineWithContent(WLine l)
    {
        return !IsEmptyLine(l);
    }

    private WRow EnrichRow(WRow row)
    {
        var rowCells = row.Cells.ToArray();
        if (rowCells.Length == 2)
        {
            return EnrichTwoCellRow(row);
        }

        if (rowCells is not [WCell first, WCell second, WCell third] || IsCellWithContent(first))
        {
            return row;
        }

        if (TryGetSinglePartyRole(third, out var role))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                EnrichCellWithParty(second, role),
                EnrichCellWithPartyRole(third, role)
            ]);
        }

        if (IsInTheMatterOfSomething(second))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                EnrichInTheMatterOfSomething(second),
                third
            ]);
        }

        if (TryGetTwoDifferentRoles(third, out var twoRoles))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                first,
                EnrichPartyNamesWithTwoRoles(second, twoRoles),
                EnrichPartyTypesWithTwoRoles(third, twoRoles)
            ]);
        }

        return row;
    }

    private static WRow EnrichTwoCellRow(WRow row)
    {
        var rowCells = row.Cells.ToArray();
        var first = (WCell)rowCells[0];
        var second = (WCell)rowCells[1];

        if (IsCellWithContent(first) && TryGetSinglePartyRole(second, out var role))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                EnrichCellWithParty(first, role),
                EnrichCellWithPartyRole(second, role)
            ]);
        }

        return row;

    }

    private static bool TryEnrichThreeRowsWithNoRoles(List<WRow> rows, out WRow[] enrichedRows)
    {
        if (rows is [var firstRow, var secondRow, var thirdRow]
            && TryEnrichRowWithRole(firstRow, PartyRole.BeforeTheV, out var firstEnriched)
            && IsNotPartyRow(secondRow)
            && TryEnrichRowWithRole(thirdRow, PartyRole.AfterTheV, out var thirdEnriched))
        {
            enrichedRows =
            [
                firstEnriched,
                secondRow,
                thirdEnriched
            ];
            return true;
        }

        enrichedRows = null;
        return false;
    }

    private static bool IsNotPartyRow(WRow row)
    {
        return row.TypedCells.ToArray() is [var firstCell, var secondCell, var thirdCell]
            && IsEmptyCell(firstCell)
            && IsEmptyCell(thirdCell)
            && secondCell.Contents.All(block => block is WLine line && !IsPartyName(line) && !IsPartyRole(line))
            && secondCell.Contents.OfType<WLine>().Count(IsLineWithContent) == 1;
    }

    private static bool TryEnrichRowWithRole(WRow wRow, PartyRole role, out WRow enrichedRow)
    {
        if (wRow.TypedCells is [var firstCell, var middleCell, var lastCell]
            && IsEmptyCell(firstCell)
            && IsEmptyCell(lastCell)
            && middleCell.Contents.All(block => block is WLine line && (IsEmptyLine(line) || IsPartyName(line))))
        {
            var enrichedContents = middleCell.Contents.Cast<WLine>()
                                             .Select(line => IsEmptyLine(line) ? line : MakeParty(line, role))
                                             .ToArray();

            enrichedRow = new WRow(
                wRow.Table,
                wRow.TablePropertyExceptions,
                wRow.Properties,
                [
                    firstCell,
                    new WCell(middleCell.Row, middleCell.Props, enrichedContents),
                    lastCell
                ]);
            return true;
        }

        enrichedRow = null;
        return false;
    }

    private static WRow EnrichRowWithPartyRoleFromNextRow(WRow row, WRow next)
    {
        if (row.Cells.ToArray() is [WCell thisRowFirstCell, WCell thisRowMiddleCell, WCell thisRowLastCell]
            && next.Cells.ToArray() is [WCell nextRowFirstCell, WCell nextRowMiddleCell, WCell nextRowLastCell]
            && IsEmptyCell(thisRowFirstCell) && IsCellWithContent(thisRowMiddleCell) && IsEmptyCell(thisRowLastCell)
            && IsEmptyCell(nextRowFirstCell) && IsEmptyCell(nextRowMiddleCell) && IsCellWithContent(nextRowLastCell)
            && TryGetSinglePartyRole(nextRowLastCell, out var role))
        {
            return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            [
                thisRowFirstCell,
                EnrichCellWithParty(thisRowMiddleCell, role),
                thisRowLastCell
            ]);
        }

        return row;
    }

    internal static bool TryGetSinglePartyRole(WCell cell, out PartyRole role)
    {
        var lineContents = cell.Contents
                               .OfType<WLine>()
                               .Where(IsLineWithContent)
                               .Select(l => l.NormalizedContent)
                               .ToArray();
        return TryGetSinglePartyRole(lineContents, out role);
    }

    internal static bool TryGetSinglePartyRole(string inputRoleStrings, out PartyRole role)
    {
        return TryGetSinglePartyRole([inputRoleStrings], out role);
    }

    internal static bool TryGetSinglePartyRole(string[] inputRoleStrings, out PartyRole role)
    {
        if (!TryGetPartyRoleParts(inputRoleStrings, out var roleParts))
        {
            role = default;
            return false;
        }

        bool AllRolesAre(PartyRole role) => roleParts.All(r => r == role);
        bool OneRoleIs(PartyRole role) => roleParts.Any(r => r == role);

        PartyRole? result = roleParts switch
        {
            [var partyRole] => partyRole,
            [var partyRole, ..] when AllRolesAre(partyRole) => partyRole, // All roles are the same

            [.., PartyRole.ThirdParty or PartyRole.InterestedParty] => null,

            [PartyRole.Appellant, PartyRole.Respondent] => PartyRole.Respondent, // [2020] EWHC 3409 (QB)
            [PartyRole.Respondent, PartyRole.Appellant] => PartyRole.Appellant, // [2021] EWCA Civ 1961

            [PartyRole.Respondent, PartyRole.Applicant] => PartyRole.Applicant,

            { Length: 2 } when OneRoleIs(PartyRole.Appellant) => PartyRole.Appellant,
            { Length: 2 } when OneRoleIs(PartyRole.Respondent) => PartyRole.Respondent,

            [PartyRole.Claimant, PartyRole.Defendant] => PartyRole.Claimant,

            [PartyRole.Applicant, PartyRole.Defendant] => PartyRole.Defendant,
            [PartyRole.Defendant, PartyRole.Applicant] => PartyRole.Applicant, // [2019] EWHC 3963 (QB)

            { Length: 2 } when OneRoleIs(PartyRole.Defendant) => PartyRole.Defendant,
            { Length: 2 } when OneRoleIs(PartyRole.Applicant) => PartyRole.Applicant,

            [PartyRole.Defendant, PartyRole.Claimant, PartyRole.Appellant] => PartyRole.Appellant,
            [PartyRole.Respondent, PartyRole.Appellant, PartyRole.Respondent] => PartyRole.Respondent,

            _ => null
        };

        if (result.HasValue)
        {
            role = result.Value;
            return true;
        }

        role = default;
        return false;
    }

    private static bool TryGetPartyRoleParts(string[] inputRoleStrings, out PartyRole[] roleParts)
    {
        var parts = inputRoleStrings
                    .SelectMany(s => s.Split('/',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .ToArray();
        if (parts.Length < 2)
        {
            parts = inputRoleStrings
                    .SelectMany(s => s.Split(" and ",
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .ToArray();
        }

        var cleanedParts = parts.Select(p => p.CleanWhitespace().Trim('(', ')').Trim());
        var cleanedPartsWithoutPrefixes = cleanedParts.Select(StripRolePrefix).ToArray();

        if (!cleanedPartsWithoutPrefixes.All(PartyRoles.ContainsKey))
        {
            roleParts = null;
            return false;
        }

        roleParts = cleanedPartsWithoutPrefixes.Select(p => PartyRoles[p]).ToArray();
        return true;
    }

    private static string StripRolePrefix(string s)
    {
        if (s.Equals("Third Party", StringComparison.OrdinalIgnoreCase))
        {
            return s;
        }

        foreach (var prefix in
                 PrefixesToStrip.Where(prefix => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            s = s.Remove(0, prefix.Length).Trim();
        }

        s = Regex.Replace(s, @"^\d+(st|nd|rd|th)", "", RegexOptions.IgnoreCase).Trim();

        return s;
    }

    private static readonly HashSet<string> PrefixesToStrip =
    [
        "First",
        "Second",
        "Third",
        "Fourth",
        "Fifth",
        "Sixth",

        "Additional",
        "Inquiry", // [2022] EWHC 189 (Pat)
        "Part 20"
    ];

    private static bool TryGetTwoDifferentRoles(WCell cell, out (PartyRole first, PartyRole second) roles)
    {
        var linesWithContent = cell.Contents.OfType<WLine>().Where(IsLineWithContent).ToArray();
        if (linesWithContent.Length == 2
            && TryGetSinglePartyRole(linesWithContent[0].NormalizedContent, out var role1)
            && TryGetSinglePartyRole(linesWithContent[1].NormalizedContent, out var role2)
            && role1 != role2)
        {
            roles = (role1, role2);
            return true;
        }

        roles = default;
        return false;
    }

    private static WCell EnrichCellWithPartyRole(WCell cell, PartyRole role)
    {
        return new WCell(cell.Row, cell.Props, cell.Contents.Cast<WLine>()
                                                   .Select(line => IsEmptyLine(line)
                                                       ? line
                                                       : WLine.Make(line,
                                                       [
                                                           new WRole { Role = role, Contents = line.Contents }
                                                       ])));
    }

    private static IBlock EnrichBlockWithParty(IBlock block, PartyRole role)
    {
        return block switch
        {
            WOldNumberedParagraph p => EnrichOldNumberedParagraphWithParty(p, role),
            WLine line => EnrichLineWithParty(line, role),
            _ => block
        };
    }

    private static WCell EnrichCellWithParty(WCell cell, PartyRole role)
    {
        var contents = cell.Contents
                           .Select(block => EnrichBlockWithParty(block, role))
                           .ToArray();

        return new WCell(cell.Row, cell.Props, contents);
    }

    private static WOldNumberedParagraph EnrichOldNumberedParagraphWithParty(WOldNumberedParagraph paragraph,
        PartyRole role)
    {
        if (paragraph.Contents.ToArray() is [WText wText])
        {
            return new WOldNumberedParagraph(paragraph, [new WParty(wText) { Role = role }]); // EWCA/Civ/2015/455
        }

        return paragraph;
    }

    private static WLine EnrichLineWithParty(WLine line, PartyRole role)
    {
        var lineContents = line.Contents.ToArray();

        static bool IsPartOfPartyName(WText wText)
            => IsNotBlank(wText)
            && !IsInBrackets(wText.Text)
            && !IsConnectorText(wText.Text);

        return lineContents.OfType<WText>().Count(IsPartOfPartyName) switch
        {
            1 => WLine.Make(line, lineContents.SelectMany(inline => EnrichWTextWithParties(inline, role)).ToArray()),
            > 1 => WLine.Make(line, [new WParty2(lineContents.Cast<ITextOrWhitespace>()) { Role = role }]),
            _ => line
        };
    }

    private static IEnumerable<IInline> EnrichWTextWithParties(IInline inline, PartyRole role)
    {
        if (inline is not WText text)
        {
            return [inline];
        }

        // Is this a case of two party names in one line - ewhc/admin/2022/273
        if (text.Text.StartsWith("(1)") && text.Text.Contains("(2)"))
        {
            var i = text.Text.IndexOf("(2)", StringComparison.Ordinal);
            return
            [
                new WParty(text.Text[..i], text.properties) { Role = role },
                new WParty(text.Text[i..], text.properties) { Role = role }
            ];
        }

        // Make sure this is the wText with a party name in it rather than some connection or descriptive text
        if (IsNotBlank(text) && !IsConnectorText(text.Text) && !IsInBrackets(text.Text))
        {
            return [new WParty(text.Text, text.properties) { Role = role }];
        }

        return [inline];
    }

    private static bool IsNotBlank(WText wText)
    {
        return !IsBlank(wText);
    }

    private static bool IsBlank(WText wText)
    {
        return string.IsNullOrWhiteSpace(wText.Text);
    }

    /// <summary>
    /// Returns true if this is a "v" string
    /// Trims ' ', '-', '–' characters and uses case insensitive comparison
    /// </summary>
    private static bool IsV(WLine line)
    {
        return line.NormalizedContent.Trim(' ', '-', '–').Equals("v", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Returns true if this is an "and" string
    /// Trims ' ', '-', '–' characters and uses case insensitive comparison
    /// </summary>
    private static bool IsAnd(string s)
    {
        return s.Trim(' ', '-', '–').Equals("and", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Returns true if this line only contains an "and" string
    /// </summary>
    private static bool IsAnd(WLine line)
    {
        return IsAnd(line.NormalizedContent);
    }

    /// <summary>
    /// Returns true if this is a string enclosed in brackets unless there are nested brackets
    /// "(some string in brackets)   " => true
    /// "(3) Appellant CAKE (Cats Against Kipper Exploitation)" => false
    /// </summary>
    private static bool IsInBrackets(string s)
    {
        return Regex.IsMatch(s, @"^\s*\([^()]+\)\s*$", RegexOptions.IgnoreCase);
    }

    private static bool IsConnectorText(string s)
    {
        return Regex.IsMatch(s, @"^(\s|_|-|–|\d|\.|\+|&|and)*$", RegexOptions.IgnoreCase);
    }

    private WCell EnrichPartyNamesWithTwoRoles(WCell cell, (PartyRole first, PartyRole second) roles)
    {
        if (cell.Contents.Any(b => b is not WLine))
        {
            return cell;
        }

        var contents = new List<IBlock>();
        var firstPartyFound = false;
        var andFound = false;
        var secondPartyFound = false;

        foreach (var line in cell.Contents.Cast<WLine>())
        {
            if (IsEmptyLine(line))
            {
                contents.Add(line);
                continue;
            }

            switch (line.Contents.ToArray())
            {
                case [WText wText] when IsBlank(wText) || IsInBrackets(wText.Text):
                    contents.Add(line);
                    break;

                case [.. { Length: 0 or 1 }, WText wText] when IsAnd(wText.Text):
                    andFound = true;
                    contents.Add(line);
                    break;

                case [WText wText] when firstPartyFound && andFound:
                    secondPartyFound = true;
                    contents.Add(WLine.Make(line, [new WParty(wText) { Role = roles.second }]));
                    break;

                case [WText wText]:
                    firstPartyFound = true;
                    contents.Add(WLine.Make(line, [new WParty(wText) { Role = roles.first }]));
                    break;

                case [WText wText1, WText wText2]
                    when IsNotBlank(wText1) || IsBlank(wText2) || IsInBrackets(wText2.Text):
                    contents.Add(line);
                    break;

                case [WText wText1, WText wText2] when andFound:
                    secondPartyFound = true;
                    contents.Add(WLine.Make(line, [wText1, new WParty(wText2) { Role = roles.second }]));
                    break;

                case [WText wText1, WText wText2]:
                    firstPartyFound = true;
                    contents.Add(WLine.Make(line, [wText1, new WParty(wText2) { Role = roles.first }]));
                    break;

                default:
                    contents.Add(line);
                    break;
            }
        }

        if (firstPartyFound && andFound && secondPartyFound)
        {
            return new WCell(cell.Row, cell.Props, contents);
        }

        return cell;
    }

    private WCell EnrichPartyTypesWithTwoRoles(WCell cell, (PartyRole first, PartyRole second) roles)
    {
        if (cell.Contents.Any(block => block is not WLine))
            return cell;

        var contents = new List<IBlock>();
        var firstPartyFound = false;
        var emptyAfterFirstFound = false;

        foreach (var line in cell.Contents.Cast<WLine>())
        {
            if (IsEmptyLine(line) && firstPartyFound)
            {
                emptyAfterFirstFound = true;
                contents.Add(line);
            }
            else if (IsEmptyLine(line))
            {
                contents.Add(line);
            }
            else if (emptyAfterFirstFound)
            {
                contents.Add(WLine.Make(line, [new WRole { Contents = line.Contents, Role = roles.second }]));
            }
            else
            {
                firstPartyFound = true;
                contents.Add(WLine.Make(line, [new WRole { Contents = line.Contents, Role = roles.first }]));
            }
        }

        if (emptyAfterFirstFound)
        {
            return new WCell(cell.Row, cell.Props, contents);
        }

        return cell;
    }

    private static WCell EnrichInTheMatterOfSomething(WCell cell)
    {
        var line = MakeDocTitle((WLine)cell.Contents.First());
        return new WCell(cell.Row, cell.Props, [line]);
    }

    private static WLine EnrichLineWithDocTitle(WLine line)
    {
        return line.Contents.ToArray() switch
        {
            [WText wText] when wText.Text.StartsWith("IN THE MATTER OF ", StringComparison.InvariantCultureIgnoreCase)
                => WLine.Make(line, [new WDocTitle(wText)]),
            _ => line
        };
    }
}
