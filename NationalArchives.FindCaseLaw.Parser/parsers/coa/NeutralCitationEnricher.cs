#nullable enable

using System.Collections.Generic;
using System.Linq;

using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Judgments.Parse;

internal class NeutralCitationEnricher() : NeutralCitationEnricherBase(@"^\s*", CitationShapes)
{
    private static readonly string[] CitationShapes =
    [
        $@"{Year}\s*EWCA\s*\(?\s*(?:Civ|Crim)\s*\)?\s*{CaseNumber}",
        $@"{Year}\s*EWCA\s*{CaseNumber}\s*\(?\s*(?:Civ|Crim)\s*\)?",
        $@"{Year}\s*(?:EWHC|EWCH|EHWC)\s*{CaseNumber}\s*\(?\s*(?:{Courts.EwhcCodesPattern})\.?\s*\)?",
        $@"{Year}\s*EWCOP\s*{CaseNumber}(?:\s*\(T[1-3]\))?",
        $@"{Year}\s*EWFC\s*{CaseNumber}(?:\s*\(B\))?",
        $@"{Year}\s*EWCC\s*{CaseNumber}",
        $@"{Year}\s*EWCR\s*{CaseNumber}",
        $@"{Year}\s*EAT\s*{CaseNumber}",
        $@"{Year}\s*UKIPTrib\s*{CaseNumber}"
    ];

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
        var lineArray = line.ToArray();

        if (TryEnrichWithReferenceToAnotherCase(lineArray, out var linesEnrichedWithReferenceToAnotherCase))
        {
            return linesEnrichedWithReferenceToAnotherCase;
        }

        return base.Enrich(line);
    }

    private static bool TryEnrichWithReferenceToAnotherCase(IInline[] lineArray, out IEnumerable<IInline>
        enrichedLines)
    {
        enrichedLines = [];
        if (lineArray is [WText firstText, ..] && firstText.Text.Contains("linked"))
        {
            // [2023] EWFC 194 & 195
            enrichedLines = CaseLawRef.EnrichFromEnd(lineArray, @"(\[\d{4}\] EWFC \d+( \(B\))?)\.?$");
            return true;
        }

        return false;
    }
}
