#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

internal partial class ProceedingsIdentifierEnricher : Enricher
{
    private readonly record struct InlineWithProceedingsIdentifiers(IInline Inline, Match[] ProceedingsIdentifiers)
    {
        public bool HasProceedingsIdentifiers => ProceedingsIdentifiers.Length > 0;
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        List<IBlock> enriched = [];
        var changed = false;
        string? previousType = null;

        foreach (var block in blocks)
        {
            if (block is not WLine line)
            {
                enriched.Add(block);
                continue;
            }

            var inlines = FindProceedingsIdentifiers(line.Contents);

            if (DoesNotContainProceedingsIdentifier(inlines))
            {
                enriched.Add(line);
                continue;
            }

            // A proceedings identifier with no label of its own takes the type of the one on a previous line
            previousType = GetLineType(inlines) ?? previousType;

            changed = true;
            enriched.Add(WLine.Make(line, EnrichLine(inlines, previousType)));
        }

        return changed ? enriched : blocks;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        var inlines = FindProceedingsIdentifiers(line);

        if (DoesNotContainProceedingsIdentifier(inlines))
        {
            return line;
        }

        return EnrichLine(inlines, GetLineType(inlines));
    }

    private static bool DoesNotContainProceedingsIdentifier(InlineWithProceedingsIdentifiers[] inlines)
    {
        return !inlines.Any(inline => inline.HasProceedingsIdentifiers);
    }

    private static InlineWithProceedingsIdentifiers[] FindProceedingsIdentifiers(IEnumerable<IInline> line)
    {
        return line.Select(inline =>
                       new InlineWithProceedingsIdentifiers(inline,
                           inline is WText wText ? GetProceedingsIdentifierMatches(wText.Text) : []))
                   .ToArray();
    }

    private static string? GetLineType(InlineWithProceedingsIdentifiers[] inlines)
    {
        var precedingText = IInline.ToString(inlines.TakeWhile(i => !i.HasProceedingsIdentifiers)
                                                    .Select(i => i.Inline));
        var (inline, proceedingsIdentifiers) = inlines.First(i => i.HasProceedingsIdentifiers);
        var text = IInline.GetText(inline);

        return GetProceedingsIdentifierType(precedingText + text[..proceedingsIdentifiers[0].Index]);
    }

    private static List<IInline> EnrichLine(InlineWithProceedingsIdentifiers[] inlines, string? type)
    {
        List<IInline> enriched = [];

        foreach (var inline in inlines)
        {
            if (inline is { Inline: WText wText, HasProceedingsIdentifiers: true })
            {
                enriched.AddRange(EnrichTextWithProceedingsIdentifiers(wText, type, inline.ProceedingsIdentifiers));
            }
            else
            {
                enriched.Add(inline.Inline);
            }
        }

        return enriched;
    }

    private static List<IInline> EnrichTextWithProceedingsIdentifiers(WText originalWText, string? type,
        Match[] proceedingsIdentifierMatches)
    {
        var textToSplit = originalWText.Text;
        List<IInline> enriched = [];
        var currentTextIndex = 0;

        foreach (var proceedingsIdentifierMatch in proceedingsIdentifierMatches)
        {
            // Is there any non-proceedings identifier text before the current match?
            if (proceedingsIdentifierMatch.Index > currentTextIndex)
            {
                enriched.Add(new WText(textToSplit[currentTextIndex..proceedingsIdentifierMatch.Index],
                    originalWText.properties));
            }

            // Add the matched proceedings identifier
            enriched.Add(new WProceedingsIdentifier(proceedingsIdentifierMatch.Value, type, originalWText.properties));

            currentTextIndex = proceedingsIdentifierMatch.Index + proceedingsIdentifierMatch.Length;
        }

        // Is there any trailing text after the last match?
        if (currentTextIndex < textToSplit.Length)
        {
            enriched.Add(new WText(textToSplit[currentTextIndex..], originalWText.properties));
        }

        return enriched;
    }

    private static string? GetProceedingsIdentifierType(string textBeforeIdentifier)
    {
        var type = TypeLabelRegex().Match(textBeforeIdentifier).Groups["type"].Value;

        return type.Length == 0 ? null : type;
    }

