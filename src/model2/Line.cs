
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WLine : ILine {

    private readonly MainDocumentPart main;
    private readonly ParagraphProperties properties;
    private IEnumerable<IInline> contents;

    internal bool IsFirstLineOfNumberedParagraph { get; set; }
    private Paragraph Paragraph { get; init; }

    [Obsolete]
    internal WLine(MainDocumentPart main, ParagraphProperties properties, IEnumerable<IInline> contents) {
        this.main = main;
        this.properties = properties;
        this.contents = contents;
        Paragraph = null;
    }
    internal WLine(MainDocumentPart main, Paragraph paragraph) {
        this.main = main;
        this.properties = paragraph.ParagraphProperties;
        this.contents = Inline.ParseRuns(main, paragraph.ChildElements);
        Paragraph = paragraph;
    }
    internal WLine(MainDocumentPart main, Paragraph paragraph, IEnumerable<IInline> contents) {
        this.main = main;
        this.properties = paragraph.ParagraphProperties;
        this.contents = contents;
        Paragraph = paragraph;
    }

    [Obsolete]
    internal WLine(WLine prototype, IEnumerable<IInline> contents) {
        // if (prototype.GetType().IsSubclassOf(typeof(WLine))) {
        // }
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = contents;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
        Paragraph = prototype.Paragraph;
    }
    [Obsolete]
    internal WLine(WLine prototype) {
        // if (prototype.GetType().IsSubclassOf(typeof(WLine))) {
        // }
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = prototype.contents;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
        Paragraph = prototype.Paragraph;
    }

    public static WLine Make(WLine prototype, IEnumerable<IInline> contents) {
        if (prototype is WOldNumberedParagraph np)
            return new WOldNumberedParagraph(np, contents);
        if (prototype is WRestriction restrict)
            return new WRestriction(restrict, contents);
        return new WLine(prototype, contents);
    }
    public static WLine RemoveNumber(WOldNumberedParagraph np) {
        return new WLine(np, np.Contents);
    }

    public string Style {
        get => properties?.ParagraphStyleId?.Val;
    }

    public AlignmentValues? Alignment {
        get {
            Justification just = properties?.Justification;
            if (just is null) {
                if (contents.Any() && !contents.Skip(1).Any() && contents.First() is IMath)
                    return AlignmentValues.Center;
                return null;
            }
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

    internal float? LeftIndentWithNumber {
        get {
            return DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, properties);
        }
    }

    public float? LeftIndentInches {
        get {
            if (!IsFirstLineOfNumberedParagraph)
                return DOCX.Paragraphs.GetLeftIndentWithoutNumberingOrStyleInInches(main, properties);
            else
                return DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, properties);
        }
    }
    public string LeftIndent {
        get {
            float? inches = this.LeftIndentInches;
            if (inches is null)
                return null;
            return CSS.ConvertSize(inches, "in");
        }
    }
    public string RightIndent {
        get {
            if (properties is null)
                return null;
            string right = properties.Indentation?.Right?.Value;
            if (right is null)
                right = properties.Indentation?.End?.Value;
            if (right is null)
                return null;
            float inches = DOCX.Util.DxaToInches(right);
            return CSS.ConvertSize(inches, "in");
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

    internal float? FirstLineIndentWithNumber {
        get => DOCX.Paragraphs.GetFirstLineIndentWithNumberingAndStyleInInches(main, properties);
    }

    public float? FirstLineIndentInches {
        get {
            if (!IsFirstLineOfNumberedParagraph)
                return DOCX.Paragraphs.GetFirstLineIndentWithoutNumberingOrStyleInInches(main, properties);

            float relative = DOCX.Paragraphs.GetFirstLineIndentWithNumberingAndStyleInInches(main, properties) ?? 0;

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
            return CSS.ConvertSize(inches, "in");
        }
    }

    public float? BorderTopWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(properties?.ParagraphBorders?.TopBorder);
    }
    public CellBorderStyle? BorderTopStyle {
        get => DOCX.Tables.ExtractBorderStyle(properties?.ParagraphBorders?.TopBorder);
    }
    public string BorderTopColor {
        get => DOCX.Tables.ExtractBorderColor(properties?.ParagraphBorders?.TopBorder);
    }

    public float? BorderRightWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(properties?.ParagraphBorders?.RightBorder);
    }
    public CellBorderStyle? BorderRightStyle {
        get => DOCX.Tables.ExtractBorderStyle(properties?.ParagraphBorders?.RightBorder);
    }
    public string BorderRightColor {
        get => DOCX.Tables.ExtractBorderColor(properties?.ParagraphBorders?.RightBorder);
    }

    public float? BorderBottomWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(properties?.ParagraphBorders?.BottomBorder);
    }
    public CellBorderStyle? BorderBottomStyle {
        get => DOCX.Tables.ExtractBorderStyle(properties?.ParagraphBorders?.BottomBorder);
    }
    public string BorderBottomColor {
        get => DOCX.Tables.ExtractBorderColor(properties?.ParagraphBorders?.BottomBorder);
    }

    public float? BorderLeftWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(properties?.ParagraphBorders?.LeftBorder);
    }
    public CellBorderStyle? BorderLeftStyle {
        get => DOCX.Tables.ExtractBorderStyle(properties?.ParagraphBorders?.LeftBorder);
    }
    public string BorderLeftColor {
        get => DOCX.Tables.ExtractBorderColor(properties?.ParagraphBorders?.LeftBorder);
    }

    public IEnumerable<IInline> Contents {
        get => contents;
        set { contents = value; }
    }

    private string _textContent;
    virtual public string TextContent {
        get {
            if (_textContent is null)
                _textContent = IInline.ToString(contents);
            return _textContent;
        }
    }

    private string _normalized;
    public string NormalizedContent {
        get {
            if (_normalized is null)
                _normalized = Regex.Replace(TextContent, @"\s+", " ").Trim();
            return _normalized;
        }
    }

}

class WRestriction : WLine, IRestriction {

    internal WRestriction(WLine line) : base(line) { }

    internal WRestriction(WRestriction proto, IEnumerable<IInline> contents) : base(proto, contents) { }

}

}
