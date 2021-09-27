
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
            List<IBlock> found = EnrichMultiLinePartyBockOrNull(rest);
            if (found is null)
                found = EnrichMultiLinePartyBockOrNull2(rest);
            if (found is not null) {
                after.AddRange(found);
                i += found.Count;
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
        WLine party1 = MakeParty(line2, PartyRole.BeforeTheV);
        after.Add(party1);
        after.Add(line3);
        WLine party2 = MakeParty(line4, PartyRole.AfterTheV);
        after.Add(party2);
        after.Add(line5);
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
                enriched.Add(MakeRole(line, role1));
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
        /* no "v" or "and" in EWHC/Comm/2013/3920 */
        if (IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line)) {
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
                return null;
            line = rest[i];
        // } else {
        //     return null;
        }
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
                enriched.Add(MakeRole(line, role2));
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
        if (IsAfterPartyMarker(line)) {
            enriched.Add(line);
            return enriched;
        }
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
                PartyRole role3 = GetSecondPartyRole(line);
                foreach (IBlock block in stack) {
                    WLine party = MakeParty(block, role3);
                    enriched.Add(party);
                }
                enriched.Add(MakeRole(line, role3));
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
        if (IsAfterPartyMarker(line)) {
            enriched.Add(line);
            return enriched;
        }
        return null;
    }

    private static List<IBlock> EnrichMultiLinePartyBockOrNull2(IBlock[] rest) {    // EWHC/Admin/2018/3311
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
        enriched.Add(party1);
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
        enriched.Add(party2);
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
            return first is WText wText1 && !string.IsNullOrWhiteSpace(wText1.Text);
        if (line.Contents.Count() == 2) {
            IInline second = line.Contents.Skip(1).First();
            if (first is WTab && second is WText wText2 && !string.IsNullOrWhiteSpace(wText2.Text))   // EWHC/Fam/2017/3707
                return true;
            if (first is WText wText3 && second is WText wText4 && !string.IsNullOrWhiteSpace(wText3.Text) && string.IsNullOrWhiteSpace(wText4.Text))   // EWCA/Crim/2014/465
                return true;
            if (first is WText wText5 && second is WText wText6 && Regex.IsMatch(wText5.Text, @"^\(\d\) +$") && !string.IsNullOrWhiteSpace(wText6.Text) )   //
                return true;
            if (first is WText wText7 && second is WText wText8 && Regex.IsMatch(wText7.Text, @"^\d\. +$") && !string.IsNullOrWhiteSpace(wText8.Text) )   // EWCA/Civ/2004/993
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
            if (string.IsNullOrWhiteSpace(second.Text)) {
                WParty party = new WParty((WText) first) { Role = role };
                IEnumerable<IInline> contents = new List<IInline>(2) { party, second };
                return new WLine(line, contents);
            } else {
                WParty party = new WParty(second) { Role = role };
                IEnumerable<IInline> contents = new List<IInline>(2) { first, party };
                return new WLine(line, contents);
            }
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

    private static WLine MakeRole(IBlock block, PartyRole role) {
        WLine line = (WLine) block;
        return new WLine(line, new List<IInline>(1) { new WRole() { Role = role, Contents = line.Contents } });
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
            "Claimant", "Claimants", "(Claimant)", "(CLAIMANT)", "Claimant/part 20 Defendant",
            "Claimant/Respondent", "Respondent/Claimant", "Claimants/Respondents",
            "Applicant", "Applicants", "Claimant/Applicant", "CLAIMANT/APPELLANT",
            "Appellant", "(APPELLANT)", "Appellant/Appellant", "Applicant/Appellant", "Appellant/Claimant",
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
            case "Claimants":
            case "(Claimant)":
            case "(CLAIMANT)":
            case "Claimant/part 20 Defendant":
                return PartyRole.Claimant;
            case "Claimant/Respondent":
            case "Respondent/Claimant":
            case "Claimants/Respondents":
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
            case "Appellant/Claimant":
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
                new WRole() { Role = role, Contents = new List<IInline>(1) { third } }
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
                new WRole() { Role = role, Contents = new List<IInline>(1) { fourth } }
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
        ISet<string> betweenPartyMarkers = new HashSet<string>() { "and", "-and-", "- and -", "--and--" }; // EWHC/Admin/2003/3013, EWHC/Fam/2013/3493, EWHC/Fam/2012/4047, EWCA/Civ/2013/1506
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();   // doesn't normalize internal spaces
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return betweenPartyMarkers.Contains(normalized);
    }

    private static bool IsSecondPartyType(string s) {
        ISet<string> secondPartyTypes = new HashSet<string>() {
            "Defendant", "Defendants", "(Defendant)", "(DEFENDANT)", "Defendant/Part 20 Claimant",
            "First Defendant", "Second Defendant", "(FIRST DEFENDANT)", "(SECOND DEFENDANT)",
            "Defendant/Appellant", "Defendants/Appellants", "Appellant/Defendant", "Appellant/First Defendant",
            "Respondent", "Respondents", "(RESPONDENT)", "Defendant/Respondent", "DEFENDANT/RESPONDENT", "DEFENDANTS/RESPONDENTS", "Respondent/Respondent", "Respondents/Defendants",
            "Interested Party", "Interested Parties", "(INTERESTED PARTIES)"
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
            case "Defendant/Part 20 Claimant":
            case "First Defendant":
            case "Second Defendant":
            case "(FIRST DEFENDANT)":
            case "(SECOND DEFENDANT)":
                return PartyRole.Defendant;
            case "Defendant/Appellant":
            case "Defendants/Appellants":
            case "Appellant/Defendant":
            case "Appellant/First Defendant":
                return PartyRole.Appellant;
            case "Respondent":
            case "Respondents":
            case "(RESPONDENT)":
            case "Defendant/Respondent":
            case "DEFENDANT/RESPONDENT":
            case "DEFENDANTS/RESPONDENTS":
            case "Respondent/Respondent":
            case "Respondents/Defendants":
                return PartyRole.Respondent;
            case "Interested Party":
            case "Interested Parties":
            case "(INTERESTED PARTIES)":
                return PartyRole.InterestedParty;
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
            return new WRow(row.Main, new List<WCell>(3){ first, second, EnrichCellWithPartyRole(third, (PartyRole) role) });
        }
        if (!IsEmptyCell(third))
            return row;
        if (IsInTheMatterOfSomething(second)) {
            second = EnrichInTheMatterOfSomething(second);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        return row;
    }

    private IEnumerable<WRow> EnrichThreeRowsWithNoRolesOrNull(IEnumerable<WRow> rows) {    // EWCA/Crim/2007/854, EWCA/Crim/2014/465
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
        if (!middle1.Contents.All(block => block is WLine line && (IsEmptyLine(line) || (IsPartyName(line)) && !IsFirstPartyType(line))))
            return null;
        if (!middle2.Contents.All(block => block is WLine line && (IsEmptyLine(line) || IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line))))
            return null;
        if (middle2.Contents.Where(block => !IsEmptyLine(block)).Count() != 1)
            return null;
        // if (!middle2.Contents.Where(block => block is WLine line && (IsBetweenPartyMarker(line) || IsBetweenPartyMarker2(line))).Any())
        //     return null;
        if (!middle3.Contents.All(block => block is WLine line && (IsEmptyLine(line) || (IsPartyName(line)) && !IsSecondPartyType(line))))
            return null;
        WCell newMiddle1 = new WCell(middle1.Main, middle1.Contents.Cast<WLine>().Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.BeforeTheV)));
        WCell newMiddle3 = new WCell(middle3.Main, middle3.Contents.Cast<WLine>().Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.AfterTheV)));
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

    private static WCell EnrichCellWithPartyRole(WCell cell, PartyRole role) {
        return new WCell(cell.Main, cell.Contents.Cast<WLine>()
            .Select(line => IsEmptyLine(line) ? line : new WLine(line, new List<IInline>(1) { new WRole() { Role = role, Contents = line.Contents } })));
    }

    private static PartyRole? GetOneLinePartyRole(WCell cell) {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block));
        if (lines.Count() != 1)
            return null;
        IBlock block = lines.First();
        if (block is not ILine line)
            return null;
        string normalized = line.NormalizedContent();
        ISet<string> types = new HashSet<string>() { "Appellant", "APPELLANT", "Appellants", "Defendant/Appellant", "Defendants/Appellants", "Appellants/ Claimants", "Claimant/Appellant", "Appellant / Claimant" };
        if (types.Contains(normalized))
            return PartyRole.Appellant;
        types = new HashSet<string>() { "Claimant", "Claimants", "Claimant/Part 20 Defendant", "Claimant/part 20 Defendant" };
        if (types.Contains(normalized))
            return PartyRole.Claimant;
        types = new HashSet<string>() { "Applicant", "Applicants", "Respondent/Applicant", "Applicants/Claimants" };
        if (types.Contains(normalized))
            return PartyRole.Applicant;
        types = new HashSet<string>() { "Defendant", "Defendants", "Defendant/Part 20 Claimant", "First Defendant", "Second Defendant", "Third Defendant" };
        if (types.Contains(normalized))
            return PartyRole.Defendant;
        types = new HashSet<string>() { "Respondent", "RESPONDENT", "Respondents", "Claimant/Respondent", "Claimant/ Respondent", "Defendant/Respondent", "Defendant/ Respondent", "Respondent / Defendant", "Petitioner/Respondent",
            "First Respondent", "Second Respondent", "Third Respondent", "Fourth Respondent",
            // "1st Respondent", "2nd Respondent",
            "Respondents/Defendants", "Respond-ents/ Defendants"  // EWCA/Civ/2015/377
        };
        if (types.Contains(normalized))
            return PartyRole.Respondent;
        types = new HashSet<string>() { "Petitioner" };
        if (types.Contains(normalized))
            return PartyRole.Petitioner;
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
        if (one == "1st Respondent" && two == "2nd Respondent") // EWHC/Fam/2017/364
            return PartyRole.Respondent;
        if (one == "1st Respondent" && two == "2ndRespondent") // EWCA/Civ/2011/1253
            return PartyRole.Respondent;
        if (one == "First Defendant" && two == "Second Defendant") // EWHC/Admin/2010/2
            return PartyRole.Defendant;
        if (one == "Defendant/Part 20 Claimant" && two == "Part 20 Claimant")    // EWHC/Ch/2003/812
            return PartyRole.Defendant;
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
                    if (trimmed == "- and -")   // EWHC/Fam/2017/364
                        return line;
                    if (trimmed == "And")   // EWHC/Admin/2010/2
                        return line;
                    if (trimmed == "- and-")    // EWHC/Comm/2016/146
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
                    if (trimmed == "- and -")
                        return line;
                    if (trimmed == "And")
                        return line;
                    if (trimmed == "- and-")
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
