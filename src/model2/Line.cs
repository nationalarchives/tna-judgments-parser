
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WLine : ILine {

    private readonly MainDocumentPart main;
    private readonly ParagraphProperties properties;
    private IEnumerable<IInline> contents;

    internal bool IsFirstLineOfNumberedParagraph { get; init; }
    private Paragraph Paragraph { get; init; }

    public WLine(MainDocumentPart main, ParagraphProperties properties, IEnumerable<IInline> contents) {
        this.main = main;
        this.properties = properties;
        this.contents = contents;
        Paragraph = null;
    }
    public WLine(MainDocumentPart main, Paragraph paragraph) {
        this.main = main;
        this.properties = paragraph.ParagraphProperties;
        this.contents = Inline.ParseRuns(main, paragraph.ChildElements);
        Paragraph = paragraph;
    }
    public WLine(WLine prototype, IEnumerable<IInline> contents) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = contents;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
        Paragraph = prototype.Paragraph;
    }
    internal WLine(WLine prototype) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = prototype.contents;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
        Paragraph = prototype.Paragraph;
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

    public float? LeftIndentInches {
        get => DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, properties);
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

    private float? CalculateMinNumberWidth() {
        if (Paragraph is null)
            return null;
        var info = DOCX.Numbering2.GetFormattedNumber(this.main, Paragraph);
        if (info is null)
            return null;
        string num = info.Value.Number.TrimEnd('.');
        return num.Length * 0.125f;
    }

    public float? FirstLineIndentInches {
        get {
            float? relative1 = DOCX.Paragraphs.GetFirstLineIndentWithNumberingAndStyleInInches(main, properties);
            if (!IsFirstLineOfNumberedParagraph)
                return relative1;

            float relative = relative1 ?? 0;

            float? minNumWidth = CalculateMinNumberWidth();

            if (relative < 0) {
                var abs = Math.Abs(relative);
                if (minNumWidth.HasValue && minNumWidth.Value > abs)
                    return minNumWidth.Value - abs;
                return null;
            }

            float leftIndent = this.LeftIndentInches ?? 0f;
            float defaultTab;
            if (leftIndent == 0) {
                defaultTab = 0.5f;
            } if (leftIndent > 0) {
                defaultTab = (float) ((Math.Floor(leftIndent * 2) + 1) / 2 - leftIndent);
            } else {
                defaultTab = (float) ((Math.Floor(Math.Abs(leftIndent) * 2) + 1) / 2 - Math.Abs(leftIndent));
            }
            if (minNumWidth.HasValue && minNumWidth.Value > defaultTab)
                defaultTab = minNumWidth.Value;

            float? firstTab = DOCX.Paragraphs.GetFirstTab(main, properties); // this is absolute not relative
            if (!firstTab.HasValue)
                return relative + defaultTab;
            float hardFirst = leftIndent + relative;
            float firstTabRelative = firstTab.Value - hardFirst;
            if (firstTabRelative > defaultTab)
                return relative + defaultTab;
            if (minNumWidth.HasValue && minNumWidth.Value > firstTabRelative)   // necessary?
                return relative + minNumWidth.Value;
            return relative + firstTabRelative;
        }
    }

    public string FirstLineIndent {
        get {
            float? inches = this.FirstLineIndentInches;
            if (!inches.HasValue)
                return null;
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
