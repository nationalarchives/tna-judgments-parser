
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WFootnote : IFootnote {

    private readonly MainDocumentPart main;
    private readonly FootnoteEndnoteReferenceType fn;

    public WFootnote(MainDocumentPart main, FootnoteEndnoteReferenceType fn) {
        this.main = main;
        this.fn = fn;
    }

    public string Marker {
        get {
            int count = 1;
            FootnoteEndnoteReferenceType previous1 = fn.PreviousSibling<FootnoteEndnoteReferenceType>();
            while (previous1 != null) {
                count += 1;
                previous1 = previous1.PreviousSibling<FootnoteEndnoteReferenceType>();
            }
            OpenXmlElement parent = fn.Parent;
            while (parent is not null) {
                OpenXmlElement previous2 = parent.PreviousSibling();
                while (previous2 is not null) {
                    count += previous2.Descendants<FootnoteEndnoteReferenceType>().Count();
                    previous2 = previous2.PreviousSibling();
                }
                parent = parent.Parent;
            }
            return count.ToString();
        }
    }

    public IEnumerable<UK.Gov.Legislation.Judgments.IBlock> Content {
        get {
            IEnumerable<FootnoteEndnoteType> notes;
            if (fn is FootnoteReference)
                notes = main.FootnotesPart.Footnotes.ChildElements.OfType<Footnote>();
            else
                notes = main.EndnotesPart.Endnotes.ChildElements.OfType<Endnote>();
            FootnoteEndnoteType note = notes.Where(f => f.Id == fn.Id).First();
            return Blocks.ParseBlocks(main, note.ChildElements);
        }
    }

}

}
