
using System;
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Enrichment;

namespace UK.Gov.NationalArchives.CaseLaw.UKSC
{

    class OnAppealFromRefEnricher : Enricher
    {

        private static readonly string Pattern = @"On appeal from: (\[\d{4}\] EWCA (Civ|Crim) \d+)$";

        private bool found = false;

        override protected WLine Enrich(WLine line)
        {
            if (found)
                return line;
            if (!line.NormalizedContent.Contains("On appeal from:"))
                return line;
            var enriched = CaseLawRef.EnrichFromEnd(line, Pattern);
            found = !ReferenceEquals(enriched, line);
            return enriched;
        }

        protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
        {
            throw new NotImplementedException();
        }
    }

}
