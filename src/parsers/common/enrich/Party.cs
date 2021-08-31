
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

// there are "third" paries in EWCA/Civ/2015/631

class PartyEnricher : Enricher {

    delegate IFormattedText Wrapper(string text, RunProperties props);

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        IBlock[] before = blocks.ToArray();
        List<IBlock> after = new List<IBlock>(before.Length);
        int i = 0;
        while (i < before.Length) {
            if (IsInTheMatterOf4(before, i)) {
                    List<IBlock> enriched4 = EnrichInTheMatterOf4(before, i);
                    after.AddRange(enriched4);
                    i += 4;
                    continue;
            }
            if (IsFiveLinePartyBlock(before, i)) {
                    List<IBlock> enriched5 = EnrichFiveLinePartyBlock(before, i);
                    after.AddRange(enriched5);
                    i += 5;
                    continue;
            }
            if (IsSixLinePartyBlock(before, i)) {
                    List<IBlock> enriched6 = EnrichSixLinePartyBlock(before, i);
                    after.AddRange(enriched6);
                    i += 6;
                    continue;
            }
            if (IsSevenLinePartyBlock(before, i)) {
                    List<IBlock> enriched7 = EnrichSevenLinePartyBlock(before, i);
                    after.AddRange(enriched7);
                    i += 7;
                    continue;
            }
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
            if (IsTwelveLinePartyBlock(before, i)) {
                    List<IBlock> enriched10 = EnrichTwelveLinePartyBlock(before, i);
                    after.AddRange(enriched10);
                    i += 12;
                    continue;
            }
            IBlock block = before[i];
            var enriched = EnrichBlock(block);
            after.Add(enriched);
            i += 1;
        }
        return after;
    }

    private static bool IsInTheMatterOf4(IBlock[] before, int i) {  // EWHC/QB/2017/2921
        if (i > before.Length - 4)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsInTheMatterOf1(line2);
        bool ok3 = IsInTheMatterOf2(line3);
        bool ok4 = IsAfterPartyMarker(line4);
        return
            IsBeforePartyMarker(line1) &&
            IsInTheMatterOf1(line2) &&
            IsInTheMatterOf2(line3) &&
            IsAfterPartyMarker(line4);

    }

    private static List<IBlock> EnrichInTheMatterOf4(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        List<IBlock> after = new List<IBlock>(4);
        after.Add(line1);
        WLine docTitle1 = MakeDocTitle(line2);
        WLine docTitle2 = MakeDocTitle(line3);
        after.Add(docTitle1);
        after.Add(docTitle2);
        after.Add(line4);
        return after;
    }

    private static bool IsFiveLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 5)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsBetweenPartyMarker(line3) &&
            IsPartyName(line4) &&
            IsAfterPartyMarker(line5);
    }
    private static List<IBlock> EnrichFiveLinePartyBlock(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        List<IBlock> after = new List<IBlock>(5);
        after.Add(line1);
        WLine party1 = MakeParty(line2, null);
        after.Add(party1);
        after.Add(line3);
        WLine party2 = MakeParty(line4, null);
        after.Add(party2);
        after.Add(line5);
        return after;
    }

    private static bool IsSixLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 6)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        return
            IsBeforePartyMarker(line1) &&
            IsBeforePartyMarker2(line2) &&
            IsPartyNameAndRole(line3) &&
            IsBetweenPartyMarker2(line4) &&
            IsPartyNameAndRole(line5) &&
            IsAfterPartyMarker(line6);
    }
    private static List<IBlock> EnrichSixLinePartyBlock(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        List<IBlock> after = new List<IBlock>(5);
        after.Add(line1);
        after.Add(line2);
        WLine party1 = MakePartyAndRole(line3);
        after.Add(party1);
        after.Add(line4);
        WLine party2 = MakePartyAndRole(line5);
        after.Add(party2);
        after.Add(line6);
        return after;
    }

    private static bool IsSevenLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 7)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsPartyName(line2);
        bool ok3 = IsFirstPartyTpye(line3);
        bool ok4 = IsBetweenPartyMarker(line4);
        bool ok5 = IsPartyName(line5);
        bool ok6 = IsSecondPartyType(line6);
        bool ok7 = IsAfterPartyMarker(line7);
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsFirstPartyTpye(line3) &&
            IsBetweenPartyMarker(line4) &&
            IsPartyName(line5) &&
            IsSecondPartyType(line6) &&
            IsAfterPartyMarker(line7);
    }
    private static List<IBlock> EnrichSevenLinePartyBlock(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        List<IBlock> after = new List<IBlock>(8);
        after.Add(line1);
        PartyRole role1 = GetFirstPartyRole(line3);
        WLine party1 = MakeParty(line2, role1);
        after.Add(party1);
        after.Add(line3);
        after.Add(line4);
        PartyRole role2 = GetSecondPartyRole(line6);
        WLine party2 = MakeParty(line5, role2);
        after.Add(party2);
        after.Add(line7);
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
        // bool ok1 = IsBeforePartyMarker(line1);
        // bool ok2 = IsBeforePartyMarker2(line2);
        // bool ok3 = IsPartyName(line3);
        // bool ok4 = IsFirstPartyTpye(line4);
        // bool ok5 = IsBetweenPartyMarker(line5);
        // bool ok6 = IsPartyName(line6);
        // bool ok7 = IsPartyName(line7);
        // bool ok8 = IsPartyName(line8);
        // bool ok9 = IsSecondPartyType(line9);
        // bool ok10 = IsAfterPartyMarker(line10);
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

    /* twelve */
    private static bool IsTwelveLinePartyBlock(IBlock[] before, int i) {    // EWCA/Civ/2005/450
        if (i > before.Length - 12)
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
        IBlock line11 = before[i+10];
        IBlock line12 = before[i+11];
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsPartyName(line2);
        bool ok3 = IsFirstPartyTpye(line3);
        bool ok4 = IsBetweenPartyMarker(line4);
        bool ok5 = IsPartyName(line5);
        bool ok6 = IsPartyName(line6);
        bool ok7 = IsPartyName(line7);
        bool ok8 = IsPartyName(line8);
        bool ok9 = IsPartyName(line9);
        bool ok10 = IsPartyName(line10);
        bool ok11 = IsSecondPartyType(line11);
        bool ok12 = IsAfterPartyMarker(line12);
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsFirstPartyTpye(line3) &&
            IsBetweenPartyMarker(line4) &&
            IsPartyName(line5) &&
            IsPartyName(line6) &&
            IsPartyName(line7) &&
            IsPartyName(line8) &&
            IsPartyName(line9) &&
            IsPartyName(line10) &&
            IsSecondPartyType(line11) &&
            IsAfterPartyMarker(line12);
    }
    private static List<IBlock> EnrichTwelveLinePartyBlock(IBlock[] before, int i) {
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
        IBlock line11 = before[i+10];
        IBlock line12 = before[i+11];
        List<IBlock> after = new List<IBlock>(8);
        after.Add(line1);
        PartyRole role1 = GetFirstPartyRole(line3);
        WLine party1 = MakeParty(line2, role1);
        after.Add(party1);
        after.Add(line4);
        PartyRole role2 = GetSecondPartyRole(line11);
        WLine party2 = MakeParty(line5, role2);
        WLine party3 = MakeParty(line6, role2);
        WLine party4 = MakeParty(line7, role2);
        WLine party5 = MakeParty(line8, role2);
        WLine party6 = MakeParty(line9, role2);
        WLine party7 = MakeParty(line10, role2);
        after.Add(party2);
        after.Add(party3);
        after.Add(party4);
        after.Add(party5);
        after.Add(party6);
        after.Add(party7);
        after.Add(line9);
        after.Add(line10);
        return after;
    }

    /* */

    private static bool IsBeforePartyMarker(IBlock block) {
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        if (Regex.IsMatch(normalized, @"^-( -)+$"))
            return true;
        if (Regex.IsMatch(normalized, @"^-+$"))
            return true;
        if (Regex.IsMatch(normalized, @"^_+$"))
            return true;
        return false;
    }
    private static bool IsBeforePartyMarker2(IBlock block) {
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        if (normalized == "Between:")
            return true;
        if (normalized == "B E T W E E N:")
            return true;
        return false;
    }

    private static bool IsInTheMatterOfSomething(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not WText wText)
            return false;
        return Regex.IsMatch(wText.Text, @"^IN THE MATTER OF [A-Z]", RegexOptions.IgnoreCase);
    }
    private static bool IsInTheMatterOf1(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not WText wText)
            return false;
        return wText.Text == "IN THE MATTER OF";
    }
    private static bool IsInTheMatterOf2(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not WText wText)
            return false;
        return true;
    }
    private static WLine MakeDocTitle(IBlock block) {
        WLine line = (WLine) block;
        WText wText = (WText) line.Contents.First();
        WDocTitle docTitle = new WDocTitle(wText);
        List<IInline> contents = new List<IInline>(1) { docTitle };
        return new WLine(line, contents);
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
        if (line.Contents.Count() == 3) {   // EWHC/Admin/2012/3928, EWHC/Admin/2007/552
            IInline second = line.Contents.Skip(1).First();
            IInline third = line.Contents.Skip(2).First();
            if (second is not WText wText2)
                return false;
            if (third is not WText wText3)
                return false;
            if (!string.IsNullOrWhiteSpace(wText2.Text))
                return false;
            return IFormattedText.HaveSameFormatting(wText, wText3);    // not really sure why this should matter
        }
        return false;
    }
    private static WLine MakeParty(IBlock name, PartyRole? role) {
        WLine line = (WLine) name;
        if (line.Contents.Count() == 1) {
            WText text = (WText) line.Contents.First();
            WParty party = new WParty(text) { Role = role };
            IEnumerable<IInline> contents = new List<IInline>(1) { party };
            return new WLine(line, contents);
        } else {
            WParty2 party2 = new WParty2(line.Contents.Cast<IFormattedText>()) { Role = role };
            IEnumerable<IInline> contents = new List<IInline>(1) { party2 };
            return new WLine(line, contents);
        }
    }

    private static bool IsAnyPartyType(string s) {
        // s = Regex.Replace(s, @"\s+", " ").Trim();
        if (IsFirstPartyTpye(s))
            return true;
        if (IsSecondPartyType(s))
            return true;
        return false;
    }
    private static bool IsFirstPartyTpye(string s) {
        ISet<string> firstPartyTypes = new HashSet<string>() { "Claimant", "Claimant/Respondent", "(CLAIMANT)", "Applicant" };
        return firstPartyTypes.Contains(s);
    }
    private static bool IsFirstPartyTpye(IBlock block) {
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return IsFirstPartyTpye(normalized);
    }
    private static PartyRole GetAnyPartyRole(string s) {
        if (IsFirstPartyTpye(s))
            return GetFirstPartyRole(s);
        if (IsSecondPartyType(s))
            return GetSecondPartyRole(s);
        throw new System.Exception();
    }
    private static PartyRole GetFirstPartyRole(string s) {
        switch (s) {
            case "Claimant":
            case "(CLAIMANT)":
                return PartyRole.Claimant;
            case "Claimant/Respondent":
                return PartyRole.Respondent;
            case "Applicant":
                return PartyRole.Applicant;
            default:
                throw new System.Exception();
        }
    }
    private static PartyRole GetFirstPartyRole(IBlock block) {
        if (block is not ILine line)
            throw new System.Exception();
        string normalized = line.NormalizedContent();
        return GetFirstPartyRole(normalized);
    }

    private static bool IsPartyNameAndRole(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line.Contents.Count() != 4)
            return false;
        IInline first = line.Contents.First();
        IInline second = line.Contents.Skip(1).First();
        IInline third = line.Contents.Skip(2).First();
        IInline fourth = line.Contents.Skip(3).First();
        if (first is not WTab)
            return false;
        if (second is not WText wText1)
            return false;
        if (third is not WTab)
            return false;
        if (fourth is not WText wText2)
            return false;
        string s = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
        if (!IsAnyPartyType(s))
            return false;
        return true;
    }
    private static WLine MakePartyAndRole(IBlock block) {
        WLine line = (WLine) block;
        WTab first = (WTab) line.Contents.First();
        WText second = (WText) line.Contents.Skip(1).First();
        WTab third = (WTab) line.Contents.Skip(2).First();
        WText fourth = (WText) line.Contents.Skip(3).First();
        string s = Regex.Replace(fourth.Text, @"\s+", " ").Trim();
        PartyRole role = GetAnyPartyRole(s);
        List<IInline> contents = new List<IInline>(4) {
            first,
            new WParty(second.Text, second.properties) { Role = role },
            third,
            fourth
        };
        return new WLine(line, contents);
    }

    private static bool IsBetweenPartyMarker(IBlock block) {
        ISet<string> betweenPartyMarkers = new HashSet<string>() { "v", "-v-", "- v -" };
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return betweenPartyMarkers.Contains(normalized);
    }
    private static bool IsBetweenPartyMarker2(IBlock block) {
        ISet<string> betweenPartyMarkers = new HashSet<string>() { "- and -" }; // EWHC/Fam/2013/3493
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();   // doesn't normalize internal spaces
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return betweenPartyMarkers.Contains(normalized);
    }

    private static bool IsSecondPartyType(string s) {
        ISet<string> secondPartyTypes = new HashSet<string>() { "Defendant", "Defendants", "Defendant/Appellant", "(DEFENDANT)", "Defendants/Appellants", "Respondent" };
        return secondPartyTypes.Contains(s);
    }
    private static bool IsSecondPartyType(IBlock block) {
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return IsSecondPartyType(normalized);
    }
    private static PartyRole GetSecondPartyRole(string s) {
        switch (s) {
            case "Defendant":
            case "Defendants":
            case "(DEFENDANT)":
                return PartyRole.Defendant;
            case "Defendant/Appellant":
            case "Defendants/Appellants":
                return PartyRole.Appellant;
            case "Respondent":
                return PartyRole.Respondent;
            default:
                throw new System.Exception();
        }
    }
    private static PartyRole GetSecondPartyRole(IBlock block) {
        if (block is not ILine line)
            throw new System.Exception();
        string normalized = line.NormalizedContent();
        return GetSecondPartyRole(normalized);
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

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        return line;
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
        if (role is not null) {
            second = EnrichCell(second, role.Value);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        if (!third.Contents.All(block => block is ILine line && string.IsNullOrWhiteSpace(line.NormalizedContent())))
            return row;
        if (IsInTheMatterOfSomething(second)) {
            second = EnrichInTheMatterOfSomething(second);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        return row;
    }

    private static PartyRole? GetPartyRole(WCell cell) {
        PartyRole? role = GetOneLinePartyRole(cell);
        if (role is not null)
            return role;
        role = GetTwoLinePartyRole(cell);
        if (role is not null)
            return role;
        role = GetNLinePartyRole(cell);
        if (role is not null)
            return role;
        return null;
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
        types = new HashSet<string>() { "Applicant", "Respondent/Applicant" };
        if (types.Contains(normalized))
            return PartyRole.Applicant;
        types = new HashSet<string>() { "Defendant", "Defendants", "First Defendant", "Second Defendant", "Third Defendant" };
        if (types.Contains(normalized))
            return PartyRole.Defendant;
        types = new HashSet<string>() { "Respondent", "Respondents", "Claimant/Respondent", "Claimant/ Respondent", "Defendant/Respondent", "Defendant/ Respondent", "Petitioner/Respondent",
            "First Respondent", "Second Respondent", "Third Respondent", "Fourth Respondent"
        };
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
        if (one == "Defendant/" && two == "Appellant")    // EWCA/Civ/2011/1383
            return PartyRole.Appellant;
        if (one == "Claimant/" && two == "Respondent")    // EWCA/Civ/2008/183
            return PartyRole.Respondent;
        if (one == "Claimants/" && two == "Respondents")    // EWHC/Ch/2017/916
            return PartyRole.Respondent;
        if (one == "Claimant/" && two.EndsWith("Defendant"))    // EWHC/Ch/2008/2079
            return PartyRole.Claimant;
        if (one == "Claimant/" && two == "Appellant")    // EWCA/Civ/2011/1277
            return PartyRole.Appellant;
        if (one == "Respondent/" && two == "Claimant")    // EWCA/Civ/2017/97
            return PartyRole.Respondent;
        if (one == "Appellants/" && two == "Defendants & Counterclaimants")    // EWCA/Civ/2017/97
            return PartyRole.Appellant;
        if (one == "Respondent" && two == "Intervener")    // EWCA/Civ/2016/176
            return PartyRole.Respondent;
        // if (one == "" && two == "")    // 
        //     return PartyRole.;
        return null;
    }

    private static PartyRole? GetNLinePartyRole(WCell cell) {
        var blocks = cell.Contents.Where(block => !IsEmptyLine(block));
        if (blocks.Count() < 3)
            return null;
        if (!blocks.All(block => block is WLine))
            return null;
        if (blocks.Count() == 3) {
            string one = ((ILine) blocks.First()).NormalizedContent();
            string two = ((ILine) blocks.Skip(1).First()).NormalizedContent();
            string three = ((ILine) blocks.Skip(2).First()).NormalizedContent();
            if (one == "Defendants" && two == "Part 20 Claimant/" && three == "Appellant")
                return PartyRole.Appellant;
        }
        Func<ILine, bool> defendant = (line) => {
            string normalized = line.NormalizedContent();
            if (Regex.IsMatch(normalized, @"^\d(st|nd|rd|th)? Defendant$"))
                return true;
            return false;
        };
        if (blocks.Cast<ILine>().All(defendant))
            return PartyRole.Defendant;
        Func<ILine, bool> appellant = (line) => {
            string normalized = line.NormalizedContent();
            return Regex.IsMatch(normalized, @"^\d(st|nd|rd|th)? Appellant$", RegexOptions.IgnoreCase);
        };
        if (blocks.Cast<ILine>().All(appellant))
            return PartyRole.Appellant;
        Func<ILine, bool> respondent = (line) => {
            string normalized = line.NormalizedContent();
            return Regex.IsMatch(normalized, @"^\d(st|nd|rd|th)? Respondent$", RegexOptions.IgnoreCase);
        };
        if (blocks.Cast<ILine>().All(respondent))
            return PartyRole.Respondent;
        Func<ILine, bool> respondent2 = (line) => {
            string normalized = line.NormalizedContent();
            return Regex.IsMatch(normalized, @"^(First|Second|Third|Fourth) Respondent$", RegexOptions.IgnoreCase);
        };
        if (blocks.Cast<ILine>().All(respondent2))
            return PartyRole.Respondent;
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
                if (string.IsNullOrWhiteSpace(wText.Text))
                    return line;
                if (wText.Text.StartsWith('(') && wText.Text.EndsWith(')'))
                    return line;
                if (wText.Text == "- and –")    // EWHC/Fam/2008/1561
                    return line;
                WParty party = new WParty(wText) { Role = role };
                return new WLine(line, new List<IInline>(1) { party });
            }
        );
        return new WCell(cell.Main, contents);
    }

    private static bool IsInTheMatterOfSomething(WCell cell) {
        if (cell.Contents.Count() != 1)
            return false;
        return IsInTheMatterOfSomething(cell.Contents.First());
    }
    private WCell EnrichInTheMatterOfSomething(WCell cell) {
        WLine line = MakeDocTitle(cell.Contents.First());
        return new WCell(cell.Main, new List<IBlock>(1) { line });
    }

    private ILine EnrichLine(ILine line) {
        if (line.Contents.Count() != 1)
            return line;
        IInline first = line.Contents.First();
        if (first is not WText text)
            return line;
        if (text.Text.StartsWith("IN THE MATTER OF ", StringComparison.InvariantCultureIgnoreCase)) {
            WDocTitle docTitle = new WDocTitle(text);
            List<IInline> contents = new List<IInline>(1) { docTitle };
            return new WLine((WLine) line, contents);
        }
        return line;
    }

}

}