    private static Match[] GetProceedingsIdentifierMatches(string text)
    {
        Regex[] proceedingsIdentifierRegexes =
        [
            SlashGroupRegex(),
            DashGroupRegex(),
            DigitDottedRegex(),
            OldCareStandardsRegex(),
            UkfttTaxChamberCaseNumberRegex(),
            LandRegistrationReferenceRegex()
        ];

        return proceedingsIdentifierRegexes.SelectMany(r => r.Matches(text))
                                           .Where(m => m.Value.Any(char.IsDigit))
                                           .OrderBy(m => m.Index).ToArray();
    }

    /// <summary>
    /// A letter-prefixed appeal/case/reference number made up of two or more "/" separated
    /// groups, e.g. "CIS/111/2021", "FT/EA/2025/0478/GDPR", "LON/00BK/LSC/2023/0354" or "REF/2024/0045 & 0046"
    /// </summary>
    [GeneratedRegex(@"\b[A-Z]{2,4}(?:/[A-Z0-9]{1,6}){1,4}(?: & \d{2,6})*\b", RegexOptions.IgnoreCase)]
    private static partial Regex SlashGroupRegex();

    /// <summary>
    /// A letter-prefixed appeal/case/reference number made up of two or more "-" separated
    /// groups, e.g. "EA-2022-001495-NK", "UA-2026-000079-PIP" or "LC-2025-372"
    /// </summary>
    [GeneratedRegex(@"\b[A-Z]{2,4}(?:-[A-Z0-9]{1,6}){1,4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex DashGroupRegex();

    /// <summary>
    /// A digit-prefixed, dot-separated reference number, e.g. "2026-01951.EA" or "2025-01627.ISO-W".
    /// </summary>
    [GeneratedRegex(@"\b\d{4}-\d{4,6}\.[A-Z]{2,4}(?:-[A-Z]{1,4})?\b", RegexOptions.IgnoreCase)]
    private static partial Regex DigitDottedRegex();

    /// <summary>
    /// An older, bracketed-year reference number from Care Standards e.g. "[2010]1808.SW", "[2009] 1658 PT" or "[2002] 7.PC"
    /// </summary>
    [GeneratedRegex(@"\[\d{4}\]\s*\d+[.\s]+[-A-Z]+(?=\s*$)", RegexOptions.IgnoreCase)]
    private static partial Regex OldCareStandardsRegex();

    /// <summary>
    /// A space-separated FTT Tax Chamber case number, e.g. "TC 10013".
    /// </summary>
    [GeneratedRegex(@"\bTC\s*\d{4,6}\b", RegexOptions.IgnoreCase)]
    private static partial Regex UkfttTaxChamberCaseNumberRegex();

    /// <summary>
    /// An FTT Property Chamber Land Registration reference with a space after the prefix, e.g. "REF 2017/0591".
    /// </summary>
    [GeneratedRegex(@"\bREF\s+\d{4}/\d{2,6}\b", RegexOptions.IgnoreCase)]
    private static partial Regex LandRegistrationReferenceRegex();

    /// <summary>
    /// What separates the label from earlier text: a tab, a gap of whitespace, e.g. after
    /// "Neutral citation number: [2022] UKUT 121 (LC)    ", or a closing bracket, e.g. after "(Immigration and Asylum Chamber) "
    /// </summary>
    private const string LabelSeparator = @"\t|\s{2,}|\)\s*";

    /// <summary>
    /// Any earlier text, up to and including the last separator before the label
    /// </summary>
    private const string TextBeforeLabel = $@"(?:.*(?:{LabelSeparator})(?=\S))?";

    /// <summary>
    /// The plural "s" on "Nos", "Numbers" or "references"
    /// </summary>
    private const string PluralSuffix = @"(?:(?<=\b(?:No|number|reference))s)?";

    private const string TrailingPunctuation = @"[\s.:]*";

    /// <summary>
    /// The label naming the type of proceedings identifier that follows it, captured as "type", e.g. "UTLC No" in
    /// "Neutral citation number: [2022] UKUT 121 (LC)    UTLC No: "
    /// </summary>
    [GeneratedRegex($"^{TextBeforeLabel}(?<type>.*?){PluralSuffix}{TrailingPunctuation}$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TypeLabelRegex();
}
