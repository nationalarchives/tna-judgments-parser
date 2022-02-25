
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using AttachmentPair = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;

namespace UK.Gov.Legislation.Judgments.Parse {

class EWPDF : CourtOfAppealParser {

    private static ILogger logger = Logging.Factory.CreateLogger<EWPDF>();

    new public static Judgment Parse(WordprocessingDocument doc) {
        return new EWPDF(doc).Parse();
    }
    new public static Judgment Parse2(WordprocessingDocument doc, IOutsideMetadata meta) {
        return new EWPDF(doc, meta).Parse();
    }
    new public static Judgment Parse3(WordprocessingDocument doc, IOutsideMetadata meta, IEnumerable<AttachmentPair> attachments) {
        return new EWPDF(doc, meta, attachments).Parse();
    }

    private EWPDF(WordprocessingDocument doc, IOutsideMetadata meta = null, IEnumerable<AttachmentPair> attachments = null) : base(doc, meta, attachments) { }

    private List<Enricher> headerEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger(),
        new NetrualCitation(),
        new CaseNo(),
        new CourtTypePDF(),
        new DocDatePDF(),
        new PartyEnricher(),
        new Judge(),
        new LawyerEnricher()
    };

    protected override IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        return Enricher.Enrich(header, headerEnrichers);
    }

}

}
