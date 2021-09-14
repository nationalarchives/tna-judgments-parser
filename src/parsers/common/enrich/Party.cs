
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
                break;
            }
            if (IsFiveLinePartyBlock(before, i)) {
                List<IBlock> enriched5 = EnrichFiveLinePartyBlock(before, i);
                after.AddRange(enriched5);
                i += 5;
                break;
            }
            // if (IsSixLinePartyBlock(before, i)) {
            //     List<IBlock> enriched6 = EnrichSixLinePartyBlock(before, i);
            //     after.AddRange(enriched6);
            //     i += 6;
            //     break;
            // }
            // if (IsSixLinePartyBlock2(before, i)) {
            //     List<IBlock> enriched6 = EnrichSixLinePartyBlock2(before, i);
            //     after.AddRange(enriched6);
            //     i += 6;
            //     break;
            // }
            // if (IsSevenLinePartyBlock(before, i)) {
            //     List<IBlock> enriched7 = EnrichSevenLinePartyBlock(before, i);
            //     after.AddRange(enriched7);
            //     i += 7;
            //     break;
            // }
            // if (IsEightLinePartyBlock(before, i)) {
            //     List<IBlock> enriched8 = EnrichEightLinePartyBlock(before, i);
            //     after.AddRange(enriched8);
            //     i += 8;
            //     break;
            // }
            // if (IsEightLinePartyBlock2(before, i)) {
            //     List<IBlock> enriched8 = EnrichEightLinePartyBlock2(before, i);
            //     after.AddRange(enriched8);
            //     i += 8;
            //     break;
            // }
            IBlock[] rest = before[i..];
            List<IBlock> magic = EnrichMultiLinePartyBockOrNull(rest);
            if (magic is not null) {
                after.AddRange(magic);
                i += magic.Count;
                break;
            }
            // if (IsNineLinePartyBlock(before, i)) {
            //     List<IBlock> enriched9 = EnrichNineLinePartyBlock(before, i);
            //     after.AddRange(enriched9);
            //     i += 9;
            //     break;
            // }
            // if (IsTenLinePartyBlock(before, i)) {
            //     List<IBlock> enriched10 = EnrichTenLinePartyBlock(before, i);
            //     after.AddRange(enriched10);
            //     i += 10;
            //     break;
            // }
            // if (IsTenLinePartyBlock2(before, i)) {
            //     List<IBlock> enriched10 = EnrichTenLinePartyBlock2(before, i);
            //     after.AddRange(enriched10);
            //     i += 10;
            //     break;
            // }
            // if (IsTenLinePartyBlock3(before, i)) {
            //     List<IBlock> enriched10 = EnrichTenLinePartyBlock3(before, i);
            //     after.AddRange(enriched10);
            //     i += 10;
            //     break;
            // }
            // if (IsElevenLinePartyBlock(before, i)) {
            //     List<IBlock> enriched11 = EnrichElevenLinePartyBlock(before, i);
            //     after.AddRange(enriched11);
            //     i += 11;
            //     break;
            // }
            // if (IsTwelveLinePartyBlock(before, i)) {
            //     List<IBlock> enriched12 = EnrichTwelveLinePartyBlock(before, i);
            //     after.AddRange(enriched12);
            //     i += 12;
            //     break;
            // }
            IBlock block = before[i];
            var enriched1 = EnrichBlock(block);
            after.Add(enriched1);
            i += 1;
        }
        after.AddRange(before.Skip(i));
        return after;
    }

    private static bool IsInTheMatterOf4(IBlock[] before, int i) {  // EWHC/QB/2017/2921, EWHC/Ch/2006/3549
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

    /* five */

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

    /* six */

    private static bool IsSixLinePartyBlock(IBlock[] before, int i) {   // EWHC/Fam/2012/4047
        if (i > before.Length - 6)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsBeforePartyMarker2(line2);
        bool ok3 = IsPartyNameAndRole(line3);
        bool ok4 = IsBetweenPartyMarker2(line4);
        bool ok5 = IsPartyNameAndRole(line5);
        bool ok6 = IsAfterPartyMarker(line6);
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
        List<IBlock> after = new List<IBlock>(6);
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

    private static bool IsSixLinePartyBlock2(IBlock[] before, int i) {   // EWCA/Crim/2011/1136
        if (i > before.Length - 6)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsPartyName(line2);
        bool ok3 = IsBetweenPartyMarker(line3);
        bool ok4 = IsPartyName(line4);
        bool ok5 = IsPartyName(line5);
        bool ok6 = IsAfterPartyMarker(line6);
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsBetweenPartyMarker(line3) &&
            IsPartyName(line4) &&
            IsPartyName(line5) &&
            IsAfterPartyMarker(line6);
    }
    private static List<IBlock> EnrichSixLinePartyBlock2(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        List<IBlock> after = new List<IBlock>(6);
        after.Add(line1);
        WLine party1 = MakeParty(line2, PartyRole.BeforeTheV);
        after.Add(party1);
        after.Add(line3);
        WLine party2 = MakeParty(line4, PartyRole.AfterTheV);
        WLine party3 = MakeParty(line5, PartyRole.AfterTheV);
        after.Add(party2);
        after.Add(party3);
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
        bool ok3 = IsFirstPartyType(line3);
        bool ok4 = IsBetweenPartyMarker(line4);
        bool ok5 = IsPartyName(line5);
        bool ok6 = IsSecondPartyType(line6);
        bool ok7 = IsAfterPartyMarker(line7);
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsFirstPartyType(line3) &&
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
        List<IBlock> after = new List<IBlock>(7);
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
            IsFirstPartyType(line3) &&
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

    private static bool IsEightLinePartyBlock2(IBlock[] before, int i) {    // EWHC/Admin/2009/1638, EWHC/Admin/2008/2214?
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
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsBeforePartyMarker2(line2);
        bool ok3 = IsPartyName(line3);
        bool ok4 = IsFirstPartyType(line4);
        bool ok5 = IsBetweenPartyMarker(line5) || IsBetweenPartyMarker2(line5);
        bool ok6 = IsPartyName(line6);
        bool ok7 = IsSecondPartyType(line7);
        bool ok8 = IsAfterPartyMarker(line8);
        return
            IsBeforePartyMarker(line1) &&
            IsBeforePartyMarker2(line2) &&
            IsPartyName(line3) &&
            IsFirstPartyType(line4) &&
            (IsBetweenPartyMarker(line5) || IsBetweenPartyMarker2(line5)) &&
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

    /* nine */
    private static bool IsNineLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 9)
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
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsBeforePartyMarker2(line2);
        bool ok3 = IsPartyName(line3);
        bool ok4 = IsFirstPartyType(line4);
        bool ok5 = IsBetweenPartyMarker(line5) || IsBetweenPartyMarker2(line5);
        bool ok6 = IsPartyName(line6);
        bool ok7 = IsPartyName(line7);
        bool ok8 = IsSecondPartyType(line8);
        bool ok9 = IsAfterPartyMarker(line9);
        return
            IsBeforePartyMarker(line1) &&
            IsBeforePartyMarker2(line2) &&
            IsPartyName(line3) &&
            IsFirstPartyType(line4) &&
            IsBetweenPartyMarker(line5) || IsBetweenPartyMarker2(line5) &&
            IsPartyName(line6) &&
            IsPartyName(line7) &&
            IsSecondPartyType(line8) &&
            IsAfterPartyMarker(line9);
    }
    private static List<IBlock> EnrichNineLinePartyBlock(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        IBlock line5 = before[i+4];
        IBlock line6 = before[i+5];
        IBlock line7 = before[i+6];
        IBlock line8 = before[i+7];
        IBlock line9 = before[i+8];
        List<IBlock> after = new List<IBlock>(9);
        after.Add(line1);
        after.Add(line2);
        PartyRole role1 = GetFirstPartyRole(line4);
        WLine party1 = MakeParty(line3, role1);
        after.Add(party1);
        after.Add(line4);
        after.Add(line5);
        PartyRole role2 = GetSecondPartyRole(line8);
        WLine party2 = MakeParty(line6, role2);
        WLine party3 = MakeParty(line7, role2);
        after.Add(party2);
        after.Add(party3);
        after.Add(line8);
        after.Add(line9);
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
            IsFirstPartyType(line4) &&
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
        List<IBlock> after = new List<IBlock>(10);
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

    private static bool IsTenLinePartyBlock2(IBlock[] before, int i) {
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
        // bool ok2 = IsPartyName(line2);
        // bool ok3 = IsFirstPartyTpye(line3);
        // bool ok4 = IsBetweenPartyMarker(line4);
        // bool ok5 = IsPartyName(line5);
        // bool ok6 = IsSecondPartyType(line6);
        // bool ok7 = IsBetweenPartyMarker2(line7);
        // bool ok8 = IsPartyName(line8);
        // bool ok9 = IsSecondPartyType(line9);
        // bool ok10 = IsAfterPartyMarker(line10);
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsFirstPartyType(line3) &&
            IsBetweenPartyMarker(line4) &&
            IsPartyName(line5) &&
            IsSecondPartyType(line6) &&
            IsBetweenPartyMarker2(line7) &&
            IsPartyName(line8) &&
            IsSecondPartyType(line9) &&
            IsAfterPartyMarker(line10);
    }
    private static List<IBlock> EnrichTenLinePartyBlock2(IBlock[] before, int i) {
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
        List<IBlock> after = new List<IBlock>(10);
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
        PartyRole role3 = GetSecondPartyRole(line9);
        WLine party3 = MakeParty(line8, role3);
        after.Add(party3);
        after.Add(line9);
        after.Add(line10);
        return after;
    }

    private static bool IsTenLinePartyBlock3(IBlock[] before, int i) {  // EWHC/Admin/2006/1205
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
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsFirstPartyType(line3) &&
            IsBetweenPartyMarker(line4) &&
            IsPartyName(line5) &&
            IsPartyName(line6) &&
            IsPartyName(line7) &&
            IsPartyName(line8) &&
            IsSecondPartyType(line9) &&
            IsAfterPartyMarker(line10);
    }
    private static List<IBlock> EnrichTenLinePartyBlock3(IBlock[] before, int i) {
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
        List<IBlock> after = new List<IBlock>(10);
        after.Add(line1);
        PartyRole role1 = GetFirstPartyRole(line3);
        WLine party1 = MakeParty(line2, role1);
        after.Add(party1);
        after.Add(line3);
        after.Add(line4);
        PartyRole role2 = GetSecondPartyRole(line9);
        WLine party2 = MakeParty(line5, role2);
        WLine party3 = MakeParty(line6, role2);
        WLine party4 = MakeParty(line7, role2);
        WLine party5 = MakeParty(line8, role2);
        after.Add(party2);
        after.Add(party3);
        after.Add(party4);
        after.Add(party5);
        after.Add(line9);
        after.Add(line10);
        return after;
    }

    /* eleven */
    private static bool IsElevenLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 11)
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
        bool ok1 = IsBeforePartyMarker(line1);
        bool ok2 = IsPartyNameAndRole(line2);
        bool ok3 = IsBetweenPartyMarker(line3) || IsBetweenPartyMarker2(line3);
        bool ok4 = IsPartyName(line4);
        bool ok5 = IsPartyName(line5);
        bool ok6 = IsPartyName(line6);
        bool ok7 = IsPartyName(line7);
        bool ok8 = IsPartyName(line8);
        bool ok9 = IsPartyName(line9);
        bool ok10 = IsPartyNameAndRole(line10);
        bool ok11 = IsAfterPartyMarker(line11);
        return
            IsBeforePartyMarker(line1) &&
            IsPartyNameAndRole(line2) &&
            (IsBetweenPartyMarker(line3) || IsBetweenPartyMarker2(line3)) &&
            IsPartyName(line4) &&
            IsPartyName(line5) &&
            IsPartyName(line6) &&
            IsPartyName(line7) &&
            IsPartyName(line8) &&
            IsPartyName(line9) &&
            IsPartyNameAndRole(line10) &&
            IsAfterPartyMarker(line11);
    }
    private static List<IBlock> EnrichElevenLinePartyBlock(IBlock[] before, int i) {
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
        List<IBlock> after = new List<IBlock>(11);
        after.Add(line1);
        WLine party1 = MakePartyAndRole(line2);
        after.Add(party1);
        after.Add(line3);
        WLine party8 = MakePartyAndRole(line10);
        PartyRole role2 = (PartyRole) party8.Contents.OfType<IParty>().First().Role;
        WLine party2 = MakeParty(line4, role2);
        WLine party3 = MakeParty(line5, role2);
        WLine party4 = MakeParty(line6, role2);
        WLine party5 = MakeParty(line7, role2);
        WLine party6 = MakeParty(line8, role2);
        WLine party7 = MakeParty(line9, role2);
        after.Add(party2);
        after.Add(party3);
        after.Add(party4);
        after.Add(party5);
        after.Add(party6);
        after.Add(party7);
        after.Add(party8);
        after.Add(line11);
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
        // bool ok1 = IsBeforePartyMarker(line1);
        // bool ok2 = IsPartyName(line2);
        // bool ok3 = IsFirstPartyTpye(line3);
        // bool ok4 = IsBetweenPartyMarker(line4);
        // bool ok5 = IsPartyName(line5);
        // bool ok6 = IsPartyName(line6);
        // bool ok7 = IsPartyName(line7);
        // bool ok8 = IsPartyName(line8);
        // bool ok9 = IsPartyName(line9);
        // bool ok10 = IsPartyName(line10);
        // bool ok11 = IsSecondPartyType(line11);
        // bool ok12 = IsAfterPartyMarker(line12);
        return
            IsBeforePartyMarker(line1) &&
            IsPartyName(line2) &&
            IsFirstPartyType(line3) &&
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
        List<IBlock> after = new List<IBlock>(12);
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

    /* multi-line */

    private static List<IBlock> EnrichMultiLinePartyBockOrNull(IBlock[] rest) {
        if (rest.Length == 0)
            return null;
        int i = 0;
        IBlock line = rest[i];
        if (!IsBeforePartyMarker(line))
            return null;
        List<IBlock> enriched = new List<IBlock>();
        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (IsBeforePartyMarker2(line)) {
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
                return null;
            line = rest[i];
        }
        if (!IsPartyName(line))
            return null;
        List<IBlock> stack = new List<IBlock>();
        stack.Add(line);
        i += 1;
        while (true) {
            if (i == rest.Length)
                return null;
            line = rest[i];
            if (IsFirstPartyType(line)) {
                PartyRole role1 = GetFirstPartyRole(line);
                foreach (IBlock block in stack) {
                    WLine party = MakeParty(block, role1);
                    enriched.Add(party);
                }
                enriched.Add(line);
                i += 1;
                break;
            } else if (IsPartyName(line)) {
                stack.Add(line);
                i += 1;
                continue;
            } else {
                return null;
            }
        }
        stack = null;
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line)) {
            enriched.Add(line);
            i += 1;
        } else {
            return null;
        }
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (!IsPartyName(line))
            return null;
        stack = new List<IBlock>();
        stack.Add(line);
        i += 1;
        while (true) {
            if (i == rest.Length)
                return null;
            line = rest[i];
            if (IsSecondPartyType(line)) {
                PartyRole role2 = GetSecondPartyRole(line);
                foreach (IBlock block in stack) {
                    WLine party = MakeParty(block, role2);
                    enriched.Add(party);
                }
                enriched.Add(line);
                i += 1;
                break;
            } else if (IsPartyName(line)) {
                stack.Add(line);
                i += 1;
                continue;
            } else {
                return null;
            }
        }
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (!IsAfterPartyMarker(line))
            return null;
        enriched.Add(line);
        return enriched;
    }

    /* not tested */
    private static List<IBlock> EnrichMultiLinePartyBockOrNull2(IBlock[] rest) {
        if (rest.Length == 0)
            return null;
        int i = 0;
        IBlock line = rest[i];
        if (!IsBeforePartyMarker(line))
            return null;
        List<IBlock> enriched = new List<IBlock>();
        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (IsBeforePartyMarker2(line)) {
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
                return null;
            line = rest[i];
        }
        if (!IsPartyNameAndRole(line))
            return null;
        WLine party1 = MakePartyAndRole(line);
        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line)) {
            enriched.Add(line);
            i += 1;
        } else {
            return null;
        }
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (!IsPartyNameAndRole(line))
            return null;
        WLine party2 = MakePartyAndRole(line);
        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (!IsAfterPartyMarker(line))
            return null;
        enriched.Add(line);
        return enriched;
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
        if (normalized == "BETWEEN:")
            return true;
        if (normalized == "B E T W E E N:")
            return true;
        if (normalized == "B E T W E E N :")    // EWHC/Fam/2012/4047
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
        if (Regex.IsMatch(wText.Text, @"^IN THE MATTER OF [A-Z]", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(wText.Text, @"^RE: [A-Z]"))   // EWCA/Crim/2007/14
            return true;
        return false;
    }
    private static bool IsInTheMatterOf1(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not WText wText)
            return false;
        return Regex.IsMatch(wText.Text.Trim(), "IN THE MATTER OF", RegexOptions.IgnoreCase);
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
        if (line.Contents.Count() == 1)
            return first is WText;
        if (line.Contents.Count() == 2) {   // EWHC/Fam/2017/3707
            IInline second = line.Contents.Skip(1).First();
            if (first is WTab && second is WText)   // EWHC/Fam/2017/3707
                return true;
            if (first is WText text1 && second is WText text2 && Regex.IsMatch(text1.Text, @"^\(\d\) +$"))   //
                return true;
            return false;
        }
        if (line.Contents.Count() == 3) {   // EWHC/Admin/2012/3928, EWHC/Admin/2007/552
            IInline second = line.Contents.Skip(1).First();
            IInline third = line.Contents.Skip(2).First();
            if (first is not WText wText1)
                return false;
            if (second is not WText wText2)
                return false;
            if (third is not WText wText3)
                return false;
            if (!string.IsNullOrWhiteSpace(wText2.Text))
                return false;
            return IFormattedText.HaveSameFormatting(wText1, wText3);    // not really sure why this should matter
        }
        if (line.Contents.Count() == 4) {   // EWHC/Fam/2017/3707
            IInline second = line.Contents.Skip(1).First();
            IInline third = line.Contents.Skip(2).First();
            IInline fourth = line.Contents.Skip(3).First();
            if (first is not WTab)
                return false;
            if (second is not WText wText1)
                return false;
            if (third is not WText wText2)
                return false;
            if (second is not WText wText3)
                return false;
            if (!string.IsNullOrWhiteSpace(wText2.Text))
                return false;
            return IFormattedText.HaveSameFormatting(wText1, wText3);
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
        }
        if (line.Contents.Count() == 2) {
            IInline first = line.Contents.First();
            WText second = (WText) line.Contents.Skip(1).First();
            WParty party = new WParty(second) { Role = role };
            IEnumerable<IInline> contents = new List<IInline>(2) { first, party };
            return new WLine(line, contents);
        }
        if (line.Contents.Count() == 3) {
            WParty2 party = new WParty2(line.Contents.Cast<IFormattedText>()) { Role = role };
            IEnumerable<IInline> contents = new List<IInline>(1) { party };
            return new WLine(line, contents);
        }
        if (line.Contents.Count() == 4) {
            WTab first = (WTab) line.Contents.First();
            WParty2 party = new WParty2(line.Contents.Skip(1).Cast<IFormattedText>()) { Role = role };
            IEnumerable<IInline> contents = new List<IInline>(2) { first, party };
            return new WLine(line, contents);
        }
        throw new Exception();
    }

    private static bool IsAnyPartyType(string s) {
        // s = Regex.Replace(s, @"\s+", " ").Trim();
        if (IsFirstPartyType(s))
            return true;
        if (IsSecondPartyType(s))
            return true;
        return false;
    }
    private static bool IsFirstPartyType(string s) {
        ISet<string> firstPartyTypes = new HashSet<string>() {
            "Claimant", "(Claimant)", "(CLAIMANT)",
            "Claimant/Respondent", "Respondent/Claimant",
            "Applicant", "Applicants", "Claimant/Applicant", "CLAIMANT/APPELLANT",
            "Appellant", "(APPELLANT)", "Appellant/Appellant", "Applicant/Appellant",
            "Petitioner"
        };
        return firstPartyTypes.Contains(s);
    }
    private static bool IsFirstPartyType(IBlock block) {
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return IsFirstPartyType(normalized);
    }
    private static PartyRole GetAnyPartyRole(string s) {
        if (IsFirstPartyType(s))
            return GetFirstPartyRole(s);
        if (IsSecondPartyType(s))
            return GetSecondPartyRole(s);
        throw new System.Exception();
    }
    private static PartyRole GetFirstPartyRole(string s) {
        switch (s) {
            case "Claimant":
            case "(Claimant)":
            case "(CLAIMANT)":
                return PartyRole.Claimant;
            case "Claimant/Respondent":
            case "Respondent/Claimant":
                return PartyRole.Respondent;
            case "Applicant":
            case "Applicants":
            case "Claimant/Applicant":
            case "CLAIMANT/APPELLANT":
                return PartyRole.Applicant;
            case "Appellant":
            case "(APPELLANT)":
            case "Appellant/Appellant":
            case "Applicant/Appellant":
                return PartyRole.Appellant;
            case "Petitioner":
                return PartyRole.Petitioner;
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
        if (line.Contents.Count() == 3) {
            IInline first = line.Contents.First();
            IInline second = line.Contents.Skip(1).First();
            IInline third = line.Contents.Skip(2).First();
            if (first is not WText wText1)
                return false;
            if (second is not WTab)
                return false;
            if (third is not WText wText2)
                return false;
            string s = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
            if (!IsAnyPartyType(s))
                return false;
            return true;
        }
        if (line.Contents.Count() == 4) {
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
        return false;
    }
    private static WLine MakePartyAndRole(IBlock block) {
        WLine line = (WLine) block;
        if (line.Contents.Count() == 3) {
            WText first = (WText) line.Contents.First();
            WTab second = (WTab) line.Contents.Skip(1).First();
            WText third = (WText) line.Contents.Skip(2).First();
            string s = Regex.Replace(third.Text, @"\s+", " ").Trim();
            PartyRole role = GetAnyPartyRole(s);
            List<IInline> contents = new List<IInline>(3) {
                new WParty(first.Text, first.properties) { Role = role },
                second,
                third
            };
            return new WLine(line, contents);
        }
        if (line.Contents.Count() == 4) {
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
        throw new Exception();
    }

    private static bool IsBetweenPartyMarker(IBlock block) {
        ISet<string> betweenPartyMarkers = new HashSet<string>() { "v", "-v-", "- v -" };
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return betweenPartyMarkers.Contains(normalized);
    }
    private static bool IsBetweenPartyMarker2(IBlock block) {
        ISet<string> betweenPartyMarkers = new HashSet<string>() { "and", "-and-", "- and -" }; // EWHC/Admin/2003/3013, EWHC/Fam/2013/3493, EWHC/Fam/2012/4047
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();   // doesn't normalize internal spaces
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return betweenPartyMarkers.Contains(normalized);
    }

    private static bool IsSecondPartyType(string s) {
        ISet<string> secondPartyTypes = new HashSet<string>() {
            "Defendant", "Defendants", "(Defendant)", "(DEFENDANT)",
            "First Defendant", "Second Defendant", "(FIRST DEFENDANT)", "(SECOND DEFENDANT)",
            "Defendant/Appellant", "Defendants/Appellants", "Appellant/First Defendant",
            "Respondent", "Respondents", "(RESPONDENT)", "Defendant/Respondent", "DEFENDANT/RESPONDENT", "Respondent/Respondent"
        };
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
            case "(Defendant)":
            case "(DEFENDANT)":
            case "First Defendant":
            case "Second Defendant":
            case "(FIRST DEFENDANT)":
            case "(SECOND DEFENDANT)":
                return PartyRole.Defendant;
            case "Defendant/Appellant":
            case "Defendants/Appellants":
            case "Appellant/First Defendant":
                return PartyRole.Appellant;
            case "Respondent":
            case "Respondents":
            case "(RESPONDENT)":
            case "Defendant/Respondent":
            case "DEFENDANT/RESPONDENT":
            case "Respondent/Respondent":
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
        IEnumerable<WRow> rows = null;
        if (table.TypedRows.Count() == 3)
            rows = EnrichThreeRowsWithNoRolesOrNull(table.TypedRows);
        if (rows is null)
            rows = EnrichRows(table.TypedRows);
        return new WTable(table.Main, rows);
    }

    private IEnumerable<WRow> EnrichRows(IEnumerable<WRow> rows) {
        return rows.Select(row => EnrichRow(row));
    }

    private static bool IsEmptyCell(ICell cell) {
        return cell.Contents.All(block => block is ILine line && IsEmptyLine(line));
    }
    private static bool IsEmptyCell(WCell cell) {
        return cell.Contents.All(block => block is ILine line && IsEmptyLine(line));
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
        if (!IsEmptyCell(first))
            return row;
        PartyRole? role = GetPartyRole(third);
        if (role is not null) {
            second = EnrichCell(second, role.Value);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        if (!IsEmptyCell(third))
            return row;
        if (IsInTheMatterOfSomething(second)) {
            second = EnrichInTheMatterOfSomething(second);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        return row;
    }

    private IEnumerable<WRow> EnrichThreeRowsWithNoRolesOrNull(IEnumerable<WRow> rows) {    // EWCA/Crim/2007/854
        WRow first = rows.ElementAt(0);
        WRow second = rows.ElementAt(1);
        WRow third = rows.ElementAt(2);
        if (first.Cells.Count() != 3)
            return null;
        if (second.Cells.Count() != 3)
            return null;
        if (third.Cells.Count() != 3)
            return null;
        if (!IsEmptyCell(first.Cells.First()))
            return null;
        if (!IsEmptyCell(first.Cells.Last()))
            return null;
        if (!IsEmptyCell(second.Cells.First()))
            return null;
        if (!IsEmptyCell(second.Cells.Last()))
            return null;
        if (!IsEmptyCell(third.Cells.First()))
            return null;
        if (!IsEmptyCell(third.Cells.Last()))
            return null;
        WCell middle1 = first.TypedCells.Skip(1).First();
        WCell middle2 = second.TypedCells.Skip(1).First();
        WCell middle3 = third.TypedCells.Skip(1).First();
        if (middle1.Contents.Where(block => !IsEmptyLine(block)).Count() != 1)
            return null;
        if (middle2.Contents.Where(block => !IsEmptyLine(block)).Count() != 1)
            return null;
        if (middle3.Contents.Where(block => !IsEmptyLine(block)).Count() != 1)
            return null;
        if (middle1.Contents.Where(block => !IsEmptyLine(block)).First() is not WLine line1)
            return null;
        if (middle2.Contents.Where(block => !IsEmptyLine(block)).First() is not WLine line2)
            return null;
        if (middle3.Contents.Where(block => !IsEmptyLine(block)).First() is not WLine line3)
            return null;
        if (!IsBetweenPartyMarker(line2) && !IsBetweenPartyMarker2(line2))
            return null;
        WCell newMiddle1 = new WCell(middle1.Main, new List<WLine>(1) { MakeParty(line1, null) });
        WCell newMiddle3 = new WCell(middle3.Main, new List<WLine>(1) { MakeParty(line3, null) });
        return new List<WRow>(3) {
            new WRow(first.Main, new List<WCell>(3) {
                first.TypedCells.First(), newMiddle1, first.TypedCells.Last()
            }),
            second,
            new WRow(third.Main, new List<WCell>(3) {
                third.TypedCells.First(), newMiddle3, third.TypedCells.Last()
            })
        };
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
        ISet<string> types = new HashSet<string>() { "Appellant", "APPELLANT", "Appellants", "Defendant/Appellant", "Defendants/Appellants", "Appellants/ Claimants" };
        if (types.Contains(normalized))
            return PartyRole.Appellant;
        types = new HashSet<string>() { "Claimant", "Claimants" };
        if (types.Contains(normalized))
            return PartyRole.Claimant;
        types = new HashSet<string>() { "Applicant", "Applicants", "Respondent/Applicant" };
        if (types.Contains(normalized))
            return PartyRole.Applicant;
        types = new HashSet<string>() { "Defendant", "Defendants", "First Defendant", "Second Defendant", "Third Defendant" };
        if (types.Contains(normalized))
            return PartyRole.Defendant;
        types = new HashSet<string>() { "Respondent", "RESPONDENT", "Respondents", "Claimant/Respondent", "Claimant/ Respondent", "Defendant/Respondent", "Defendant/ Respondent", "Petitioner/Respondent",
            "First Respondent", "Second Respondent", "Third Respondent", "Fourth Respondent",
            "Respond-ents/ Defendants"  // EWCA/Civ/2015/377
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
        if (one == "Appellants/" && two == "Claimants")    // EWCA/Civ/2015/377
            return PartyRole.Appellant;
        if (one == "Respondent" && two == "Intervener")    // EWCA/Civ/2016/176
            return PartyRole.Respondent;
        if (one == "Respondents" && two.StartsWith("Respondent"))    // EWHC/Fam/2013/1956
            return PartyRole.Respondent;
        if (one == "Appellant/" && two == "Claimant")    // EWHC/Ch/2017/541
            return PartyRole.Appellant;
        if (one == "Respondent/" && two == "Defendant")    // EWHC/Ch/2017/541
            return PartyRole.Respondent;
        if (one == "Claimants/" && two == "Appellants") // EWHC/Admin/2016/321
            return PartyRole.Appellant;
        if (one == "Defendants/" && two == "Respondents") // EWHC/Admin/2016/321
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
            if (one == "Respondents" && two == "Appellant" && three == "Respondent")    // EWCA/Civ/2010/180
                return PartyRole.Respondent;
        }
        Func<ILine, bool> defendant = (line) => {
            string normalized = line.NormalizedContent();
            return Regex.IsMatch(normalized, @"^\d(st|nd|rd|th)? Defendant$");
        };
        if (blocks.Cast<ILine>().All(defendant))
            return PartyRole.Defendant;
        Func<ILine, bool> defendant2 = (line) => {  // EWHC/Fam/2003/365
            string normalized = line.NormalizedContent();
            return Regex.IsMatch(normalized, @"^(First|Second|Third|Fourth) Defendant$", RegexOptions.IgnoreCase);
        };
        if (blocks.Cast<ILine>().All(defendant2))
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
                if (line.Contents.Count() == 0)
                    return line;
                if (line.Contents.Count() == 1) {
                    IInline first = line.Contents.First();
                    if (first is not WText wText)
                        return line;
                    if (string.IsNullOrWhiteSpace(wText.Text))
                        return line;
                    string trimmed = wText.Text.Trim();
                    if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
                        return line;
                    if (trimmed == "and")    // EWHC/Fam/2003/365
                        return line;
                    if (trimmed == "-and-")    // EWHC/Fam/2013/1956
                        return line;
                    if (trimmed == "- and –")    // EWHC/Fam/2008/1561
                        return line;
                    WParty party = new WParty(wText) { Role = role };
                    return new WLine(line, new List<IInline>(1) { party });
                }
                if (line.Contents.Count() == 2) {
                    IInline first = line.Contents.First();
                    IInline second = line.Contents.Skip(1).First();
                    if (first is not WText wText1)
                        return line;
                    if (second is not WText wText2)
                        return line;
                    if (string.IsNullOrWhiteSpace(wText1.Text))
                        return line;
                    if (!string.IsNullOrWhiteSpace(wText2.Text))
                        return line;
                    string trimmed = wText1.Text.Trim();
                    if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
                        return line;
                    if (trimmed == "and")
                        return line;
                    if (trimmed == "-and-")
                        return line;
                    if (trimmed == "- and –")
                        return line;
                    WParty party = new WParty(wText1) { Role = role };
                    return new WLine(line, new List<IInline>(2) { party, second });
                }
                return line;
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
