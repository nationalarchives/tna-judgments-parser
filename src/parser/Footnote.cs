
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WFootnote : IFootnote {

    private readonly MainDocumentPart main;
    private readonly FootnoteReference fn;

    public WFootnote(MainDocumentPart main, FootnoteReference fn) {
        this.main = main;
        this.fn = fn;
    }

    public string Marker {
        get {
            int count = 1;
            FootnoteReference previous1 = fn.PreviousSibling<FootnoteReference>();
            while (previous1 != null) {
                count += 1;
                previous1 = previous1.PreviousSibling<FootnoteReference>();
            }
            OpenXmlElement parent = fn.Parent;
            while (parent is not null) {
                OpenXmlElement previous2 = parent.PreviousSibling();
                while (previous2 is not null) {
                    count += previous2.Descendants<FootnoteReference>().Count();
                    previous2 = previous2.PreviousSibling();
                }
                parent = parent.Parent;
            }
            return count.ToString();
        }
    }

    public IEnumerable<UK.Gov.Legislation.Judgments.ILine> Content {
        get {
            Footnote footnote = main.FootnotesPart.Footnotes.ChildElements.OfType<Footnote>()
                .Where(f => f.Id == fn.Id).First();
            return footnote.ChildElements.OfType<Paragraph>()
                .Select(p => new WLine(main, p));
        }
    }

}

}
