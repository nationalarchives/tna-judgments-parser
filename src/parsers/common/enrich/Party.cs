
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class PartyEnricher : Enricher {

    delegate IFormattedText Wrapper(string text, RunProperties props);

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        IBlock[] before = blocks.ToArray();
        List<IBlock> after = new List<IBlock>(before.Length);
        int i = 0;
        while (i < before.Length) {
            IBlock block = before[i];
            if (IsEightLinePartyBlock(before, i)) {
                    List<IBlock> enriched8 = EnrichEightLinePartyBlock(before, i);
                    after.AddRange(enriched8);
                    i += 8;
                    continue;
            }
            if (IsEightLinePartyBlock2(before, i)) {
                    List<IBlock> enriched8 = EnrichEightLinePartyBlock2(before, i);
                    after.AddRange(enriched8);
                    i += 8;
                    continue;
            }
            if (IsTenLinePartyBlock(before, i)) {
                    List<IBlock> enriched10 = EnrichTenLinePartyBlock(before, i);
                    after.AddRange(enriched10);
                    i += 10;
                    continue;
            }
            if (block is ILine line1 && before.Length - i > 7) {
                IBlock line2 = before[i+1];
                IBlock line3 = before[i+2];
                IBlock line4 = before[i+3];
                IBlock line5 = before[i+4];
                IBlock line6 = before[i+5];
                IBlock line7 = before[i+6];
                if (IsBeforePartyMarker(line1)) {
                    if (IsPartyName(line2)) {
                        if (IsFirstPartyTpye(line3)) {
                            if (IsBetweenPartyMarker(line4)) {
                                if (IsPartyName(line5)) {
                                    if (IsSecondPartyType(line6)) {
                                        if (IsAfterPartyMarker(line7)) {
                    after.Add(line1);
                    PartyRole role1 = GetFirstPartyRole(line3);
                    WLine party1 = MakeParty(line2, role1);
                    after.Add(party1);
                    after.Add(line3);
                    after.Add(line4);
                    PartyRole role2 = GetSecondPartyRole(line6);
                    WLine party2 = MakeParty(line5, role2);
                    after.Add(party2);
                    after.Add(line6);
                    after.Add(line7);
                    i += 7;
                    continue;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (IsBeforePartyMarker(line1)) {
                    if (IsPartyName(line2)) {
                        if (IsBetweenPartyMarker(line3)) {
                            if (IsPartyName(line4)) {
                                if (IsAfterPartyMarker(line5)) {
                    after.Add(line1);
                    WLine party1 = MakeParty(line2, null);
                    after.Add(party1);
                    after.Add(line3);
                    WLine party2 = MakeParty(line4, null);
                    after.Add(party2);
                    after.Add(line5);
                    i += 5;
                    continue;
                                }
                            }
                        }
                    }
                }
            }
            var enriched = EnrichBlock(block);
            after.Add(enriched);
            i += 1;
        }
        return after;
    }

    private static bool IsEightLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 8)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        IBlock line8 = before[i+7];
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsFirstPartyTpye(line3) &&
            IsBetweenPartyMarker(line4) &&
            IsPartyName(line5) &&
            IsPartyName(line6) &&
            IsSecondPartyType(line7) &&
            IsAfterPartyMarker(line8);
    }
    private static List<IBlock> EnrichEightLinePartyBlock(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        IBlock line8 = before[i+7];
        List<IBlock> after = new List<IBlock>(8);
        after.Add(line1);
        PartyRole role1 = GetFirstPartyRole(line3);
        WLine party1 = MakeParty(line2, role1);
        after.Add(party1);
        after.Add(line3);
        after.Add(line4);
        PartyRole role2 = GetSecondPartyRole(line7);
        WLine party2 = MakeParty(line5, role2);
        after.Add(party2);
        WLine party3 = MakeParty(line6, role2);
        after.Add(party3);
        after.Add(line7);
        after.Add(line8);
        return after;
    }

    private static bool IsEightLinePartyBlock2(IBlock[] before, int i) {    // EWHC/Admin/2009/1638
        if (i > before.Length - 8)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        IBlock line8 = before[i+7];
        return
            IsBeforePartyMarker(line1) &&
            IsBeforePartyMarker2(line2) &&
            IsPartyName(line3) &&
            IsFirstPartyTpye(line4) &&
            IsBetweenPartyMarker(line5) &&
            IsPartyName(line6) &&
            IsSecondPartyType(line7) &&
            IsAfterPartyMarker(line8);
    }
    private static List<IBlock> EnrichEightLinePartyBlock2(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        IBlock line8 = before[i+7];
        List<IBlock> after = new List<IBlock>(8);
        after.Add(line1);
        after.Add(line2);
        PartyRole role1 = GetFirstPartyRole(line4);
        WLine party1 = MakeParty(line3, role1);
        after.Add(party1);
        after.Add(line4);
        after.Add(line5);
        PartyRole role2 = GetSecondPartyRole(line7);
        WLine party2 = MakeParty(line6, role2);
        after.Add(party2);
        after.Add(line7);
        after.Add(line8);
        return after;
    }

    /* ten */
    private static bool IsTenLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 10)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        IBlock line8 = before[i+7];
        IBlock line9 = before[i+8];
        IBlock line10 = before[i+9];
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsBeforePartyMarker2(line2);
        bool ok3 = IsPartyName(line3);
        bool ok4 = IsFirstPartyTpye(line4);
        bool ok5 = IsBetweenPartyMarker(line5);
        bool ok6 = IsPartyName(line6);
        bool ok7 = IsPartyName(line7);
        bool ok8 = IsPartyName(line8);
        bool ok9 = IsSecondPartyType(line9);
        bool ok10 = IsAfterPartyMarker(line10);
        return
            IsBeforePartyMarker(line1) &&
            IsBeforePartyMarker2(line2) &&
            IsPartyName(line3) &&
            IsFirstPartyTpye(line4) &&
            IsBetweenPartyMarker(line5) &&
            IsPartyName(line6) &&
            IsPartyName(line7) &&
            IsPartyName(line8) &&
            IsSecondPartyType(line9) &&
            IsAfterPartyMarker(line10);
    }
    private static List<IBlock> EnrichTenLinePartyBlock(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        IBlock line8 = before[i+7];
        IBlock line9 = before[i+8];
        IBlock line10 = before[i+9];
        List<IBlock> after = new List<IBlock>(8);
        after.Add(line1);
        after.Add(line2);
        PartyRole role1 = GetFirstPartyRole(line4);
        WLine party1 = MakeParty(line3, role1);
        after.Add(party1);
        after.Add(line4);
        after.Add(line5);
        PartyRole role2 = GetSecondPartyRole(line9);
        WLine party2 = MakeParty(line6, role2);
        WLine party3 = MakeParty(line7, role2);
        WLine party4 = MakeParty(line8, role2);
        after.Add(party2);
        after.Add(party3);
        after.Add(party4);
        after.Add(line9);
        after.Add(line10);
        return after;
    }


    private static bool IsBeforePartyMarker(IBlock block) {
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return Regex.IsMatch(normalized, @"^-( -)+$");
    }
    private static bool IsBeforePartyMarker2(IBlock block) {
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return normalized == "Between:";
    }
    private static bool IsPartyName(IBlock block) {
        if (block is not ILine line)
            return false;
        if (line.Contents.Count() == 0)
            return false;
        IInline first = line.Contents.First();
        if (first is not WText wText)
            return false;
        if (line.Contents.Count() == 1)
            return true;
        if (line.Contents.Count() == 3) {   // EWHC/Admin/2012/3928
            IInline second = line.Contents.Skip(1).First();
            IInline third = line.Contents.Skip(2).First();
            if (second is not WText wText2)
                return false;
            if (third is not WText wText3)
                return false;
            if (!string.IsNullOrWhiteSpace(wText2.Text))
                return false;
            return Regex.IsMatch(wText3.Text, @"^\(\d\)$");
        }
        return false;
    }
    private static WLine MakeParty(IBlock name, PartyRole? role) {
        WLine line = (WLine) name;
        WText text = (WText) line.Contents.First();
        WParty party = new WParty(text) { Role = role };
        IEnumerable<IInline> contents = line.Contents.Skip(1).Prepend(party);
        return new WLine(line, contents);
    }
    private static bool IsFirstPartyTpye(IBlock block) {
        ISet<string> firstPartyTypes = new HashSet<string>() { "Claimant", "Claimant/Respondent" };
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return firstPartyTypes.Contains(normalized);
    }
    private static PartyRole GetFirstPartyRole(IBlock block) {
        if (block is not ILine line)
            throw new System.Exception();
        string normalized = line.NormalizedContent();
        switch (normalized) {
            case "Claimant":
                return PartyRole.Claimant;
            case "Claimant/Respondent":
                return PartyRole.Respondent;
            default:
                throw new System.Exception();
        }
    }
    private static bool IsBetweenPartyMarker(IBlock block) {
        ISet<string> betweenPartyMarkers = new HashSet<string>() { "-v-", "v" };
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return betweenPartyMarkers.Contains(normalized);
    }
    private static bool IsSecondPartyType(IBlock block) {
        ISet<string> secondPartyTypes = new HashSet<string>() { "Defendant", "Defendants", "Defendant/Appellant" };
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return secondPartyTypes.Contains(normalized);
    }
    private static PartyRole GetSecondPartyRole(IBlock block) {
        if (block is not ILine line)
            throw new System.Exception();
        string normalized = line.NormalizedContent();
        switch (normalized) {
            case "Defendant":
            case "Defendants":
                return PartyRole.Defendant;
            case "Defendant/Appellant":
                return PartyRole.Appellant;
            default:
                throw new System.Exception();
        }
    }
    private static bool IsAfterPartyMarker(IBlock block) {
        return IsBeforePartyMarker(block);
    }


    private IBlock EnrichBlock(IBlock block) {
        if (block is WTable table)
            return EnrichTable(table);
        if (block is ILine line)
            return EnrichLine(line);
        return block;
    }

    // static IInline[] Split(WText text, Group group, Wrapper wrapper) {
    //     string before = text.Text.Substring(0, group.Index);
    //     string during = group.Value;
    //     string after = text.Text.Substring(group.Index + group.Length);
    //     IInline[] replacement = {
    //         new WText(before, text.properties),
    //         wrapper(during, text.properties)
    //     };
    //     if (!string.IsNullOrEmpty(after))
    //         replacement[2] = new WText(after, text.properties);
    //     return replacement;
    // }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        return line;
        // while (line.Count() > 0 && line.Last() is WTab)
        //     line = line.SkipLast(1);
        // if (line.Count() < 3)
        //     return line;
        // IInline first = line.First();
        // if (first is not WText text1)
        //     return line;
        // if (!Regex.IsMatch(text1.Text, @"^\s*(Claimant|Respondent)\:?\s*$"))
        //     return line;
        // IEnumerable<IInline> middle = line.Skip(1).Take(line.Count() - 2);
        // if (!middle.All(i => i is WTab))
        //     return line;
        // IInline last = line.Last();
        // if (last is not WText text3)
        //     return line;
        // return middle.Prepend(first).Append(new WParty(text3));
    }


    /* tables */


    private WTable EnrichTable(WTable table) {
        IEnumerable<WRow> rows = EnrichRows(table.TypedRows);
        return new WTable(table.Main, rows);
    }

    private IEnumerable<WRow> EnrichRows(IEnumerable<WRow> rows) {
        return rows.Select(row => EnrichRow(row));
    }

    private static bool IsEmptyLine(IBlock block) {
        if (block is not ILine line)
            return false;
        return IsEmptyLine(line);
    }
    private static bool IsEmptyLine(ILine line) {
        return string.IsNullOrWhiteSpace(line.NormalizedContent());
    }

    private WRow EnrichRow(WRow row) {
        if (row.Cells.Count() != 3)
            return row;
        WCell first = (WCell) row.Cells.ElementAt(0);
        WCell second = (WCell) row.Cells.ElementAt(1);
        WCell third = (WCell) row.Cells.ElementAt(2);
        if (!first.Contents.All(block => block is ILine line && string.IsNullOrWhiteSpace(line.NormalizedContent())))
            return row;
        PartyRole? role = GetPartyRole(third);
        if (role is null)
            return row;
        second = EnrichCell(second, role.Value);
        return new WRow(row.Main, new List<WCell>(3){ first, second, third });
    }

    private static PartyRole? GetPartyRole(WCell cell) {
        PartyRole? role = GetOneLinePartyRole(cell);
        if (role is not null)
            return role;
        return GetTwoLinePartyRole(cell);
    }

    private static PartyRole? GetOneLinePartyRole(WCell cell) {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block));
        if (lines.Count() != 1)
            return null;
        IBlock block = lines.First();
        if (block is not ILine line)
            return null;
        string normalized = line.NormalizedContent();
        ISet<string> types = new HashSet<string>() { "Appellant", "Appellants", "Defendant/Appellant", "Defendants/Appellants", "Appellants/ Claimants" };
        if (types.Contains(normalized))
            return PartyRole.Appellant;
        types = new HashSet<string>() { "Claimant", "Claimants" };
        if (types.Contains(normalized))
            return PartyRole.Claimant;
        types = new HashSet<string>() { "Applicant" };
        if (types.Contains(normalized))
            return PartyRole.Applicant;
        types = new HashSet<string>() { "Defendant", "Defendants", "First Defendant", "Second Defendant", "Third Defendant" };
        if (types.Contains(normalized))
            return PartyRole.Defendant;
        types = new HashSet<string>() { "Respondent", "Respondents", "Claimant/Respondent", "Claimant/ Respondent", "Defendant/ Respondent" };
        if (types.Contains(normalized))
            return PartyRole.Respondent;
        return null;
    }

    private static PartyRole? GetTwoLinePartyRole(WCell cell) {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block));
        if (lines.Count() != 2)
            return null;
        IBlock block1 = lines.First();
        if (block1 is not ILine line1)
            return null;
        IBlock block2 = lines.Last();
        if (block2 is not ILine line2)
            return null;
        string one = line1.NormalizedContent();
        string two = line2.NormalizedContent();
        if (one == "Defendant/" && two.EndsWith("Claimant"))    // EWHC/Ch/2008/2079
            return PartyRole.Defendant;
        if (one == "Defendant/" && two == "Applicant")    // EWHC/Ch/2017/916
            return PartyRole.Applicant;
        if (one == "Claimant/" && two == "Respondent")    // EWCA/Civ/2008/183
            return PartyRole.Respondent;
        if (one == "Claimants/" && two == "Respondents")    // EWHC/Ch/2017/916
            return PartyRole.Respondent;
        if (one == "Claimant/" && two.EndsWith("Defendant"))    // EWHC/Ch/2008/2079
            return PartyRole.Claimant;
        if (one == "Respondent/" && two.EndsWith("Claimant"))    // EWCA/Civ/2017/97
            return PartyRole.Respondent;
        if (one == "Appellants/" && two == "Defendants & Counterclaimants")    // EWCA/Civ/2017/97
            return PartyRole.Appellant;
        return null;
    }

    private WCell EnrichCell(WCell cell, PartyRole role) {
        IEnumerable<IBlock> contents = cell.Contents.Select(
            block => {
                if (block is WOldNumberedParagraph np) {    // EWCA/Civ/2015/455
                    if (np.Contents.Count() != 1)
                        return np;
                    IInline first2 = np.Contents.First();
                    if (first2 is not WText wText2)
                        return np;
                    WParty party2 = new WParty(wText2) { Role = role };
                    return new WOldNumberedParagraph(np, new List<IInline>(1) { party2 });
                }
                if (block is not WLine line)
                    return block;
                if (line.Contents.Count() != 1)
                    return line;
                IInline first = line.Contents.First();
                if (first is not WText wText)
                    return line;
                WParty party = new WParty(wText) { Role = role };
                return new WLine(line, new List<IInline>(1) { party });
            }
        );
        return new WCell(cell.Main, contents);
    }

    private ILine EnrichLine(ILine line) {
        if (line.Contents.Count() != 1)
            return line;
        IInline first = line.Contents.First();
        if (first is not WText text)
            return line;
        if (text.Text.StartsWith("IN THE MATTER OF ")) {
            WDocTitle docTitle = new WDocTitle(text);
            List<IInline> contents = new List<IInline>(1) { docTitle };
            return new WLine((WLine) line, contents);
        }
        return line;
    }

}

}
