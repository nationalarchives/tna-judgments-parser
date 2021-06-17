
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

internal class Blocks {

    internal static IEnumerable<IBlock> ParseBlocks(MainDocumentPart main, IEnumerable<OpenXmlElement> elements) {
        return elements
        .Where(e => !(e is SectionProperties))
        .Where(e => !(e is BookmarkStart))
        .Where(e => !(e is BookmarkEnd))
        .Select<OpenXmlElement, IBlock>(e => {
            if (e is Paragraph para)
                return Parse1(main, para);
            if (e is Table table)
                return new WTable(main, table);
            throw new System.Exception(e.GetType().ToString());
        });
    }

    internal static IBlock Parse1(MainDocumentPart main, Paragraph paragraph) {
        string number = DOCX.Numbering.GetFormattedNumber(main, paragraph)?.Text;
        if (number is null)
            return new WLine(main, paragraph);
        return new WOldNumberedParagraph(number, main, paragraph);
    }


}

class WLine : ILine {

    private readonly MainDocumentPart main;
    private readonly ParagraphProperties properties;
    private readonly IEnumerable<IInline> contents;

    public WLine(MainDocumentPart main, ParagraphProperties properties, IEnumerable<IInline> contents) {
        this.main = main;
        this.properties = properties;
        this.contents = contents;
    }
    public WLine(MainDocumentPart main, Paragraph paragraph) {
        this.main = main;
        this.properties = paragraph.ParagraphProperties;
        this.contents = Inline.ParseRuns(main, paragraph.ChildElements);
    }
    public WLine(WLine prototype, IEnumerable<IInline> contents) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = contents;
    }
    internal WLine(WLine prototype) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = prototype.contents;
    }

    public string Style {
        get => properties?.ParagraphStyleId?.Val;
    }

    public AlignmentValues? Alignment {
        get {
            Justification just = properties?.Justification;
            if (just is null)
                return null;
            if (just.Val.Equals(JustificationValues.Left))
                return AlignmentValues.Left;
            if (just.Val.Equals(JustificationValues.Right))
                return AlignmentValues.Right;
            if (just.Val.Equals(JustificationValues.Center))
                return AlignmentValues.Center;
            if (just.Val.Equals(JustificationValues.Both))
                return AlignmentValues.Justify;
            return null;
        }
    }

    public string LeftIndent {
        get {
            string i = DOCX.Formatting.GetLeftIndent(main, properties);
            if (i is null)
                return null;
            float inches = float.Parse(i) / 1440f;
            return inches.ToString("F2") + "in";
        }
    }
    public string RightIndent {
        get {
            if (properties?.Indentation?.Right is null)
                return null;
            float inches = float.Parse(properties.Indentation.Right.Value) / 1440f;
            return inches.ToString("F2") + "in";
        }
    }

    // public string Number() {
    //     return null;
    // }

    public IEnumerable<IInline> Contents {
        get => contents;
    }

}

}
