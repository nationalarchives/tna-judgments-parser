
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
            if (IsInTheMatterOf3(before, i)) {
                List<IBlock> enriched3 = EnrichInTheMatterOf3(before, i);
                after.AddRange(enriched3);
                i += 3;
                break;
            }
            if (IsInTheMatterOf4(before, i)) {
                List<IBlock> enriched4 = EnrichInTheMatterOf4(before, i);
                after.AddRange(enriched4);
                i += 4;
                break;
            }
            if (IsThreeLinePartyBlock(before, i)) {
                List<IBlock> enriched3 = EnrichThreeLinePartyBlock(before, i);
                after.AddRange(enriched3);
                i += 3;
                break;
            }
            if (IsFourtLinePartyBlock(before, i)) {
                List<IBlock> enriched4 = EnrichFourLinePartyBlock(before, i);
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
            IBlock[] rest = before[i..];
            List<IBlock> found = EnrichMultiLinePartyBockOrNull(rest);
            // if (found is null)
            //     found = EnrichMultiLinePartyBockOrNull2(rest);
            found ??= EnrichMultiLinePartyBockOrNull2(rest);
            // if (found is null)
            //     found = EnrichMultiLinePartyBockOrNull3(rest);
            found ??= EnrichMultiLinePartyBockOrNull3(rest);
            if (found is not null) {
                after.AddRange(found);
                i += found.Count;
                break;
            }
            IBlock block = before[i];
            var enriched1 = EnrichBlock(block);
            after.Add(enriched1);
            i += 1;
        }
        after.AddRange(before.Skip(i));
        return after;
    }

    private static bool IsInTheMatterOf3(IBlock[] before, int i) {  // EWCA/Civ/2008/1303
        if (i > before.Length - 3)
            return false;
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        return
            IsBeforePartyMarker(line1) &&
            IsInTheMatterOf1(line2) &&
            IsAfterPartyMarker(line3);
    }

    private static List<IBlock> EnrichInTheMatterOf3(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        List<IBlock> after = new List<IBlock>(3);
        after.Add(line1);
        WLine docTitle = MakeDocTitle(line2);
        after.Add(docTitle);
        after.Add(line3);
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

    /* three and four */

    private static bool IsRegina(ILine line) {
        string content = line.NormalizedContent();
        if (content == "REGINA")
            return true;
        return false;
    }

    private static bool IsThreeLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 4)
            return false;
        IBlock block1 = before[i];
        IBlock block2 = before[i+1];
        IBlock block3 = before[i+2];
        IBlock block4 = before[i+3];
        if (block1 is not ILine line1)
            return false;
        if (!IsRegina(line1))
            return false;
        if (!IsBetweenPartyMarker(block2))
            return false;
        if (!IsPartyName(block3))
            return false;
        if (!IsAfterPartyMarker(block4))
            return false;
        return true;
    }
    private static List<IBlock> EnrichThreeLinePartyBlock(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        return new List<IBlock>(3) {
            MakeParty(line1, PartyRole.BeforeTheV),
            line2,
            MakeParty(line3, PartyRole.AfterTheV)
        };
    }

    private static bool IsFourtLinePartyBlock(IBlock[] before, int i) {
        if (i > before.Length - 5)
            return false;
        IBlock block1 = before[i];
        IBlock block2 = before[i+1];
        IBlock block3 = before[i+2];
        IBlock block4 = before[i+3];
        IBlock block5 = before[i+4];
        if (block1 is not ILine line1)
            return false;
        if (!IsRegina(line1))
            return false;
        if (!IsBetweenPartyMarker(block2))
            return false;
        if (!IsPartyName(block3))
            return false;
        if (!IsPartyName(block4))
            return false;
        if (!IsAfterPartyMarker(block5))
            return false;
        return true;
    }
    private static List<IBlock> EnrichFourLinePartyBlock(IBlock[] before, int i) {
        IBlock line1 = before[i];
        IBlock line2 = before[i+1];
        IBlock line3 = before[i+2];
        IBlock line4 = before[i+3];
        return new List<IBlock>(3) {
            MakeParty(line1, PartyRole.BeforeTheV),
            line2,
            MakeParty(line3, PartyRole.AfterTheV),
            MakeParty(line4, PartyRole.AfterTheV)
        };
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
            (IsBetweenPartyMarker(line3) || IsBetweenPartyMarker2(line3)) &&
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
        if (!IsBeforePartyMarker(line) && !IsBeforePartyMarker2(line))
            return null;
        List<IBlock> enriched = new List<IBlock>();
        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (line is ILine inPrivate && inPrivate.NormalizedContent() == "IN PRIVATE") { // EWHC/Admin/2012/2822
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
                return null;
            line = rest[i];
        }
        if (IsBeforePartyMarker2(line)) {
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
                return null;
            line = rest[i];
        }
        List<IBlock> firstGroupOfParites = Magic1(rest[i..]);
        if (firstGroupOfParites is null)
            return null;
        enriched.AddRange(firstGroupOfParites);
        i += firstGroupOfParites.Count;
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
        List<IBlock> secondGroupOfParites = Magic2(rest[i..]);
        if (secondGroupOfParites is null)
            return null;
        enriched.AddRange(secondGroupOfParites);
        i += secondGroupOfParites.Count;
        if (i == rest.Length)
            return null;
        line = rest[i];
        // if (IsAfterPartyMarker(line)) {
        //     enriched.Add(line);
        //     return enriched;
        // }
        if (IsBetweenPartyMarker2(line)) {
            enriched.Add(line);
            i += 1;
            if (i == rest.Length)
                return null;
            line = rest[i];
        }
        List<IBlock> thirdGroupOfParites = Magic2(rest[i..]);
        if (thirdGroupOfParites is not null) {
            enriched.AddRange(thirdGroupOfParites);
            i += thirdGroupOfParites.Count;
        }
        List<IBlock> fourthGroupOfParites = Magic2(rest[i..]);
        if (fourthGroupOfParites is not null) {
            enriched.AddRange(fourthGroupOfParites);
            i += fourthGroupOfParites.Count;
        }
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
        if (!IsBeforePartyMarker(line) && !IsBeforePartyMarker2(line))
            return null;
        List<IBlock> enriched = new List<IBlock>();
        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (IsBeforePartyMarker2(line)) {   // perhaps do this only if first line isn't marker 2
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

    /* this one has two types of parties before the v */
    private static List<IBlock> EnrichMultiLinePartyBockOrNull3(IBlock[] rest) {    // EWHC/Admin/2015/897
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
        /* between */
        if (!IsBeforePartyMarker2(line))
            return null;
        enriched.Add(line);
        i += 1;
        List<IBlock> firstGroupOfParites = Magic1(rest[i..]);
        if (firstGroupOfParites is null)
            return null;
        enriched.AddRange(firstGroupOfParites);
        i += firstGroupOfParites.Count;
        if (i == rest.Length)
            return null;
        line = rest[i];
        /* and */
        if (!IsBetweenPartyMarker2(line))
            return null;
        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
            return null;
        line = rest[i];
        List<IBlock> secondGroupOfParites = Magic1(rest[i..]);
        if (secondGroupOfParites is null)
            return null;
        enriched.AddRange(secondGroupOfParites);
        i += secondGroupOfParites.Count;
        if (i == rest.Length)
            return null;
        line = rest[i];
        /* v */
        if (!IsBetweenPartyMarker(line))
            return null;
        enriched.Add(line);
        i += 1;
        if (i == rest.Length)
            return null;
        line = rest[i];
        List<IBlock> thirdGroupOfParites = Magic2(rest[i..]);
        if (thirdGroupOfParites is null)
            return null;
        enriched.AddRange(thirdGroupOfParites);
        i += thirdGroupOfParites.Count;
        if (i == rest.Length)
            return null;
        line = rest[i];
        if (IsAfterPartyMarker(line)) {
            enriched.Add(line);
            return enriched;
        }
        return null;
    }

    private static List<IBlock> Magic1(IBlock[] rest) {
        return Magic(rest, IsFirstPartyType, GetFirstPartyRole);
    }

    private static List<IBlock> Magic2(IBlock[] rest) {
        return Magic(rest, IsSecondPartyType, GetSecondPartyRole);
    }

    private static List<IBlock> Magic(IBlock[] rest, Func<IBlock, bool> test, Func<IBlock, PartyRole> construct) {
        int i = 0;
        if (i == rest.Length)
            return null;
        IBlock line = rest[i];
        if (!IsPartyName(line))
            return null;
        List<IBlock> stack = new List<IBlock>();
        stack.Add(line);
        i += 1;
        while (true) {
            if (i == rest.Length)
                return null;
            line = rest[i];
            if (test(line)) {
                PartyRole role1 = construct(line);
                List<IBlock> enriched = new List<IBlock>(stack.Count + 1);
                foreach (IBlock block in stack) {
                    WLine party = MakeParty(block, role1);
                    enriched.Add(party);
                }
                enriched.Add(MakeRole(line, role1));
                return enriched;
            } else if (IsPartyName(line)) {
                stack.Add(line);
                i += 1;
                continue;
            } else {
                return null;
            }
        }
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
        if (normalized == "Between :")
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
        if (line.Contents.Count() == 1) {
            IInline first = line.Contents.First();
            if (first is not WText wText)
                return false;
            return Regex.IsMatch(wText.Text.Trim(), "IN THE MATTER OF", RegexOptions.IgnoreCase);
        }
        if (line.Contents.Count() == 3) {   // EWCA/Civ/2008/1303
            IInline first = line.Contents.First();
            if (first is not WText wText)
                return false;
            IInline second = line.Contents.Skip(1).First();
            if (second is not WLineBreak)
                return false;
            return Regex.IsMatch(wText.Text.Trim(), "IN THE MATTER OF", RegexOptions.IgnoreCase);
        }
        return false;
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
        IEnumerable<IInline> contents = line.Contents.Skip(1).Prepend(docTitle);
        return new WLine(line, contents);
    }

    private static bool IsPartyName(IBlock block) {
        if (IsBeforePartyMarker(block))
            return false;
        if (IsBeforePartyMarker2(block))
            return false;
        if (IsBetweenPartyMarker(block))
            return false;
        if (IsBetweenPartyMarker2(block))
            return false;
        if (IsAfterPartyMarker(block))
            return false;
        if (IsFirstPartyType(block))
            return false;
        if (IsSecondPartyType(block))
            return false;
        // if (block is WOldNumberedParagraph np) {
        //     if (np.Contents.Count() != 1)
        //         return false;
        //     return IsPartyName(np.Contents.First());
        // }
        if (block is not ILine line)
            return false;
        if (line.Contents.Count() == 0)
            return false;
        if (line.Contents.All(inline => inline is WText) && line.Contents.Cast<WText>().Any(wText => !string.IsNullOrWhiteSpace(wText.Text)))
            return true;
        IInline first = line.Contents.First();
        if (line.Contents.Count() == 1) {
            if (first is not WText wText1)
                return false;
            if (string.IsNullOrWhiteSpace(wText1.Text))
                return false;
            return true;
        }
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
            return true;    // not same formatting in EWHC/Admin/2004/1823
            // return IFormattedText.HaveSameFormatting(wText1, wText3);    // not really sure why this should matter
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
        if (line.Contents.All(inline => inline is WText) && line.Contents.Cast<WText>().Any(wText => !string.IsNullOrWhiteSpace(wText.Text))) {
            WParty2 party = new WParty2(line.Contents.Cast<WText>()) { Role = role };
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
            "Claimant", "Claimants", "(Claimant)", "(CLAIMANT)", "(CLAIMANTS)", "Claimant/part 20 Defendant",
            "First Claimant", "Second Claimant",
            "Claimant/Respondent", "Claimant/ Respondent", "CLAIMANT/RESPONDENT", "Respondent/Claimant", "Claimants/Respondents", "CLAIMANTS/RESPONDENTS",
            "Respondent",    // EWCA/Civ/2003/1686
            "Applicant", "Applicants", "Claimant/Applicant", "CLAIMANT/APPELLANT",
            "Appellant", "(APPELLANT)", "(APPELLANTS)", "Appellant/Appellant", "Applicant/Appellant", "Appellant/Applicant", "Appellant/Claimant", "Appellants/ Claimants",
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
            case "(CLAIMANTS)":
            case "Claimant/part 20 Defendant":
            case "First Claimant":
            case "Second Claimant":
                return PartyRole.Claimant;
            case "Claimant/Respondent":
            case "Claimant/ Respondent":
            case "CLAIMANT/RESPONDENT":
            case "Respondent/Claimant":
            case "Claimants/Respondents":
            case "CLAIMANTS/RESPONDENTS":
            case "Respondent":
                return PartyRole.Respondent;
            case "Applicant":
            case "Applicants":
            case "Claimant/Applicant":
            case "CLAIMANT/APPELLANT":
                return PartyRole.Applicant;
            case "Appellant":
            case "(APPELLANT)":
            case "(APPELLANTS)":
            case "Appellant/Appellant":
            case "Applicant/Appellant":
            case "Appellant/Applicant":
            case "Appellant/Claimant":
            case "Appellants/ Claimants":
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
        // if (line.Contents.Count() == 3) {
        //     IInline first = line.Contents.First();
        //     IInline second = line.Contents.Skip(1).First();
        //     IInline third = line.Contents.Skip(2).First();
        //     if (first is not WText wText1)
        //         return false;
        //     if (second is not WTab)
        //         return false;
        //     if (third is not WText wText2)
        //         return false;
        //     string s = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
        //     if (!IsAnyPartyType(s))
        //         return false;
        //     return true;
        // }
        // if (line.Contents.Count() == 4) {
        //     IInline first = line.Contents.First();
        //     IInline second = line.Contents.Skip(1).First();
        //     IInline third = line.Contents.Skip(2).First();
        //     IInline fourth = line.Contents.Skip(3).First();
        //     if (first is not WTab)
        //         return false;
        //     if (second is not WText wText1)
        //         return false;
        //     if (third is not WTab)
        //         return false;
        //     if (fourth is not WText wText2)
        //         return false;
        //     string s = Regex.Replace(wText2.Text, @"\s+", " ").Trim();
        //     if (!IsAnyPartyType(s))
        //         return false;
        //     return true;
        // }
        if (line.Contents.Count() >= 3) {
            IEnumerable<IInline> before = line.Contents.SkipLast(3);
            IInline antiPenult = line.Contents.SkipLast(2).Last();
            IInline penult = line.Contents.SkipLast(1).Last();
            IInline last = line.Contents.Last();
            if (!before.All(i => i is WTab))
                return false;
            if (antiPenult is not WText wText1)
                return false;
            if (penult is not WTab)
                return false;
            if (last is not WText wText2)
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
        // if (line.Contents.Count() == 3) {
        //     WText first = (WText) line.Contents.First();
        //     WTab second = (WTab) line.Contents.Skip(1).First();
        //     WText third = (WText) line.Contents.Skip(2).First();
        //     string s = Regex.Replace(third.Text, @"\s+", " ").Trim();
        //     PartyRole role = GetAnyPartyRole(s);
        //     List<IInline> contents = new List<IInline>(3) {
        //         new WParty(first.Text, first.properties) { Role = role },
        //         second,
        //         new WRole() { Role = role, Contents = new List<IInline>(1) { third } }
        //     };
        //     return new WLine(line, contents);
        // }
        // if (line.Contents.Count() == 4) {
        //     WTab first = (WTab) line.Contents.First();
        //     WText second = (WText) line.Contents.Skip(1).First();
        //     WTab third = (WTab) line.Contents.Skip(2).First();
        //     WText fourth = (WText) line.Contents.Skip(3).First();
        //     string s = Regex.Replace(fourth.Text, @"\s+", " ").Trim();
        //     PartyRole role = GetAnyPartyRole(s);
        //     List<IInline> contents = new List<IInline>(4) {
        //         first,
        //         new WParty(second.Text, second.properties) { Role = role },
        //         third,
        //         new WRole() { Role = role, Contents = new List<IInline>(1) { fourth } }
        //     };
        //     return new WLine(line, contents);
        // }
        if (line.Contents.Count() >= 3) {
            IEnumerable<IInline> before = line.Contents.SkipLast(3);
            WText antiPenult = (WText) line.Contents.SkipLast(2).Last();
            WTab penult = (WTab) line.Contents.SkipLast(1).Last();
            WText last = (WText) line.Contents.Last();
            string s = Regex.Replace(last.Text, @"\s+", " ").Trim();
            PartyRole role = GetAnyPartyRole(s);
            IEnumerable<IInline> contents = before.Concat(new List<IInline>(3) {
                new WParty(antiPenult.Text, antiPenult.properties) { Role = role },
                penult,
                new WRole() { Role = role, Contents = new List<IInline>(1) { last } }
            });
            return new WLine(line, contents);
        }
        throw new Exception();
    }

    private static bool IsBetweenPartyMarker(IBlock block) {
        ISet<string> betweenPartyMarkers = new HashSet<string>() { "v", "-v-", "- v -",
            "- v –",   // [2021] EWCA Crim 1412
            "V" // [2021] EWCA Crim 1413
        };
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        var temp = betweenPartyMarkers.Contains(normalized);
        return betweenPartyMarkers.Contains(normalized);
    }
    private static bool IsBetweenPartyMarker2(IBlock block) {
        ISet<string> betweenPartyMarkers = new HashSet<string>() { "and", "-and-", "- and -", "--and--" }; // EWHC/Admin/2003/3013, EWHC/Fam/2013/3493, EWHC/Fam/2012/4047, EWCA/Civ/2013/1506
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        return betweenPartyMarkers.Contains(normalized);
    }

    private static bool IsSecondPartyType(string s) {
        ISet<string> secondPartyTypes = new HashSet<string>() {
            "Defendant", "Defendants", "(Defendant)", "(DEFENDANT)", "(DEFENDANTS)", "Defendant/Part 20 Claimant",
            "First Defendant", "Second Defendant",
            "(FIRST DEFENDANT)", "(SECOND DEFENDANT)", "(1ST DEFENDANT)", "(2ND DEFENDANT)", "(1st DEFENDANT)", "(2nd DEFENDANT)", "(3rd DEFENDANT)",
            "Applicants/Defendants",
            "Defendant/Appellant", "DEFENDANT/APPELLANT", "Defendants/Appellants", "Defendants / Appellants", "Appellant/Defendant", "Appellant/First Defendant",
            "Appellant", // EWCA/Civ/2003/1686
            "Respondent", "Respondents", "(RESPONDENT)", "(RESPONDENTS)", "Defendant/Respondent", "DEFENDANT/RESPONDENT", "DEFENDANTS/RESPONDENTS", "Respondent/Respondent", "Respondents/Respondents", "Respondents/Defendants", "Respondents/ Defendants",
            "Respondnet",  // EWHC/Admin/2010/3393
            "First Respondent", "Second Respondent",
            "Interested Party", "Interested Parties", "(INTERESTED PARTY)", "(INTERESTED PARTIES)", "Second Interested Party", "Third Interested Party",
            "FIRST DEFENDANT’S SOLICITOR/APPELLANT"
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
            case "(DEFENDANTS)":
            case "Defendant/Part 20 Claimant":
            case "First Defendant":
            case "Second Defendant":
            case "(FIRST DEFENDANT)":
            case "(SECOND DEFENDANT)":
            case "(1ST DEFENDANT)":
            case "(2ND DEFENDANT)":
            case "(1st DEFENDANT)":
            case "(2nd DEFENDANT)":
            case "(3rd DEFENDANT)":
            case "Applicants/Defendants":
                return PartyRole.Defendant;
            case "Defendant/Appellant":
            case "DEFENDANT/APPELLANT":
            case "Defendants/Appellants":
            case "Defendants / Appellants":
            case "Appellant/Defendant":
            case "Appellant/First Defendant":
            case "Appellant":
            case "FIRST DEFENDANT’S SOLICITOR/APPELLANT":   // EWCA/Civ/2006/1032
                return PartyRole.Appellant;
            case "Respondent":
            case "Respondents":
            case "(RESPONDENT)":
            case "(RESPONDENTS)":
            case "Defendant/Respondent":
            case "DEFENDANT/RESPONDENT":
            case "DEFENDANTS/RESPONDENTS":
            case "Respondent/Respondent":
            case "Respondents/Respondents":
            case "Respondents/Defendants":
            case "Respondents/ Defendants":
            case "First Respondent":
            case "Second Respondent":
            case "Respondnet":  // EWHC/Admin/2010/3393
                return PartyRole.Respondent;
            case "Interested Party":
            case "Interested Parties":
            case "(INTERESTED PARTY)":
            case "(INTERESTED PARTIES)":
            case "Second Interested Party":
            case "Third Interested Party":
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
        if (IsBeforePartyMarker(block))
            return true;
        if (block is not ILine line)
            return false;
        string content = line.NormalizedContent();
        if (content.StartsWith("Computer Aided Transcript"))
            return true;
        if (content.StartsWith("REPORTING RESTRICTIONS APPLY:"))
            return true;
        return false;
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
        return new WTable(table.Main, table.Properties, table.Grid, rows);
    }

    private IEnumerable<WRow> EnrichRows(IEnumerable<WRow> rows) {
        WRow[] before = rows.ToArray();
        WRow[] after = new WRow[before.Length];
        for (int i = 0; i < before.Length; i++) {
            WRow row = before[i];
            WRow enriched = EnrichRow(row);
            if (!Object.ReferenceEquals(row, enriched)) {
                after[i] = enriched;
                continue;
            }
            if (i == before.Length - 1) {
                after[i] = enriched;
                continue;
            }
            WRow next = before[i+1];
            after[i] = EnrichRow(row, next);
        }
        return after;
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
        if (row.Cells.Count() == 2)
            return EnrichRow2(row);
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
            third = EnrichCellWithPartyRole(third, (PartyRole) role);
            return new WRow(row.Table, new List<WCell>(3){ first, second, third });
        }
        // if (!IsEmptyCell(third))
        //     return row;
        if (IsInTheMatterOfSomething(second)) {
            second = EnrichInTheMatterOfSomething(second);
            return new WRow(row.Table, new List<WCell>(3){ first, second, third });
        }
        (PartyRole first, PartyRole second)? twoRoles = GetTwoDifferentRoles(third);
        if (twoRoles is not null) {
            second = EnrichPartyNamesWithTwoRoles(second, twoRoles.Value);
            third = EnrichPartyTypesWithTwoRoles(third, twoRoles.Value);
            return new WRow(row.Table, new List<WCell>(3){ first, second, third });
        }
        return row;
    }

    private WRow EnrichRow2(WRow row) {
        WCell first = (WCell) row.Cells.ElementAt(0);
        WCell second = (WCell) row.Cells.ElementAt(1);
        if (IsEmptyCell(first))
            return row;
        PartyRole? role = GetPartyRole(second);
        if (role is null)
            return row;
        first = EnrichCell(first, role.Value);
        second = EnrichCellWithPartyRole(second, role.Value);
        return new WRow(row.Table, new List<WCell>(2){ first, second });
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
        WCell newMiddle1 = new WCell(middle1.Row, middle1.Contents.Cast<WLine>().Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.BeforeTheV)));
        WCell newMiddle3 = new WCell(middle3.Row, middle3.Contents.Cast<WLine>().Select(line => IsEmptyLine(line) ? line : MakeParty(line, PartyRole.AfterTheV)));
        return new List<WRow>(3) {
            new WRow(first.Table, new List<WCell>(3) {
                first.TypedCells.First(), newMiddle1, first.TypedCells.Last()
            }),
            second,
            new WRow(third.Table, new List<WCell>(3) {
                third.TypedCells.First(), newMiddle3, third.TypedCells.Last()
            })
        };
    }

    private WRow EnrichRow(WRow row, WRow next) {
        if (row.Cells.Count() != 3)
            return row;
        if (next.Cells.Count() != 3)
            return row;
        WCell before = (WCell) row.Cells.ElementAt(0);
        if (!IsEmptyCell(before))
            return row;
        WCell after = (WCell) row.Cells.ElementAt(2);
        if (!IsEmptyCell(after))
            return row;
        if (!IsEmptyCell(next.Cells.ElementAt(0)))
            return row;
        if (!IsEmptyCell(next.Cells.ElementAt(1)))
            return row;
        WCell partyCell = (WCell) row.Cells.ElementAt(1);
        WCell roleCell = (WCell) next.Cells.ElementAt(2);
        if (IsEmptyCell(partyCell))
            return row;
        if (IsEmptyCell(roleCell))
            return row;
        PartyRole? role = GetPartyRole(roleCell);
        if (role is not null) {
            partyCell = EnrichCell(partyCell, role.Value);
            return new WRow(row.Table, new List<WCell>(3){ before, partyCell, after });
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

    private static (PartyRole first, PartyRole second)? GetTwoDifferentRoles(WCell cell) {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block));
        if (lines.Count() != 2)
            return null;
        IBlock block1 = lines.First();
        IBlock block2 = lines.Last();
        PartyRole? role1 = GetOneLinePartyRole(block1);
        if (role1 is null)
            return null;
        PartyRole? role2 = GetOneLinePartyRole(block2);
        if (role2 is null)
            return null;
        if (role1 == role2)
            return null;
        return (role1.Value, role2.Value);
    }

    private static WCell EnrichCellWithPartyRole(WCell cell, PartyRole role) {
        return new WCell(cell.Row, cell.Contents.Cast<WLine>()
            .Select(line => IsEmptyLine(line) ? line : new WLine(line, new List<IInline>(1) { new WRole() { Role = role, Contents = line.Contents } })));
    }

    private static PartyRole? GetOneLinePartyRole(WCell cell) {
        var lines = cell.Contents.Where(block => !IsEmptyLine(block));
        if (lines.Count() != 1)
            return null;
        IBlock block = lines.First();
        return GetOneLinePartyRole(block);
    }

    private static PartyRole? GetOneLinePartyRole(IBlock block) {
        if (block is not ILine line)
            return null;
        string normalized = line.NormalizedContent();
        ISet<string> types = new HashSet<string>() { "Appellant", "APPELLANT", "Appellants", "Defendant/Appellant", "Defendant/ Appellant", "Defendants/Appellants", "Defendants/Appellants/", "Appellant/ Defendant", "Appellants/Claimants", "Appellants/ Claimants", "Claimant/Appellant", "Claimant/ Appellant", "Claimant / Appellant", "Appellant / Claimant", "Appellant / Third Defendant",
            "1st Appellant", "Respondent/Appellant", "Defendants/ Appellants",
            "Appellant/ Respondent", // [2021] EWCA Civ 1792
            "Claimants/ Appellants",    // [2021] EWCA Civ 1799
            "Appellant/Applicant",  // [2021] EWCA Crim 1877
        };
        if (types.Contains(normalized))
            return PartyRole.Appellant;
        types = new HashSet<string>() { "Claimant", "Claimants", "Claimant/Part 20 Defendant", "Claimant/part 20 Defendant" };
        if (types.Contains(normalized))
            return PartyRole.Claimant;
        types = new HashSet<string>() { "Applicant", "Applicants", "Respondent/Applicant", "Applicants/Claimants", "Applicant/ Claimant", "Claimant/Applicant", "Defendant/ Applicant", "1st Applicant", "2nd Applicant" };
        if (types.Contains(normalized))
            return PartyRole.Applicant;
        types = new HashSet<string>() { "Defendant", "Defendants", "Defendant/Part 20 Claimant", "First Defendant", "Second Defendant", "Third Defendant" };
        if (types.Contains(normalized))
            return PartyRole.Defendant;
        types = new HashSet<string>() { "Respondent", "RESPONDENT", "Respondents",
            "Claimant/Respondent", "Claimant/ Respondent", "Claimant / Respondent", "Clamaints/ Respondents", "Respondent/Claimant", "Respondent/ Claimant",
            "Defendant/Respondent", "Defendant/ Respondent", "Defendant / Respondent", "Defendants/Respondents", "Petitioner/Respondent",
            "First Respondent", "Second Respondent", "Third Respondent", "Fourth Respondent",
            "1st Respondent", "2nd Respondent", "3rd Respondent",   // EWCA/Civ/2012/378
            "Respondents/Defendants", "Respond-ents/ Defendants", "Respondents/ Defendants",  // EWCA/Civ/2015/377, EWHC/QB/2006/582
            "Respondent/Defendants", "Respondent / Defendant",
            "Respondents Second and Third/ Defendants",  // EWCA/Civ/2004/1249
            "Respondent/Petitioner", // [2021] EWCA Civ 1792
            "Respondents/Claimants",
        };
        if (types.Contains(normalized))
            return PartyRole.Respondent;
        types = new HashSet<string>() { "Petitioner" };
        if (types.Contains(normalized))
            return PartyRole.Petitioner;
        types = new HashSet<string>() { "Interested Party", "Interested parties" };
        if (types.Contains(normalized))
            return PartyRole.InterestedParty;
        return null;
    }

    private static PartyRole? GetTwoLinePartyRole(WCell cell) {
        var lines = cell.Contents;
        while (lines.Any() && IsEmptyLine(lines.First()))
            lines = lines.Skip(1);
        while (lines.Any() && IsEmptyLine(lines.Last()))
            lines = lines.SkipLast(1);
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
        return GetTwoLinePartyRole(one, two);
    }

    private static PartyRole? GetTwoLinePartyRole(string one, string two) {
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
        if (one == "Appellants/" && two == "Defendants")
            return PartyRole.Appellant;
        if (one == "Appellants/" && two == "Defendants & Counterclaimants")    // EWCA/Civ/2017/97
            return PartyRole.Appellant;
        if (one == "Appellants/" && two == "Claimants")    // EWCA/Civ/2015/377
            return PartyRole.Appellant;
        if (one == "Appellants" && two == "Claimants")    // EWCA/Civ/2018/601
            return PartyRole.Appellant;
        if (one == "Respondent" && two == "Intervener")    // EWCA/Civ/2016/176
            return PartyRole.Respondent;
        if (one == "Respondents" && two.StartsWith("Respondent"))    // EWHC/Fam/2013/1956
            return PartyRole.Respondent;
        if (one == "Appellant/" && two == "Claimant")    // EWHC/Ch/2017/541
            return PartyRole.Appellant;
        if (one == "Appellant" && two == "/Claimant")    // [2021] EWHC 3453 (QB)
            return PartyRole.Appellant;
        if (one == "Appellant/" && two == "Defendant")    // EWHC/QB/2013/196
            return PartyRole.Appellant;
        if (one == "Respondent/" && two == "Defendant")    // EWHC/Ch/2017/541
            return PartyRole.Respondent;
        if (one == "Respondent" && two == "Defendant")    // EWCA/Civ/2018/601
            return PartyRole.Respondent;
        if (one == "1st Respondent" && two == "/Defendant")    // [2021] EWHC 3453 (QB)
            return PartyRole.Respondent;
        if (one == "Claimants/" && two == "Appellants") // EWHC/Admin/2016/321
            return PartyRole.Appellant;
        if (one == "Defendant/" && two == "Respondent") // EWHC/Admin/2015/1639
            return PartyRole.Respondent;
        if (one == "Defendants/" && two == "Respondents") // EWHC/Admin/2016/321
            return PartyRole.Respondent;
        if (one == "Defendants/" && two == "Appellants") // EWCA/Civ/2004/277
            return PartyRole.Appellant;
        if (one == "Defendants/" && two == "Applicants") // [2021] EWHC 2684 (Comm)
            return PartyRole.Applicant;
        if (one == "1st Respondent" && two == "2nd Respondent") // EWHC/Fam/2017/364, EWHC/Fam/2013/1864?
            return PartyRole.Respondent;
        if (one == "1st Respondent" && two == "2ndRespondent") // EWCA/Civ/2011/1253
            return PartyRole.Respondent;
        if (one == "First Defendant" && two == "Second Defendant") // EWHC/Admin/2010/2
            return PartyRole.Defendant;
        if (one == "Defendant/Part 20 Claimant" && two == "Part 20 Claimant")    // EWHC/Ch/2003/812
            return PartyRole.Defendant;
        if (one == "1st Defendant/Part 20 Claimant" && two == "2nd Defendant/Part 20 Defendant")    // EWHC/QB/2004/1260
            return PartyRole.Defendant;
        if (one == "Defendant/" && two == "Cross appellant")    // EWHC/QB/2013/652
            return PartyRole.Defendant; // ??? other role is Appellant
        // if (one == "" && two == "")    //
        //     return PartyRole.;
        if (one == "Applicant/" && two == "Respondent")    // [2021] EWCA Civ 1725
            return PartyRole.Respondent;
        if (one == "Appellant/" && two == "Respondent")    // [2020] EWHC 3409 (QB)
            return PartyRole.Respondent;
        return null;
    }

    private static PartyRole? GetNLinePartyRole(WCell cell) {
        var blocks = cell.Contents.Where(block => !IsEmptyLine(block));
        if (blocks.Count() < 2)
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
            return Regex.IsMatch(normalized, @"^(\d(st|nd|rd|th)? ?)?Respondent$", RegexOptions.IgnoreCase); // EWFC/HCJ/2014/34, no space in EWHC/Fam/2013/1864
        };
        if (blocks.Cast<ILine>().All(respondent))
            return PartyRole.Respondent;
        Func<ILine, bool> respondent2 = (line) => {
            string normalized = line.NormalizedContent();
            return Regex.IsMatch(normalized, @"^(First|Second|Third|Fourth) Respondent$", RegexOptions.IgnoreCase);
        };
        if (blocks.Cast<ILine>().All(respondent2))
            return PartyRole.Respondent;
        // if (blocks.Count() == 4) {
        //     string one = ((ILine) blocks.First()).NormalizedContent();
        //     string two = ((ILine) blocks.ElementAt(1)).NormalizedContent();
        //     string three = ((ILine) blocks.ElementAt(2)).NormalizedContent();
        //     string four = ((ILine) blocks.ElementAt(3)).NormalizedContent();
        //     if (string.IsNullOrWhiteSpace(three) && four == "Intervener")
        //         return GetTwoLinePartyRole(one, two);
        // }
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
                Func<IInline, bool> filter = inline => {
                    if (inline is not WText wText)
                        return false;
                    if (string.IsNullOrWhiteSpace(wText.Text))
                        return false;
                    string trimmed = wText.Text.Trim();
                    if (trimmed.StartsWith('(') && trimmed.EndsWith(')') && !trimmed.StartsWith("(on the application of", StringComparison.InvariantCultureIgnoreCase))
                        return false;
                    if (trimmed == "and")    // EWHC/Fam/2003/365
                        return false;
                    if (trimmed == "-and-")    // EWHC/Fam/2013/1956
                        return false;
                    if (trimmed == "- and –")    // EWHC/Fam/2008/1561
                        return false;
                    if (trimmed == "- and -")   // EWHC/Fam/2017/364
                        return false;
                    if (trimmed == "And")   // EWHC/Admin/2010/2
                        return false;
                    if (trimmed == "- and-")    // EWHC/Comm/2016/146
                        return false;
                    return true;
                };
                IEnumerable<IInline> filtered = line.Contents.Where(filter);
                if (filtered.Count() == 1) {
                    IEnumerable<IInline> mapped = line.Contents.Select(inline => filter(inline) ? new WParty((WText) inline) { Role = role } : inline);
                    return new WLine(line, mapped);
                }
                if (line.Contents.Any(inline => inline is WText wt && Regex.IsMatch(wt.Text, @"^\(\d+\) ")) && line.Contents.All(inline => inline is WLineBreak || inline is WText wt && (string.IsNullOrEmpty(wt.Text) || Regex.IsMatch(wt.Text, @"^\(\d+\) ")))) {
                    IEnumerable<IInline> mapped = line.Contents.Select(inline => {
                        if (inline is WText wt) {
                            if (string.IsNullOrEmpty(wt.Text))
                                return inline;
                            return new WParty(wt) { Role = role };
                        }
                        return inline;
                    });
                    return new WLine(line, mapped);
                }
                /* these should be rewritten so they do nothing if their conditions aren't met (instead of returning) */
                if (line.Contents.Count() == 1) {
                    IInline first = line.Contents.First();
                    if (first is not WText wText)
                        return line;
                    if (string.IsNullOrWhiteSpace(wText.Text))
                        return line;
                    string trimmed = wText.Text.Trim();
                    if (trimmed.StartsWith('(') && trimmed.EndsWith(')') && !trimmed.StartsWith("(on the application of", StringComparison.InvariantCultureIgnoreCase))
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
                if (line.Contents.Count() == 3) {   // [2021] EWCA Civ 1876
                    IInline first = line.Contents.First();
                    IInline second = line.Contents.Skip(1).First();
                    IInline third = line.Contents.Skip(2).First();
                    if (first is WText wText1)
                        if (second is WLineBreak)
                            if (third is WText)
                                if (wText1.Text == "SECRETARY OF STATE ") {
                                    WParty2 party = new WParty2(line.Contents.Cast<ITextOrWhitespace>()) { Role = role };
                                    return new WLine(line, new List<IInline>(1) { party });
                                }
                }
                if (line.Contents.Count() == 3) {   // EWHC/Ch/2018/2498
                    IInline first = line.Contents.First();
                    IInline second = line.Contents.Skip(1).First();
                    IInline third = line.Contents.Skip(2).First();
                    if (first is not WText wText1)
                        return line;
                    if (second is not WTab)
                        return line;
                    if (third is not WText wText3)
                        return line;
                    if (!Regex.IsMatch(wText1.Text, @"^\d\.$"))
                        return line;
                    string trimmed = wText3.Text.Trim();
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
                    WParty party = new WParty(wText3) { Role = role };
                    return new WLine(line, new List<IInline>(3) { first, second, party });
                }
                return line;
            }
        );
        return new WCell(cell.Row, contents);
    }

    private WCell EnrichPartyNamesWithTwoRoles(WCell cell, (PartyRole first, PartyRole second) roles) {
        List<IBlock> contents = new List<IBlock>(cell.Contents.Count());
        bool firstPartyFound = false;
        bool andFound = false;
        bool secondPartyFound = false;
        ISet<string> ands = new HashSet<string>() {
            "and", "-and-", "- and –", "- and -", "And", "- and-"
        };
        foreach (IBlock block in cell.Contents) {
            if (block is not WLine line)
                return cell;
            if (IsEmptyLine(block)) {
                contents.Add(block);
                continue;
            }
            if (line.Contents.Count() == 1) {

                IInline first = line.Contents.First();
                if (first is not WText wText)
                    return cell;
                if (string.IsNullOrWhiteSpace(wText.Text)) {
                    contents.Add(block);
                    continue;
                }
                string trimmed = wText.Text.Trim();
                if (trimmed.StartsWith('(') && trimmed.EndsWith(')')) {
                    contents.Add(block);
                    continue;
                }
                if (ands.Contains(trimmed)) {
                    andFound = true;
                    contents.Add(block);
                    continue;
                }
                if (andFound) {
                    secondPartyFound = true;
                    WParty party = new WParty(wText) { Role = roles.second };
                    WLine newLine = new WLine(line, new List<IInline>(1) { party });
                    contents.Add(newLine);
                } else {
                    firstPartyFound = true;
                    WParty party = new WParty(wText) { Role = roles.first };
                    WLine newLine = new WLine(line, new List<IInline>(1) { party });
                    contents.Add(newLine);
                }

            } else if (line.Contents.Count() == 2) {    // EWHC/Admin/2016/176

                IInline first = line.Contents.First();
                IInline second = line.Contents.ElementAt(1);
                if (first is not WText wText1) {
                    contents.Add(block);
                    continue;
                }
                if (second is not WText wText2) {
                    contents.Add(block);
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(wText1.Text)) {
                    contents.Add(block);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(wText2.Text)) {
                    contents.Add(block);
                    continue;
                }
                string trimmed = wText2.Text.Trim();
                if (trimmed.StartsWith('(') && trimmed.EndsWith(')')) {
                    contents.Add(block);
                    continue;
                }
                if (ands.Contains(trimmed)) {
                    andFound = true;
                    contents.Add(block);
                    continue;
                }
                if (andFound) {
                    secondPartyFound = true;
                    WParty party = new WParty(wText2) { Role = roles.second };
                    WLine newLine = new WLine(line, new List<IInline>(2) { first, party });
                    contents.Add(newLine);
                } else {
                    firstPartyFound = true;
                    WParty party = new WParty(wText2) { Role = roles.first };
                    WLine newLine = new WLine(line, new List<IInline>(2) { first, party });
                    contents.Add(newLine);
                }

            } else {
                contents.Add(block);
                continue;
            }
        }
        if (!firstPartyFound)
            return cell;
        if (!andFound)
            return cell;
        if (!secondPartyFound)
            return cell;
        return new WCell(cell.Row, contents);
    }
    private WCell EnrichPartyTypesWithTwoRoles(WCell cell, (PartyRole first, PartyRole second) roles) {
        List<IBlock> contents = new List<IBlock>(cell.Contents.Count());
        bool firstPartyFound = false;
        bool emptyAfterFirstFound = false;
        foreach (IBlock block in cell.Contents) {
            if (IsEmptyLine(block)) {
                if (firstPartyFound)
                    emptyAfterFirstFound = true;
                contents.Add(block);
                continue;
            }
            if (block is not WLine line)
                return cell;
            if (emptyAfterFirstFound) {
                WRole role = new WRole() { Contents = line.Contents, Role = roles.second };
                WLine newLine = new WLine(line, new List<IInline>(1) { role });
                contents.Add(newLine);
            } else {
                firstPartyFound = true;
                WRole role = new WRole() { Contents = line.Contents, Role = roles.first };
                WLine newLine = new WLine(line, new List<IInline>(1) { role });
                contents.Add(newLine);
            }
        }
        // if (!firstPartyFound)
        //     return cell;
        if (!emptyAfterFirstFound)
            return cell;
        return new WCell(cell.Row, contents);
    }

    private static bool IsInTheMatterOfSomething(WCell cell) {
        if (cell.Contents.Count() != 1)
            return false;
        return IsInTheMatterOfSomething(cell.Contents.First());
    }
    private WCell EnrichInTheMatterOfSomething(WCell cell) {
        WLine line = MakeDocTitle(cell.Contents.First());
        return new WCell(cell.Row, new List<IBlock>(1) { line });
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
