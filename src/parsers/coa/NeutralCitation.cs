
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Judgments.Parse {

class NetrualCitation : Enricher2 {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        return Enumerable.Concat(
            blocks.Take(10).Select(Enrich),
            blocks.Skip(10)
        );
    }

    private static readonly string[] patterns = {
        @"^ ?Neutral Citation( Number| No)?[:\.]? *(\[\d{4}\] EWCA (Civ|Crim) \d+)",
        @"^ *Neutral [Cc]itation( +[Nn]umber| No)? ?[:\.]? *(\[\d{4}\]? EWHC +\d+ +\((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\.?\))",    // . in EWHC/Comm/2007/197
        @"^Neutral Citation( Number| No)?:? +(\[\d{4}\] EWHC \d+ (Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC))",  // EWHC/Admin/2003/301
        @"^Neutral Citation( Number| No)?:? +(\[\d{4}\] EWCH \d+ \((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\))",   // EWHC/Admin/2006/2373
        @"^Neutral Citation( Number| No)?:? +(\[\d{4}\] EHWC \d+ \((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\))",   // [2022] EHWC 950 (Ch)
        @"^Neutral Citation( Number| No)?:? (\[\d{4}\] EWCOP \d+)",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWFC \d+( \(B\))?)",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWCA \d+ \((Civ|Crim)\))",   // EWCA/Civ/2017/1798
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWCA \d+ (Civ|Crim))",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EAT \d+)"
    };
    private static readonly string[] patterns2 = {
        @"^\s*(\[\d{4}\] EWCA (Civ|Crim) \d+)", // \s matches non-breaking space in [2022] EWCA Crim 733
        @"^ *(\[?\d{4}\] EWHC \d+ \((Admin|Admlty\.?|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\))",  // period after Admlty in EWHC/Admlty/2003/320
        @"^\s(\[\d{4}\] EWHC \d+ \(Admin\))",  // non-space in [2022] EWHC 307 (Admin)
        @"^(\[\d{4}\] EWHC \[\d+\] \((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\))$",    // [2021] EWHC [3505] (IPEC)
        @"^Neutral Citation Nunber: (\[\d{4}\] EWCA (Civ|Crim) \d+)",    // misspelling in EWCA/Civ/2006/1507
        @"^Neutral Citation Numer: (\[\d{4}\] EWHC \d+ \(Ch\))$", // misspelling in EWHC/Ch/2015/411
        @"^NCN:? (\[\d{4}\] EWCA (Civ|Crim) \d+)$",    // [2021] EWCA Crim 1412
        @"^NCN No: (\[\d{4}\] EWCA (Civ|Crim) \d+)$",    // [2022] EWCA Crim 39
        @"(\[\d{4}\] EWFC \d+( \(B\))?)",
        @"^Neutral Citation Number: (\[\d{4}\[ EWCA (Civ|Crim) \d+)",   // [2018[ EWCA Civ 1744
        @"^(\[\d{4}\] EWCOP \d+)$", //[2021] EWCOP 67
        @"^ *(\[?\d{4}\]? EAT \d+)$",
        @"^Neutral Citation Number:? (\[\d{4}\] UKIPTrib \d+)"
    };

    private static Group Match(string text) {
        foreach (string pattern in patterns) {
            Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[2];
        }
        return null;
    }
    private static Group Match2(string text) {
        foreach (string pattern in patterns2) {
            Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1];
        }
        return null;
    }

    private static List<IInline> Replace(string text, Group group, RunProperties rProps) {
        List<IInline> replacement = new List<IInline>(3);
        if (group.Index > 0) {
            string before1 = text.Substring(0, group.Index);
            WText before2 = new WText(before1, rProps);
            replacement.Add(before2);
        }
        string during1 = group.Value;
        WNeutralCitation during2 = new WNeutralCitation(during1, rProps);
        replacement.Add(during2);
        string after1 = text.Substring(group.Index + group.Length);
        if (!string.IsNullOrEmpty(after1)) {
            WText after2 = new WText(after1, rProps);
            replacement.Add(after2);
        }
        return replacement;
    }

    private static List<IInline> Replace(WText fText, Group group) {
        return Replace(fText.Text, group, fText.properties);
    }

    private IEnumerable<T> Concat3<T>(IEnumerable<T> one, IEnumerable<T> two, IEnumerable<T> three) {
        return Enumerable.Concat(Enumerable.Concat(one, two), three);
    }
    private IEnumerable<T> Concat3<T>(T one, IEnumerable<T> two, IEnumerable<T> three) {
        return Enumerable.Concat(two.Prepend(one), three);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (line.Any()) {
            IInline first = line.First();
            if (first is WText fText) {
                if (fText.Text.Contains("linked"))  // [2023] EWFC 194 & 195
                    return CaseLawRef.EnrichFromEnd(line, @"(\[\d{4}\] EWFC \d+( \(B\))?)\.?$");
                Group group = Match(fText.Text);
                if (group is null)
                    group = Match2(fText.Text);
                if (group is not null) {
                    List<IInline> replacement = Replace(fText, group);
                    IEnumerable<IInline> rest = line.Skip(1);
                    return Enumerable.Concat(replacement, rest);
                }
            }
            IInline last = line.Last();
            if (last is WText fText2) {
                Group group = Match(fText2.Text);
                if (group is null)
                    group = Match2(fText2.Text);
                if (group is not null) {
                    if (first is WText fText1 && fText1.Text.Contains("linked"))  // [2023] EWFC 169 & 170
                        return CaseLawRef.EnrichFromEnd(line, @"(\[\d{4}\] EWFC \d+( \(B\))?)\.?$");
                    IEnumerable<IInline> before = line.SkipLast(1);
                    List<IInline> replacement = Replace(fText2, group);
                    return Enumerable.Concat(before, replacement);
                }
            }
        }
        if (line.Count() > 1) {
            IInline first = line.First();
            IInline second = line.Skip(1).First();
            if (first is WText fText1 && second is WText fText2) {
                if (fText1.Text.Trim() == "Neutral Citation Number:") {
                    Group group = Match2(fText2.Text);
                    if (group is not null) {
                        List<IInline> replacement = Replace(fText2, group);
                        IEnumerable<IInline> rest = line.Skip(2);
                        return Enumerable.Concat(replacement, rest).Prepend(first);
                    }
                }
                if (fText1.Text == "Neutral Citation Number: [" || fText1.Text == "Neutral Citation Number:  [" || fText1.Text == "Neutral Citation No. [") {  // EWHC/Admin/2004/584, EWHC/Admin/2014/1564, EWHC/Ch/2009/1908
                    Group group = Match2("[" + fText2.Text);
                    if (group is not null) {
                        WText label = new WText(fText1.Text.Substring(0, fText1.Text.Length - 1), fText1.properties);
                        WNeutralCitation nc = new WNeutralCitation("[" + fText2.Text, fText2.properties);
                        IEnumerable<IInline> rest = line.Skip(2);
                        return rest.Prepend(nc).Prepend(label);
                    }
                }
                if (fText1.Text == "Neutral Citation Number" && fText2.Text.StartsWith(": ")) { // EWHC/Comm/2005/279
                    Group group = Match2(fText2.Text.Substring(2));
                    if (group is not null) {
                        WText split = new WText(fText2.Text.Substring(0, 2), fText2.properties);
                        WNeutralCitation nc = new WNeutralCitation(fText2.Text.Substring(2), fText2.properties);
                        IEnumerable<IInline> rest = line.Skip(2);
                        return new List<IInline>(3) { fText1, split, nc }.Concat(rest);
                    }
                }
                if (fText1.Text == "Neutral Citation figure: [") {   // EWHC/Admin/2009/3312
                    Group group = Match2("[" + fText2.Text);
                    if (group is not null) {
                        WText label = new WText(fText1.Text.Substring(0, fText1.Text.Length - 1), fText1.properties);
                        WNeutralCitation nc = new WNeutralCitation("[" + fText2.Text, fText2.properties);
                        IEnumerable<IInline> rest = line.Skip(2);
                        return rest.Prepend(nc).Prepend(label);
                    }
                }
                if (fText2.Text == ")") {   // EWHC/Ch/2011/3553
                    string text = fText1.Text + fText2.Text;
                    Group group = Match(text);
                    if (group is not null) {
                        List<IInline> replacement = Replace(text, group, fText1.properties);
                        IEnumerable<IInline> rest = line.Skip(1);
                        return Enumerable.Concat(replacement, rest);

                    }
                }
                if (fText1.Text == "[") {   // [2021] EWHC 2776 (QB)
                    string combined = fText1.Text + fText2.Text;
                    Group group = Match2(combined);
                    if (group is not null) {
                        List<IInline> replacement = Replace(combined, group, fText2.properties);
                        IEnumerable<IInline> rest = line.Skip(2);
                        return Enumerable.Concat(replacement, rest);

                    }
                }
                if (string.IsNullOrWhiteSpace(fText2.Text)) {
                    IInline third = line.Skip(2).FirstOrDefault();
                    if (third is WText fText3) {
                        ISet<string> prefixes = new HashSet<string>() { "Neutral Citation Number:", "NCN:" };
                        if (prefixes.Contains(fText1.Text)) {
                            Group group = Match2(fText3.Text);
                            if (group is not null) {
                                List<IInline> replacement = Replace(fText3.Text, group, fText3.properties);
                                return Concat3(line.Take(2), replacement, line.Skip(3));
                            }
                        }
                    }
                }
            }
            if ((first is WImageRef || first is WLineBreak) && second is WText wText) {
                Group group = Match(wText.Text);
                if (group is not null) {
                    List<IInline> replacement = Replace(wText, group);
                    return Concat3(first, replacement, line.Skip(2));
                }
            }
        }
        if (line.Count() == 3) {
            IInline first = line.First();
            IInline second = line.Skip(1).First();
            IInline third = line.Last();
            if (first is WText fText1 && second is WText fText2 && third is WText fText3) {
                string text = ILine.TextContent(line);
                Group group = Match(text);
                if (group is null)
                    group = Match2(text);
                if (group is not null)
                    return Replace(text, group, fText1.properties); // this won't preserve all run formatting
            }
        }
        return line;
    }

}

}
