using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Judgments.Parse;

internal class NetrualCitation : Enricher2
{
    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        return blocks.Take(10)
                     .Select(Enrich)
                     .Concat(blocks.Skip(10));
    }

    private static readonly string[] Patterns =
    [
        @"^ ?Neutral Citation( Number| No)?[:\.]? *(\[\d{4}\] EWCA (Civ|Crim) \d+)",
        @"^ *Neutral [Cc]itation( +[Nn]umber| No)? ?[:\.]? *(\[\d{4}\]? EWHC +\d+ +\((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\.?\))", // . in EWHC/Comm/2007/197
        @"^Neutral Citation( Number| No)?:? +(\[\d{4}\] EWHC \d+ (Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC))", // EWHC/Admin/2003/301
        @"^Neutral Citation( Number| No)?:? +(\[\d{4}\] EWCH \d+ \((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\))", // EWHC/Admin/2006/2373
        @"^Neutral Citation( Number| No)?:? +(\[\d{4}\] EHWC \d+ \((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\))", // [2022] EHWC 950 (Ch)
        @"^Neutral Citation( Number| No)?:? (\[\d{4}\] EWCOP \d+( \(T[1-3]\))?)",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWFC \d+( \(B\))?)",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWCA \d+ \((Civ|Crim)\))", // EWCA/Civ/2017/1798
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EWCA \d+ (Civ|Crim))",
        @"^Neutral Citation( Number)?:? +(\[\d{4}\] EWCC \d+)", @"^Neutral Citation( Number)?:? +(\[\d{4}\] EWCR \d+)",
        @"^Neutral Citation( Number)?:? (\[\d{4}\] EAT \d+)"
    ];

    private static readonly string[] Patterns2 =
    [
        @"^\s*(\[\d{4}\] EWCA (Civ|Crim) \d+)", // \s matches non-breaking space in [2022] EWCA Crim 733
        @"^ *(\[?\d{4}\] EWHC \d+ \((Admin|Admlty\.?|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\))", // period after Admlty in EWHC/Admlty/2003/320
        @"^\s(\[\d{4}\] EWHC \d+ \(Admin\))", // non-space in [2022] EWHC 307 (Admin)
        @"^(\[\d{4}\] EWHC \[\d+\] \((Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)\))$", // [2021] EWHC [3505] (IPEC)
        @"^Neutral Citation Nunber: (\[\d{4}\] EWCA (Civ|Crim) \d+)", // misspelling in EWCA/Civ/2006/1507
        @"^Neutral Citation Numer: (\[\d{4}\] EWHC \d+ \(Ch\))$", // misspelling in EWHC/Ch/2015/411
        @"^NCN:? (\[\d{4}\] EWCA (Civ|Crim) \d+)$", // [2021] EWCA Crim 1412
        @"^NCN No: (\[\d{4}\] EWCA (Civ|Crim) \d+)$", // [2022] EWCA Crim 39
        @"(\[\d{4}\] EWFC \d+( \(B\))?)",
        @"^Neutral Citation Number: (\[\d{4}\[ EWCA (Civ|Crim) \d+)", // [2018[ EWCA Civ 1744
        @"^(\[\d{4}\] EWCOP \d+( \(T[1-3]\))?)$", @"^(\[\d{4}\] EWCC \d+)", @"^(\[\d{4}\] EWCR \d+)",
        @"^ *(\[?\d{4}\]? EAT \d+)$", @"^Neutral Citation Number:? (\[\d{4}\] UKIPTrib \d+)"
    ];

    private static Group Match(string text)
    {
        foreach (var pattern in Patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[2];
            }
        }

        return null;
    }

    private static Group Match2(string text)
    {
        foreach (var pattern in Patterns2)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1];
            }
        }

        return null;
    }

    private static List<IInline> Replace(string text, Group group, RunProperties rProps)
    {
        var replacement = new List<IInline>(3);
        if (group.Index > 0)
        {
            var before1 = text.Substring(0, group.Index);
            var before2 = new WText(before1, rProps);
            replacement.Add(before2);
        }

        var during1 = group.Value;
        var during2 = new WNeutralCitation(during1, rProps);
        replacement.Add(during2);
        var after1 = text.Substring(group.Index + group.Length);
        if (!string.IsNullOrEmpty(after1))
        {
            var after2 = new WText(after1, rProps);
            replacement.Add(after2);
        }

        return replacement;
    }

    private static List<IInline> Replace(WText fText, Group group)
    {
        return Replace(fText.Text, group, fText.properties);
    }

    private IEnumerable<T> Concat3<T>(IEnumerable<T> one, IEnumerable<T> two, IEnumerable<T> three)
    {
        return one.Concat(two).Concat(three);
    }

    private IEnumerable<T> Concat3<T>(T one, IEnumerable<T> two, IEnumerable<T> three)
    {
        return two.Prepend(one).Concat(three);
    }

    protected override WLine Enrich(WLine line)
    {
        if (line.NormalizedContent.Contains("the draft judgment is only to be used to"))
        {
            return line;
        }

        return base.Enrich(line);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        if (line.Any())
        {
            var first = line.First();
            if (first is WText fText)
            {
                if (fText.Text.Contains("linked")) // [2023] EWFC 194 & 195
                {
                    return CaseLawRef.EnrichFromEnd(line, @"(\[\d{4}\] EWFC \d+( \(B\))?)\.?$");
                }

                var group = Match(fText.Text) ??
                            Match2(fText.Text);

                if (group is not null)
                {
                    var replacement = Replace(fText, group);
                    var rest = line.Skip(1);
                    return replacement.Concat(rest);
                }
            }

            var last = line.Last();
            if (last is WText fText2)
            {
                var group = Match(fText2.Text)
                            ?? Match2(fText2.Text);

                if (group is not null)
                {
                    if (first is WText fText1 && fText1.Text.Contains("linked")) // [2023] EWFC 169 & 170
                    {
                        return CaseLawRef.EnrichFromEnd(line, @"(\[\d{4}\] EWFC \d+( \(B\))?)\.?$");
                    }

                    var before = line.SkipLast(1);
                    var replacement = Replace(fText2, group);
                    return before.Concat(replacement);
                }
            }
        }

        if (line.Count() > 1)
        {
            var first = line.First();
            var second = line.Skip(1).First();
            if (first is WText fText1 && second is WText fText2)
            {
                if (fText1.Text.Trim() == "Neutral Citation Number:")
                {
                    var group = Match2(fText2.Text);
                    if (group is not null)
                    {
                        var replacement = Replace(fText2, group);
                        var rest = line.Skip(2);
                        return replacement.Concat(rest).Prepend(first);
                    }
                }

                if (fText1.Text == "Neutral Citation Number: [" || fText1.Text == "Neutral Citation Number:  [" ||
                    fText1.Text == "Neutral Citation No. [")
                {
                    // EWHC/Admin/2004/584, EWHC/Admin/2014/1564, EWHC/Ch/2009/1908
                    var group = Match2("[" + fText2.Text);
                    if (group is not null)
                    {
                        var label = new WText(fText1.Text.Substring(0, fText1.Text.Length - 1), fText1.properties);
                        var nc = new WNeutralCitation("[" + fText2.Text, fText2.properties);
                        var rest = line.Skip(2);
                        return rest.Prepend(nc).Prepend(label);
                    }
                }

                if (fText1.Text == "Neutral Citation Number" && fText2.Text.StartsWith(": "))
                {
                    // EWHC/Comm/2005/279
                    var group = Match2(fText2.Text.Substring(2));
                    if (group is not null)
                    {
                        var split = new WText(fText2.Text.Substring(0, 2), fText2.properties);
                        var nc = new WNeutralCitation(fText2.Text.Substring(2), fText2.properties);
                        var rest = line.Skip(2);
                        return new List<IInline>(3)
                        {
                            fText1,
                            split,
                            nc
                        }.Concat(rest);
                    }
                }

                if (fText1.Text == "Neutral Citation figure: [")
                {
                    // EWHC/Admin/2009/3312
                    var group = Match2("[" + fText2.Text);
                    if (group is not null)
                    {
                        var label = new WText(fText1.Text.Substring(0, fText1.Text.Length - 1), fText1.properties);
                        var nc = new WNeutralCitation("[" + fText2.Text, fText2.properties);
                        var rest = line.Skip(2);
                        return rest.Prepend(nc).Prepend(label);
                    }
                }

                if (fText2.Text == ")")
                {
                    // EWHC/Ch/2011/3553
                    var text = fText1.Text + fText2.Text;
                    var group = Match(text);
                    if (group is not null)
                    {
                        var replacement = Replace(text, group, fText1.properties);
                        var rest = line.Skip(1);
                        return replacement.Concat(rest);
                    }
                }

                if (fText1.Text == "[")
                {
                    // [2021] EWHC 2776 (QB)
                    var combined = fText1.Text + fText2.Text;
                    var group = Match2(combined);
                    if (group is not null)
                    {
                        var replacement = Replace(combined, group, fText2.properties);
                        var rest = line.Skip(2);
                        return replacement.Concat(rest);
                    }
                }

                if (string.IsNullOrWhiteSpace(fText2.Text))
                {
                    var third = line.Skip(2).FirstOrDefault();
                    if (third is WText fText3)
                    {
                        ISet<string> prefixes = new HashSet<string> { "Neutral Citation Number:", "NCN:" };
                        if (prefixes.Contains(fText1.Text))
                        {
                            var group = Match2(fText3.Text);
                            if (group is not null)
                            {
                                var replacement = Replace(fText3.Text, group, fText3.properties);
                                return Concat3(line.Take(2), replacement, line.Skip(3));
                            }
                        }
                    }
                }
            }

            if ((first is WImageRef || first is WLineBreak) && second is WText wText)
            {
                var group = Match(wText.Text);
                if (group is not null)
                {
                    var replacement = Replace(wText, group);
                    return Concat3(first, replacement, line.Skip(2));
                }
            }
        }

        if (line.Count() == 3)
        {
            var first = line.First();
            var second = line.Skip(1).First();
            var third = line.Last();
            if (first is WText fText1 && second is WText && third is WText)
            {
                var text = IInline.ToString(line);
                var group = Match(text)
                            ?? Match2(text);

                if (group is not null)
                {
                    return Replace(text, group, fText1.properties); // this won't preserve all run formatting
                }
            }
        }

        return line;
    }
}
