
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class PressSummaryParser {

    internal PressSummary Parse(WordprocessingDocument doc) {
        WordDocument preParsed = new PreParser().Parse(doc);
        return Parse(doc, preParsed);
    }

    internal PressSummary Parse(WordprocessingDocument doc, WordDocument preParsed) {
        var contents = Enumerable.Concat( preParsed.Header, preParsed.Body.Select(bb => bb.Block) );
        contents = PressSummaryEnricher.Enrich(contents);
        var metadata = new PSMetadata(doc.MainDocumentPart, contents);
        var images = WImage.Get(doc);
        return new PressSummary { Metadata = metadata, Body = contents, Images = images };
    }

}

}
