#nullable enable

using System.Collections.Generic;
using System.Linq;

using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.Legislation.Judgments.Parse;

internal abstract class NeutralCitationEnricherBase : Enricher2
{
    /// <param name="linePatternBeforeNcn">Anything that can go before "NCN number:"</param>
    /// <param name="citationShapePatterns">NCN Patterns for courts that this parser route handles</param>
    protected NeutralCitationEnricherBase(string linePatternBeforeNcn, string[] citationShapePatterns)
    {
        var unifiedCitationShapesPattern = string.Join('|', citationShapePatterns.Select(c => $"(?:{c})"));

        NeutralCitationPattern =
            $"(?i)(?:{linePatternBeforeNcn})(?:{PrefixNeutralCitationNumber}|{PrefixNcnNumber}|{PrefixNcn})?({unifiedCitationShapesPattern})";
    }

    /// <summary>
    /// How many of the leading blocks are searched for a citation.
    /// </summary>
    private const int BlockLimit = 10;

    /// <summary>
    /// A document has exactly one neutral citation, so enrichment stops once one has been marked up.
    /// Other changes to a block (e.g. a reference to another case) do not stop the search.
    /// </summary>
    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        List<IBlock> result = [];
        var foundNeutralCitation = false;
        foreach (var block in blocks)
        {
            if (foundNeutralCitation || result.Count >= BlockLimit)
            {
                result.Add(block);
                continue;
            }

            var enriched = Enrich(block);
            foundNeutralCitation = ContainsNeutralCitation(enriched);
            result.Add(enriched);
        }

        return result;
    }

    private static bool ContainsNeutralCitation(IBlock block)
    {
        IEnumerable<WLine> lines = block switch
        {
            WLine line => [line],
            WTable table => table.Lines,
            _ => []
        };

        return lines.SelectMany(line => line.Contents).Any(inline => inline is INeutralCitation or INeutralCitation2);
    }

    private const string Number = "(?:Number|Nunber|Numer|Num|No|figure)";
    protected const string CaseNumber = @"\[?\d+\]?";
    protected const string Year = @"\[\d{4}(?:\]|\[)?";

    private const string Ncn = @"N\s*C\s*N?";

    /// <summary>
    /// e.g. "Neutral Citation Number:", "Neutral Citation No."
    /// </summary>
    private const string PrefixNeutralCitationNumber =
        $@"(?:Neutral|Nuetral)\s*(?:Citation|Citaion)(?:\s*{Number})?\s*[:.]*\s*";

    /// <summary>
    /// e.g. "NCN no:", "NC figure"
    /// </summary>
    private const string PrefixNcnNumber = $@"{Ncn}\s*{Number}\s*[:.]*\s*";

    /// <summary>
    /// e.g. "ncn:", "NC."
    /// </summary>
    private const string PrefixNcn = $@"{Ncn}\s*[:.]*\s*";

    private string NeutralCitationPattern { get; }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        var lineArray = line.ToArray();

        return TryEnrichWithNeutralCitation(lineArray, out var enriched) ? enriched : line;
    }

    private bool TryEnrichWithNeutralCitation(IInline[] lineArray, out IInline[] enrichedLines)
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
