
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

        internal static IEnumerable<IInline> Enrich(IEnumerable<IInline> raw, string pattern)
        {
            return EnrichFromEnd.Enrich(raw, pattern, MakeRef);
        }

    }

}
