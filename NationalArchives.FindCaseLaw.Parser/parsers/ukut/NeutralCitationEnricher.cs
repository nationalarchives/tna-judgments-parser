using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

internal class NeutralCitationEnricher() : NeutralCitationEnricherBase(".*", CitationShapes)
{
    private static readonly string[] CitationShapes =
    [
        $@"{Year}\s*UKUT\s*{CaseNumber}\s*\(\s*(?:{Courts.UpperTribunalChamberCodesPattern})\s*\)",
        $@"{Year}\s*UKFTT\s*{CaseNumber}\s*\(\s*(?:{Courts.FirstTierTribunalChamberCodesPattern})\s*\)",
        $@"{Year}\s*UKAIT\s*{CaseNumber}"
    ];
}
