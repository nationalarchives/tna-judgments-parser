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
            && IsBetweenPartyMarker(line2)
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
            && IsBetweenPartyMarker(line2)
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
            && (IsBetweenPartyMarker(line3) || IsBetweenPartyMarker2(line3))
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
        if (blockCursor.NextLineMatches(l => IsBetweenPartyMarker(l) || IsBetweenPartyMarker2(l)))
        {
            result.Add(blockCursor.ReadNextLine());
        }

        if (!TryEnrichPartyNamesWithRoleLabel(blockCursor.PeekRemaining(), out var secondGroupOfParites))
        {
            return false;
        }
        result.AddRange(secondGroupOfParites);
        blockCursor.AdvanceCursor(secondGroupOfParites.Length);

        if (blockCursor.NextLineMatches(IsBetweenPartyMarker2))
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

        if (!blockCursor.NextLineMatches(l => IsBetweenPartyMarker(l) || IsBetweenPartyMarker2(l)))
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
        if (!blockCursor.NextLineMatches(IsBetweenPartyMarker2))
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
        if (!blockCursor.NextLineMatches(IsBetweenPartyMarker))
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
        foreach (var block in inputBlocks.Skip(1))
        {
            switch (block)
            {
                case WLine line when TryGetSinglePartyRole(line.NormalizedContent, out var role):
                    {
                        enriched =
                        [
                            .. foundPartyNames.Select(l => MakeParty(l, role)),
                            MakeRole(line, role)
                        ];
                        return true;
                    }
                case WLine line when IsPartyName(line):
                    {
                        foundPartyNames.Add(line);
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
            || IsBetweenPartyMarker(line)
            || IsBetweenPartyMarker2(line)
            || IsAfterPartyMarker(line)
            || IsPartyRole(line))
        {
            return false;
        }

        var lineContents = line.Contents.ToArray();
        if (lineContents.Length == 0)
        {
            return false;
        }

        if (lineContents.All(inline => inline is WText) &&
            lineContents.Cast<WText>().Any(IsNotBlank))
        {
            return true;
        }

        if (lineContents.All(inline => inline is ITextOrWhitespace) &&
            lineContents.Any(inline => inline is WText wText && IsNotBlank(wText)))
        {
            return true;
        }

        if (lineContents.Length == 1)
        {
            if (lineContents[0] is not WText wText1)
            {
                return false;
            }

            if (IsBlank(wText1))
            {
                return false;
            }

            return true;
        }

        if (lineContents.Length == 2)
        {
            if (lineContents[0] is WTab && lineContents[1] is WText wText2 &&
                IsNotBlank(wText2)) // EWHC/Fam/2017/3707
            {
                return true;
            }

            if (lineContents[0] is WText wText3 && lineContents[1] is WText wText4 &&
                IsNotBlank(wText3) &&
                IsBlank(wText4)) // EWCA/Crim/2014/465
            {
                return true;
            }

            if (lineContents[0] is WText wText5 && lineContents[1] is WText wText6 &&
                Regex.IsMatch(wText5.Text, @"^\(\d\) +$") &&
                IsNotBlank(wText6))
            {
                return true;
            }

            if (lineContents[0] is WText wText7 && lineContents[1] is WText wText8 &&
                Regex.IsMatch(wText7.Text, @"^\d\. +$") &&
                IsNotBlank(wText8)) // EWCA/Civ/2004/993
            {
                return true;
            }

            return false;
        }

        if (lineContents.Length == 3)
        {
            // EWHC/Admin/2012/3928, EWHC/Admin/2007/552
            if (lineContents[0] is not WText)
            {
                return false;
            }

            if (lineContents[1] is not WText wText2)
            {
                return false;
            }

            if (lineContents[2] is not WText)
            {
                return false;
            }

            if (IsNotBlank(wText2))
            {
                return false;
            }

            return true; // not same formatting in EWHC/Admin/2004/1823
        }

        if (lineContents.Length == 4)
        {
            // EWHC/Fam/2017/3707
            if (lineContents[0] is not WTab)
            {
                return false;
            }

            if (lineContents[1] is not WText)
            {
                return false;
            }

            if (lineContents[2] is not WText wText2)
            {
                return false;
            }

            if (IsNotBlank(wText2))
            {
                return false;
            }

            return true;
        }

        return false;
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

        throw new Exception();
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

    private static bool IsBetweenPartyMarker(WLine line)
    {
        var normalized = line.NormalizedContent;
        return IsV(normalized);
    }

    private static bool IsBetweenPartyMarker2(WLine line)
    {
        var normalized = line.NormalizedContent;
        return IsAnd(normalized);
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

    private static bool LineHasContent(WLine l)
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

    private WRow EnrichTwoCellRow(WRow row)
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

    private bool TryEnrichThreeRowsWithNoRoles(List<WRow> rows, out WRow[] enrichedRows)
    {
        // EWCA/Crim/2007/854, EWCA/Crim/2014/465
        enrichedRows = null;
        var firstRowCells = rows[0].Cells.ToArray();
        if (firstRowCells.Length != 3)
        {
            return false;
        }

        var secondRowCells = rows[1].Cells.ToArray();
        if (secondRowCells.Length != 3)
        {
            return false;
        }

        var thirdRowCells = rows[2].Cells.ToArray();
        if (thirdRowCells.Length != 3)
        {
            return false;
        }

        if (IsCellWithContent(firstRowCells[0]))
        {
            return false;
        }

        if (IsCellWithContent(firstRowCells[^1]))
        {
            return false;
        }

        if (IsCellWithContent(secondRowCells[0]))
        {
            return false;
        }

        if (IsCellWithContent(secondRowCells[^1]))
        {
            return false;
        }

        if (IsCellWithContent(thirdRowCells[0]))
        {
            return false;
        }

        if (IsCellWithContent(thirdRowCells[^1]))
        {
            return false;
        }

        var middle1 = rows[0].TypedCells[1];
        var middle2 = rows[1].TypedCells[1];
        var middle3 = rows[2].TypedCells[1];
        if (!middle1.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || (IsPartyName(line) && !IsPartyRole(line)))))
        {
            return false;
        }

        if (!middle2.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || IsBetweenPartyMarker(line) ||
                                            IsBetweenPartyMarker2(line))))
        {
            return false;
        }

        if (middle2.Contents.OfType<WLine>().Count(LineHasContent) != 1)
        {
            return false;
        }

        if (!middle3.Contents.All(block => block is WLine line &&
                                           (IsEmptyLine(line) || (IsPartyName(line) && !IsPartyRole(line)))))
        {
            return false;
        }

        var newMiddle1 = new WCell(middle1.Row, middle1.Props,
            middle1.Contents.Cast<WLine>()
                   .Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.BeforeTheV)));
        var newMiddle3 = new WCell(middle3.Row, middle3.Props,
            middle3.Contents.Cast<WLine>()
                   .Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.AfterTheV)));
        enrichedRows =
        [

            new(rows[0].Table, rows[0].TablePropertyExceptions, rows[0].Properties,
            [
                    rows[0].TypedCells[0],
                    newMiddle1,
                    rows[0].TypedCells[^1]
            ]),

            rows[1],
            new(rows[2].Table, rows[2].TablePropertyExceptions, rows[2].Properties,
            [
                    rows[2].TypedCells[0],
                    newMiddle3,
                    rows[2].TypedCells[^1]
            ])
        ];
        return true;
    }

    private WRow EnrichRowWithPartyRoleFromNextRow(WRow row, WRow next)
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
                               .Where(LineHasContent)
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
        var linesWithContent = cell.Contents.OfType<WLine>().Where(LineHasContent).ToArray();
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
    private static bool IsV(string s)
    {
        return s.Trim(' ', '-', '–').Equals("v", StringComparison.InvariantCultureIgnoreCase);
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
        var contents = new List<IBlock>();
        var firstPartyFound = false;
        var andFound = false;
        var secondPartyFound = false;
        foreach (var block in cell.Contents)
        {
            if (block is not WLine line)
            {
                return cell;
            }

            if (IsEmptyLine(line))
            {
                contents.Add(block);
                continue;
            }

            var lineContents = line.Contents.ToArray();
            if (lineContents.Length == 1)
            {
                var first = lineContents[0];
                if (first is not WText wText)
                {
                    return cell;
                }

                if (IsBlank(wText))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsInBrackets(wText.Text))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsAnd(wText.Text))
                {
                    andFound = true;
                    contents.Add(block);
                    continue;
                }

                if (firstPartyFound && andFound)
                {
                    secondPartyFound = true;
                    var party = new WParty(wText) { Role = roles.second };
                    var newLine = WLine.Make(line, [party]);
                    contents.Add(newLine);
                }
                else
                {
                    firstPartyFound = true;
                    var party = new WParty(wText) { Role = roles.first };
                    var newLine = WLine.Make(line, [party]);
                    contents.Add(newLine);
                }
            }
            else if (lineContents.Length == 2)
            {
                // EWHC/Admin/2016/176

                var first = lineContents[0];
                var second = lineContents[1];
                if (first is not WText wText1)
                {
                    contents.Add(block);
                    continue;
                }

                if (second is not WText wText2)
                {
                    contents.Add(block);
                    continue;
                }

                if (IsNotBlank(wText1))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsBlank(wText2))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsInBrackets(wText2.Text))
                {
                    contents.Add(block);
                    continue;
                }

                if (IsAnd(wText2.Text))
                {
                    andFound = true;
                    contents.Add(block);
                    continue;
                }

                if (andFound)
                {
                    secondPartyFound = true;
                    var party = new WParty(wText2) { Role = roles.second };
                    var newLine = WLine.Make(line, [first, party]);
                    contents.Add(newLine);
                }
                else
                {
                    firstPartyFound = true;
                    var party = new WParty(wText2) { Role = roles.first };
                    var newLine = WLine.Make(line, [first, party]);
                    contents.Add(newLine);
                }
            }
            else
            {
                contents.Add(block);
            }
        }

        if (!firstPartyFound)
        {
            return cell;
        }

        if (!andFound)
        {
            return cell;
        }

        if (!secondPartyFound)
        {
            return cell;
        }

        return new WCell(cell.Row, cell.Props, contents);
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
            if (IsEmptyLine(line))
            {
                if (firstPartyFound)
                {
                    emptyAfterFirstFound = true;
                }

                contents.Add(line);
                continue;
            }

            if (emptyAfterFirstFound)
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

    private static bool IsInTheMatterOfSomething(WCell cell)
    {
        var cellContents = cell.Contents.ToArray();
        return cellContents is [WLine line] && IsInTheMatterOfSomething(line);
    }

    private WCell EnrichInTheMatterOfSomething(WCell cell)
    {
        var line = MakeDocTitle((WLine)cell.Contents.First());
        return new WCell(cell.Row, cell.Props, [line]);
    }

    private WLine EnrichLineWithDocTitle(WLine line)
    {
        return line.Contents.ToArray() switch
        {
            [WText wText] when wText.Text.StartsWith("IN THE MATTER OF ", StringComparison.InvariantCultureIgnoreCase)
                => WLine.Make(line, [new WDocTitle(wText)]),
            _ => line
        };
    }
}
