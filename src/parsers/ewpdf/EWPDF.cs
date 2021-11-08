using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse {

class EWPDF : CourtOfAppealParser {

    private static ILogger logger = Logging.Factory.CreateLogger<EWPDF>();

    new public static Judgment Parse(WordprocessingDocument doc) {
        return new EWPDF(doc).Parse();
    }

    private EWPDF(WordprocessingDocument doc) : base(doc) { }

    private List<Enricher> headerEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger(),
        new NetrualCitation(),
        new CaseNo(),
        new CourtTypePDF(),
        new DocDate(),
        new PartyEnricher(),
        new Judge(),
        new LawyerEnricher()
    };

    protected override IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        return Enricher.Enrich(header, headerEnrichers);
    }

}

}
