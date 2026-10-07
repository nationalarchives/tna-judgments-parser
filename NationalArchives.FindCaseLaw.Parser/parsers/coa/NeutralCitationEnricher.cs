#nullable enable

using System.Collections.Generic;
using System.Linq;

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

    private const string Ncn = @"N\s*C\s*N?";
    private const string Number = "(?:Number|Nunber|Numer|Num|No|figure)";
    private const string CaseNumber = @"\[?\d+\]?";
    private const string Year = @"\[\d{4}(?:\]|\[)?";

    private const string Ewhc = "(?:EWHC|EWCH|EHWC)";

    /// <summary>
    /// e.g. "Neutral Citation Number:", "Neutral Citation No."
    /// </summary>
    private const string PrefixNeutralCitationNumber = $@"(?:Neutral|Nuetral)\s*(?:Citation|Citaion)(?:\s*{Number})?\s*[:.]*\s*";

    /// <summary>
    /// e.g. "NCN no:", "NC figure"
    /// </summary>
    private const string PrefixNcnNumber = $@"{Ncn}\s*{Number}\s*[:.]*\s*";

    /// <summary>
    /// e.g. "ncn:", "NC."
    /// </summary>
    private const string PrefixNcn = $@"{Ncn}\s*[:.]*\s*";

    /// <summary>
    /// e.g. "[2017] EWCA Civ 1798"
    /// </summary>
    private const string NcnEwcaDivisionBeforeNumber = $@"{Year}\s*EWCA\s*\(?\s*(?:Civ|Crim)\s*\)?\s*{CaseNumber}";

    /// <summary>
    /// e.g. "[2017] EWCA 1798 (Civ)"
    /// </summary>
    private const string NcnEwcaDivisionAfterNumber = $@"{Year}\s*EWCA\s*{CaseNumber}\s*\(?\s*(?:Civ|Crim)\s*\)?";

    /// <summary>
    /// e.g. "[2007] EWHC 197 (Comm)"
    /// </summary>
    private const string NcnEwhc = $@"{Year}\s*{Ewhc}\s*{CaseNumber}\s*\(?\s*(?:{Courts.EwhcCodesPattern})\.?\s*\)?";

    /// <summary>
    /// e.g. "[2020] EWCOP 12 (T1)"
    /// </summary>
    private const string NcnEwcop = $@"{Year}\s*EWCOP\s*{CaseNumber}(?:\s*\(T[1-3]\))?";

    /// <summary>
    /// e.g. "[2026] EWFC 194 (B)"
    /// </summary>
    private const string NcnEwfc = $@"{Year}\s*EWFC\s*{CaseNumber}(?:\s*\(B\))?";

    /// <summary>
    /// e.g. "[2020] EWCC 12"
    /// </summary>
    private const string NcnEwcc = $@"{Year}\s*EWCC\s*{CaseNumber}";

    /// <summary>
    /// e.g. "[2020] EWCR 12"
    /// </summary>
    private const string NcnEwcr = $@"{Year}\s*EWCR\s*{CaseNumber}";

    /// <summary>
    /// e.g. "[2020] EAT 12"
    /// </summary>
    private const string NcnEat = $@"{Year}\s*EAT\s*{CaseNumber}"; //todo: check on EAT and UKIPTrib - do they belong here?

    /// <summary>
    /// e.g. "[2020] UKIPTrib 12"
    /// </summary>
    private const string NcnUkiptrib = $@"{Year}\s*UKIPTrib\s*{CaseNumber}";

    /// <summary>
    /// Matches an optional recognised label (e.g. "Neutral Citation Number:") followed by one of
    /// the recognised neutral citation shapes, which is captured as group 1. Anchored to the start
    /// of the line (after any leading whitespace) so it only matches a citation that leads the line.
    /// </summary>
    private const string NeutralCitationPattern =
        $@"(?i)^\s*(?:{PrefixNeutralCitationNumber}|{PrefixNcnNumber}|{PrefixNcn})?" +
        $@"({NcnEwcaDivisionBeforeNumber}|{NcnEwcaDivisionAfterNumber}|{NcnEwhc}|{NcnEwcop}|{NcnEwfc}|{NcnEwcc}|{NcnEwcr}|{NcnEat}|{NcnUkiptrib})";

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

        if (TryEnrichWithNeutralCitation(lineArray, out var linesEnrichedWithNcn))
        {
            return linesEnrichedWithNcn;
        }

        return line;
    }

    private static bool TryEnrichWithReferenceToAnotherCase(IInline[] lineArray, out IEnumerable<IInline> enrichedLines)
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

    private static bool TryEnrichWithNeutralCitation(IInline[] lineArray, out IInline[] enrichedLines)
    {
        var enriched = EnrichFromBeginning.Enrich(lineArray, NeutralCitationPattern, MakeNeutralCitation);
        if (ReferenceEquals(enriched, lineArray))
        {
            enrichedLines = [];
            return false;
        }

        enrichedLines = enriched.ToArray();
        return true;
    }

    /// <summary>
    /// Wraps the matched citation text in a WNeutralCitation. Where the match is assembled from
    /// more than one run, the new node's formatting comes from whichever run the citation starts in.
    /// </summary>
    private static IInline MakeNeutralCitation(IList<IInline> inside)
    {
        var text = IInline.ToString(inside);
        var properties = inside.OfType<WText>().FirstOrDefault()?.properties;
        return new WNeutralCitation(text, properties!);
    }
}
