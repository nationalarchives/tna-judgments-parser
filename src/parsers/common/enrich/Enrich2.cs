

using System;
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse {

class Enrich2 {

    static IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks, IEnumerable<Enricher> enrichers) {
        return enrichers.Aggregate(blocks, (done, enricher) => enricher.Enrich(done));
    }

}

}
