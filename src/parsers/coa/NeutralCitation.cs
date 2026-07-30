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
        @"^Neutral Citation( Number)?:? +(\[\d{4}\] EWCC \d+)",
        @"^Neutral Citation( Number)?:? +(\[\d{4}\] EWCR \d+)",
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
        @"^(\[\d{4}\] EWCOP \d+( \(T[1-3]\))?)$",
        @"^(\[\d{4}\] EWCC \d+)",
        @"^(\[\d{4}\] EWCR \d+)",
        @"^ *(\[?\d{4}\]? EAT \d+)$",
        @"^Neutral Citation Number:? (\[\d{4}\] UKIPTrib \d+)"
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
        var linesArray = line.ToArray(); //enumerate to an array so we don't waste resource recalculating lines
        var numberOfLines = linesArray.Length;

        if (numberOfLines == 0)
        {
            return linesArray;
        }

        var firstLineWText = linesArray[0] as WText;
        var firstLineProperties = firstLineWText?.properties;
        var normalisedFirstLineText = firstLineWText?.Text;

        var secondLineWText = (numberOfLines >= 2 ? linesArray[1] : null) as WText;
        var secondLineProperties = secondLineWText?.properties;
        var normalisedSecondLineText = secondLineWText?.Text;

        var thirdLineWText = (numberOfLines >= 3 ? linesArray[2] : null) as WText;
        var thirdLineProperties = thirdLineWText?.properties;
        var normalisedThirdLineText = thirdLineWText?.Text;

        var lastLineWText = linesArray[^1] as WText;
        var lastLineProperties = lastLineWText?.properties;
        var normalisedLastLineText = lastLineWText?.Text;

        switch (normalisedFirstLineText?.Trim(), normalisedSecondLineText?.Trim(), normalisedThirdLineText?.Trim())
        {
            case (not null, _, _) when normalisedFirstLineText!.Contains("linked"):
                {
                    // [2023] EWFC 194 & 195, [2023] EWFC 169 & 170
                    return CaseLawRef.EnrichFromEnd(linesArray, @"(\[\d{4}\] EWFC \d+( \(B\))?)\.?$");
                }
            case (not null, _, _) when (Match(normalisedFirstLineText) ?? Match2(normalisedFirstLineText)) is var group
                                       && group is not null:
                {
                    var replacement = Replace(normalisedFirstLineText, group, firstLineProperties);
                    return [.. replacement, .. linesArray.Skip(1)];
                }
            case (_, _, _) when numberOfLines == 1:
                {
                    return linesArray;
                }
            case (_, _, _) when normalisedLastLineText is not null
                                && (Match(normalisedLastLineText) ?? Match2(normalisedLastLineText)) is var group
                                && group is not null:
                {
                    var replacement = Replace(normalisedLastLineText, group, lastLineProperties);
                    return [.. linesArray.SkipLast(1), .. replacement];
                }
            case ("Neutral Citation Number:", not null, _) when Match2(normalisedSecondLineText) is var group
                                                                && group is not null:
                {
                    var replacement = Replace(normalisedSecondLineText, group, secondLineProperties);
                    return [linesArray[0], .. replacement, .. linesArray.Skip(2)];
                }
            case ("Neutral Citation Number: ["
                or "Neutral Citation Number:  ["
                or "Neutral Citation No. ["
                or "Neutral Citation figure: [", not null, _) when Match2("[" + normalisedSecondLineText) is not null:
                {
                    // EWHC/Admin/2004/584, EWHC/Admin/2014/1564, EWHC/Ch/2009/1908,  EWHC/Admin/2009/3312
                    var label = new WText(normalisedFirstLineText[..^1], firstLineProperties);
                    var nc = new WNeutralCitation("[" + normalisedSecondLineText, secondLineProperties);
                    return [label, nc, .. linesArray.Skip(2)];
                }
            case ("Neutral Citation Number", not null, _) when normalisedSecondLineText.StartsWith(": ")
                                                               && Match2(normalisedSecondLineText[2..]) is not null:
                {
                    // EWHC/Comm/2005/279
                    var split = new WText(normalisedSecondLineText.Substring(0, 2), secondLineProperties);
                    var nc = new WNeutralCitation(normalisedSecondLineText.Substring(2), secondLineProperties);
                    return [firstLineWText, split, nc, .. linesArray.Skip(2)];
                }
            case (not null, ")", _) when normalisedFirstLineText + normalisedSecondLineText is var combined
                                         && Match(combined) is var group
                                         && group is not null:
                {
                    // EWHC/Ch/2011/3553
                    var replacement = Replace(combined, group, firstLineProperties);
                    return [.. replacement, .. linesArray.Skip(1)];
                }
            case ("[", not null, _) when normalisedFirstLineText + normalisedSecondLineText is var combined
                                         && Match2(combined) is var group
                                         && group is not null:
                {
                    // [2021] EWHC 2776 (QB)
                    var replacement = Replace(combined, group, secondLineProperties);
                    return [.. replacement, .. linesArray.Skip(2)];
                }
            case ("Neutral Citation Number:" or "NCN:", _, not null) when Match2(normalisedThirdLineText) is var group
                                                                          && group is not null:
                {
                    var replacement = Replace(normalisedThirdLineText, group, thirdLineProperties);
                    return [.. linesArray.Take(2), .. replacement, .. linesArray.Skip(3)];
                }
            case (_, not null, _) when linesArray[0] is WImageRef or WLineBreak
                                       && Match(normalisedSecondLineText) is var group
                                       && group is not null:
                {
                    var replacement = Replace(normalisedSecondLineText, group, secondLineProperties);
                    return [linesArray[0], .. replacement, .. linesArray.Skip(2)];
                }
            case (not null, not null, not null) when IInline.ToString(linesArray) is var combined
                                                     && (Match(combined) ?? Match2(combined)) is var group
                                                     && group is not null:
                {
                    // this won't preserve all run formatting
                    return Replace(combined, group, firstLineProperties);
                }
            default:
                return linesArray;
        }
    }
}
