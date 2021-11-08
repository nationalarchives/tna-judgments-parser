
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WLine : ILine {

    private readonly MainDocumentPart main;
    private readonly ParagraphProperties properties;
    private IEnumerable<IInline> contents;

    internal bool IsFirstLineOfNumberedParagraph { get; init; }

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
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
    }
    internal WLine(WLine prototype) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = prototype.contents;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
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
            float? inches = DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, properties);
            if (inches is null)
                return null;
            return inches.Value.ToString("F2") + "in";
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
    public string FirstLineIndent {
        get {
            float? inches = DOCX.Paragraphs.GetFirstLineIndentWithStyleButNotNumberingInInches(main, properties);
            if (!inches.HasValue)
                return IsFirstLineOfNumberedParagraph ? "0.5in" : null;
            if (IsFirstLineOfNumberedParagraph && inches < 0.0f)
                return null;
            // when number and first line indent is 0, default indent is a tab
            if (IsFirstLineOfNumberedParagraph)
                inches = inches.Value + 0.5f;
            // if (IsFirstLineOfNumberedParagraph && inches < 0.5f)
            //     return "0.5in";
            return inches.Value.ToString("F2") + "in";
        }
    }

    public IEnumerable<IInline> Contents {
        get => contents;
        set { contents = value; }
    }

}

class WRestriction : WLine, IRestriction {

    internal WRestriction(WLine line) : base(line) { }

}

}
