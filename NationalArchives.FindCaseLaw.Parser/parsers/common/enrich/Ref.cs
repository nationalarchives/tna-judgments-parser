
using System.Collections.Generic;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.Enrichment
{

    class CaseLawRef
    {

        static WRef MakeRef(string ncn, RunProperties rProps)
        {
            var normalized = Citations.Normalize(ncn);
            var prefix = "https://caselaw.nationalarchives.gov.uk/";
            var url = prefix + Citations.MakeUriComponent(normalized);
            return new WRef(ncn, rProps)
            {
                Href = url,
                Canonical = normalized,
                IsNeutral = true,
                Type = RefType.Case
            };
        }

        // internal static WLine EnrichFromEnd(WLine raw, string[] patterns)
        // {
        //     return Enrichment.EnrichFromEnd.Enrich(raw, patterns, MakeRef);
        // }

        internal static WLine EnrichFromEnd(WLine raw, string pattern)
        {
            return Enrichment.EnrichFromEnd.Enrich(raw, pattern, MakeRef);
        }

        internal static IEnumerable<IInline> EnrichFromEnd(IEnumerable<IInline> raw, string pattern)
        {
            return Enrichment.EnrichFromEnd.Enrich(raw, pattern, MakeRef);
        }

    }

}
