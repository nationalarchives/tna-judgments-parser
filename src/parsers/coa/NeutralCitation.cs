
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse {

class NetrualCitation : Enricher {

    private static readonly string[] patterns = {
        @"^Neutral Citation( Number| No)?[:\.]? (\[\d{4}\] EWCA (Civ|Crim) \d+)",
        @"^Neutral Citation( Number| No)?:? +(\[\d{4}\] EWHC \d+ \((Admin|Ch|Comm|Fam|Pat|QB|TCC)\))",
        @"^Neutral Citation( Number| No)?:? +(\[\d{4}\] EWCH \d+ \((Admin|Ch|Comm|Fam|Pat|QB|TCC)\))",   // EWHC/Admin/2006/2373
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWCOP \d+)",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWFC \d+)",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWCA \d+ (Civ|Crim))"
    };
    private static readonly string[] patterns2 = {
        @"^(\[\d{4}\] EWCA (Civ|Crim) \d+)",
        @"^(\[\d{4}\] EWHC \d+ \((Admin|Ch|Comm|Fam|Pat|QB|TCC)\))",
        @"^(\[\d{4}\] EWHC \d+)$"    // is this valid? EWHC/Admin/2004/584
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

    private static IInline[] Replace(WText fText, Group group) {
        string before = fText.Text.Substring(0, group.Index);
        string during = group.Value;
        string after = fText.Text.Substring(group.Index + group.Length);
        IInline[] replacement;
        if (string.IsNullOrEmpty(after)) {
            replacement = new IInline[] {
                new WText(before, fText.properties),
                new WNeutralCitation(during, fText.properties)
            };
        } else {
            replacement = new IInline[] {
                new WText(before, fText.properties),
                new WNeutralCitation(during, fText.properties),
                new WText(after, fText.properties)
            };
        }
        return replacement;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (line.Count() > 0) {
            IInline first = line.First();
            if (first is WText fText) {
                Group group = Match(fText.Text);
                if (group is not null) {
                    IInline[] replacement = Replace(fText, group);
                    IEnumerable<IInline> rest = line.Skip(1);
                    return Enumerable.Concat(replacement, rest);
                }
            }
        }
        if (line.Count() > 1) {
            IInline first = line.First();
            IInline second = line.Skip(1).First();
            if (first is WText fText1 && second is WText fText2) {
                if (fText1.Text == "Neutral Citation Number: ") {
                    Group group = Match2(fText2.Text);
                    if (group is not null) {
                        IInline[] replacement = Replace(fText2, group);
                        IEnumerable<IInline> rest = line.Skip(2);
                        return Enumerable.Concat(replacement, rest).Prepend(first);
                    }
                }
                if (fText1.Text == "Neutral Citation Number: [") {  // EWHC/Admin/2004/584
                    Group group = Match2("[" + fText2.Text);
                    if (group is not null) {
                        WText label = new WText(fText1.Text.Substring(0, fText1.Text.Length - 1), fText1.properties);
                        WNeutralCitation nc = new WNeutralCitation("[" + fText2.Text, fText1.properties);
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
            }
        }
        return line;
    }

}

}
