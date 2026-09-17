using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Judgments.Parse;

internal class NeutralCitationEnricher : Enricher2
{
    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        return blocks.Take(10)
                     .Select(Enrich)
                     .Concat(blocks.Skip(10));
    }

    private static readonly string NeutralCitationNumberPrefixRegexPattern =
        $@"Neutral\sCitation(?:\s+{NumberSpellingsRegexPattern})?\s*[:\.]?\s*";

    private static readonly string NumberSpellingsRegexPattern = "(?:Number|No|Numer|Nunber)";

    private static readonly string
        EwhcCourtsRegexPattern = "(?:Admin|Admlty|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC)";

    // Use \s instead of " " to match non-breaking spaces
    private static readonly string[] PrefixPatterns =
    [
        $@"NCN(?:\s+{NumberSpellingsRegexPattern})?\s*[:\.]?\s*",
        NeutralCitationNumberPrefixRegexPattern,
        @"Neutral\sCitation(?:\s+figure)?\s*[:\.]?\s*"
    ];

    // Use \s instead of " " to match non-breaking spaces
    private static readonly string[] NcnPatterns =
    [
        $@"(\[\d{{4}}\]? EWHC +\d+ +\({EwhcCourtsRegexPattern}\.?\))", // . in EWHC/Comm/2007/197
        $@"(\[\d{{4}}\] EWHC \d+ {EwhcCourtsRegexPattern})", // EWHC/Admin/2003/301
        $@"(\[\d{{4}}\] EWCH \d+ \({EwhcCourtsRegexPattern}\))", // EWHC/Admin/2006/2373
        $@"(\[\d{{4}}\] EHWC \d+ \({EwhcCourtsRegexPattern}\))", // [2022] EHWC 950 (Ch)
        $@"(\[?\d{{4}}\] EWHC \d+ \({EwhcCourtsRegexPattern}\.?\))", // period after Admlty in EWHC/Admlty/2003/320
        $@"(\[\d{{4}}\] EWHC \[\d+\] \({EwhcCourtsRegexPattern}\))$", // [2021] EWHC [3505] (IPEC)
        @"(\[\d{4}\] EWCOP \d+( \(T[1-3]\))?)",
        @"(\[\d{4}\] EWFC \d+( \(B\))?)",
        @"(\[\d{4}\] EWCC \d+)",
        @"(\[\d{4}\] EWCR \d+)",
        @"(\[\d{4}\] EAT \d+)",
        @"(\[\d{4}\]\sEWCA\s\d+\s\(?(?:Civ|Crim)\)?)", // EWCA/Civ/2017/1798
        @"(\[\d{4}\]\sEWCA\s(Civ|Crim)\s\d+)", // \s matches non-breaking space in [2022] EWCA Crim 733
        @"(\[\d{4}\] EWHC \d+ \(Admin\))", // non-space in [2022] EWHC 307 (Admin)
        @"(\[\d{4}\] EWFC \d+( \(B\))?)",
        @"(\[\d{4}\] EWCOP \d+( \(T[1-3]\))?)$",
        @"(\[\d{4}\] EWCC \d+)",
        @"(\[\d{4}\] UKIPTrib \d+)"
    ];

    private static readonly string[] Patterns =
    [
        $@"{NeutralCitationNumberPrefixRegexPattern}(\[\d{{4}}\]? EWHC +\d+ +\({EwhcCourtsRegexPattern}\.?\))", // . in EWHC/Comm/2007/197
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? +(\[\d{{4}}\] EWHC \d+ {EwhcCourtsRegexPattern})", // EWHC/Admin/2003/301
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? +(\[\d{{4}}\] EWCH \d+ \({EwhcCourtsRegexPattern}\))", // EWHC/Admin/2006/2373
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? +(\[\d{{4}}\] EHWC \d+ \({EwhcCourtsRegexPattern}\))", // [2022] EHWC 950 (Ch)
        $@" *(\[?\d{{4}}\] EWHC \d+ \({EwhcCourtsRegexPattern}\.?\))", // period after Admlty in EWHC/Admlty/2003/320
        $@"(\[\d{{4}}\] EWHC \[\d+\] \({EwhcCourtsRegexPattern}\))$", // [2021] EWHC [3505] (IPEC)
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? (\[\d{{4}}\] EWCOP \d+( \(T[1-3]\))?)",
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? (\[\d{{4}}\] EWFC \d+( \(B\))?)",
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? +(\[\d{{4}}\] EWCC \d+)",
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? +(\[\d{{4}}\] EWCR \d+)",
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? (\[\d{{4}}\] EAT \d+)",
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?[:\.]? *(\[\d{{4}}\] EWCA (Civ|Crim) \d+)",
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? (\[\d{{4}}\] EWCA \d+ \((Civ|Crim)\))", // EWCA/Civ/2017/1798
        $@"Neutral Citation(?: {NumberSpellingsRegexPattern})?:? (\[\d{{4}}\] EWCA \d+ (Civ|Crim))",
        @"\s*(\[\d{4}\] EWCA (Civ|Crim) \d+)", // \s matches non-breaking space in [2022] EWCA Crim 733
        @"\s(\[\d{4}\] EWHC \d+ \(Admin\))", // non-space in [2022] EWHC 307 (Admin)
        $@"Neutral Citation {NumberSpellingsRegexPattern}: (\[\d{{4}}\] EWCA (Civ|Crim) \d+)", // misspelling in EWCA/Civ/2006/1507
        @"NCN:?\s+(\[\d{4}\] EWCA (Civ|Crim) \d+)$", // [2021] EWCA Crim 1412
        $@"NCN {NumberSpellingsRegexPattern}: (\[\d{{4}}\] EWCA (Civ|Crim) \d+)$", // [2022] EWCA Crim 39
        $@"Neutral Citation {NumberSpellingsRegexPattern}: (\[\d{{4}}\[ EWCA (Civ|Crim) \d+)", // [2018[ EWCA Civ 1744
        @"(\[\d{4}\] EWFC \d+( \(B\))?)",
        @"(\[\d{4}\] EWCOP \d+( \(T[1-3]\))?)$",
        @"(\[\d{4}\] EWCC \d+)",
        @"(\[\d{4}\] EWCR \d+)",
        @" *(\[?\d{4}\]? EAT \d+)$",
        $@"Neutral Citation {NumberSpellingsRegexPattern}:? (\[\d{{4}}\] UKIPTrib \d+)"
    ];


    private static bool TryMatchNcn(WText wText, out Group group)
    {
        return TryMatchNcn(wText.Text, out group);
    }

    private static bool TryMatchNcn(string text, out Group group)
    {
        foreach (var pattern in Patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                group = match.Groups[1];
                return true;
            }
        }

        group = null;
        return false;
    }

    private static List<IInline> Replace(WText wText, Group neutralCitationRegexGroup)
    {
        return Replace(wText.Text, neutralCitationRegexGroup, wText.properties);
    }

    private static List<IInline> Replace(string text, Group neutralCitationRegexGroup, RunProperties rProps)
    {
        var replacement = new List<IInline>(3);
        if (neutralCitationRegexGroup.Index > 0)
        {
            replacement.Add(new WText(text[..neutralCitationRegexGroup.Index], rProps));
        }

        replacement.Add(new WNeutralCitation(neutralCitationRegexGroup.Value, rProps));

        var indexAfterGroupMatch = neutralCitationRegexGroup.Index + neutralCitationRegexGroup.Length;
        if (text.Length > indexAfterGroupMatch)
        {
            replacement.Add(new WText(text[indexAfterGroupMatch..], rProps));
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
        return line.ToArray() switch
        {
            [WText first, ..] when first.Text.Contains("linked") =>
                // [2023] EWFC 194 & 195, [2023] EWFC 169 & 170
                CaseLawRef.EnrichFromEnd(line, @"(\[\d{4}\] EWFC \d+( \(B\))?)\.?$"),

            [WText first, .. var rest] when TryMatchNcn(first, out var group)
                => [.. Replace(first, group), .. rest],

            [.. var before, WText last] when TryMatchNcn(last, out var group)
                => [.. before, .. Replace(last, group)],

            [WText { Text: "Neutral Citation Number:" } first, WText second, .. var rest]
                when TryMatchNcn(second, out var group)
                => [first, .. Replace(second, group), .. rest],

            [WText first, WText second, .. var rest]
                when (first.Text?.Trim() == "NCN" || first.Text?.Trim() == "NCN:")
                && TryMatchNcn(second, out var group)
                => [first, .. Replace(second, group), .. rest],

            [
                    WText
            {
                Text: "Neutral Citation Number: [" or "Neutral Citation Number:  ["
                        or "Neutral Citation No. [" or "Neutral Citation figure: ["
            } first,
                    WText second, .. var rest
                ] when TryMatchNcn("[" + second.Text, out _) =>
                // EWHC/Admin/2004/584, EWHC/Admin/2014/1564, EWHC/Ch/2009/1908,  EWHC/Admin/2009/3312
                [
                    new WText(first.Text[..^1], first.properties),
                    new WNeutralCitation("[" + second.Text, second.properties), .. rest
                ],

            [WText { Text: "Neutral Citation Number" } first, WText second, .. var rest] when
                second.Text.StartsWith(": ") && TryMatchNcn(second.Text[2..], out _) =>
                // EWHC/Comm/2005/279
                [
                    first, new WText(second.Text[..2], second.properties),
                    new WNeutralCitation(second.Text[2..], second.properties), .. rest
                ],

            [WText first, WText { Text: ")" } second, .. var rest]
                when first.Text + second.Text is var combined && TryMatchNcn(combined, out var group)
                =>
                // EWHC/Ch/2011/3553
                [.. Replace(combined, group, first.properties), .. rest],

            [WText { Text: "[" } first, WText second, .. var rest]
                when first.Text + second.Text is var combined
                && TryMatchNcn(combined, out var group)
                =>
                // [2021] EWHC 2776 (QB)
                [.. Replace(combined, group, second.properties), .. rest],

            [WText { Text: "Neutral Citation Number:" or "NCN:" } first, WText second, WText third, .. var rest]
                when
                Regex.IsMatch(first.Text, NeutralCitationNumberPrefixRegexPattern)
                && TryMatchNcn(third, out var group)
                => [first, second, .. Replace(third, group), .. rest],

            [{ } first and (WImageRef or WLineBreak), WText second, .. var rest]
                when TryMatchNcn(second, out var group)
                => [first, .. Replace(second, group), .. rest],

            [WText first, ..] when IInline.ToString(line) is var combined
                && TryMatchNcn(combined, out var group) =>
                // this won't preserve all run formatting
                Replace(combined, group, first.properties),

            _ => line
        };
    }
}
