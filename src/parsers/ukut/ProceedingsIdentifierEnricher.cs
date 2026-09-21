#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

internal partial class ProceedingsIdentifierEnricher : Enricher
{
    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        var enriched = new List<IInline>();
        var enrichedSuccessfully = false;

        foreach (var inline in line.ToArray())
        {
            if (inline is WText wTextToEnrich
                && TryEnrichTextContainingProceedingsIdentifier(wTextToEnrich, out var enrichedText))
            {
                enrichedSuccessfully = true;
                enriched.AddRange(enrichedText);
            }
            else
            {
                enriched.Add(inline);
            }
        }

        return enrichedSuccessfully ? enriched : line;
    }

    private static bool TryEnrichTextContainingProceedingsIdentifier(WText originalWText, out List<IInline> enriched)
    {
        var proceedingsIdentifierMatches = GetProceedingsIdentifierMatches(originalWText.Text);

        if (proceedingsIdentifierMatches.Length == 0)
        {
            enriched = [];
            return false;
        }

        enriched = EnrichTextWithProceedingsIdentifiers(originalWText, proceedingsIdentifierMatches);

        return true;
    }

    private static List<IInline> EnrichTextWithProceedingsIdentifiers(WText originalWText,
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

            // add the matched proceedings identifier            
            enriched.Add(new WProceedingsIdentifier(proceedingsIdentifierMatch.Value, originalWText.properties));

            currentTextIndex = proceedingsIdentifierMatch.Index + proceedingsIdentifierMatch.Length;
        }

        // Is there any trailing text after the last match?
        if (currentTextIndex < textToSplit.Length)
        {
            enriched.Add(new WText(textToSplit[currentTextIndex..], originalWText.properties));
        }

        return enriched;
    }

    private static Match[] GetProceedingsIdentifierMatches(string text)
    {
        Regex[] proceedingsIdentifierRegexes =
        [
            SlashGroupRegex(),
            DashGroupRegex(),
            DigitDottedRegex(),
            OldCareStandardsRegex(),
            UkfttTaxChamberCaseNumberRegex()
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
    [GeneratedRegex(@"\[\d{4}\]\s*\d+[.\s]+[-A-Z]+$", RegexOptions.IgnoreCase)]
    private static partial Regex OldCareStandardsRegex();

    /// <summary>
    /// A space-separated FTT Tax Chamber case number, e.g. "TC 10013".
    /// </summary>
    [GeneratedRegex(@"\bTC\s*\d{4,6}\b", RegexOptions.IgnoreCase)]
    private static partial Regex UkfttTaxChamberCaseNumberRegex();
}
