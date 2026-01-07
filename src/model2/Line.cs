
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UK.Gov.Legislation.Lawmaker;

namespace UK.Gov.Legislation.Judgments.Parse {

class WLine : ILine, ILineable {

    internal readonly MainDocumentPart main;
    internal readonly ParagraphProperties properties;
    private List<IInline> contents;

    public IEnumerable<WLine> Lines => [this];

    internal IEnumerable<WBookmark> Bookmarks { get; private set; }
    internal void PrependBookmarksFromPrecedingSkippedLines(IEnumerable<WBookmark> skipped) {
        Bookmarks = skipped.Concat(Bookmarks);
    }

    internal bool IsFirstLineOfNumberedParagraph { get; set; }
    private Paragraph Paragraph { get; init; }

    internal WLine(MainDocumentPart main, Paragraph paragraph) {
        this.main = main;
        this.properties = paragraph.ParagraphProperties;
        var combined = NationalArchives.CaseLaw.Parse.Inline2.ParseContents(main, paragraph);
        this.contents = combined.Where(i => i is not WBookmark).ToList();
        Bookmarks = combined.Where(i => i is WBookmark).Cast<WBookmark>();
        Paragraph = paragraph;
    }

    public WLine(WLine prototype, IEnumerable<IInline> contents) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = contents.ToList();
        Bookmarks = prototype.Bookmarks;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
        Paragraph = prototype.Paragraph;
    }
    protected WLine(WLine prototype) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = prototype.contents;
        Bookmarks = prototype.Bookmarks;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
        Paragraph = prototype.Paragraph;
    }

    public static WLine Make(WLine prototype, IEnumerable<IInline> contents) {
        if (prototype is WOldNumberedParagraph np)
            return new WOldNumberedParagraph(np, contents);
        if (prototype is WRestriction restrict)
            return new WRestriction(restrict, contents);
        if (prototype is WUnknownLine line)
            return new WUnknownLine(line, contents);
        return new WLine(prototype, contents);
    }

    /// <summary>
    /// Override to give more useful information in the debugger
    /// </summary>
    public override string ToString()
    {
        return TextContent ?? base.ToString();
    }

    public static WLine RemoveNumber(WOldNumberedParagraph np) {
        return new WLine(np, np.Contents);
    }

#nullable enable
    public string? Style {
        get => properties?.ParagraphStyleId?.Val;
    }
#nullable disable

    public AlignmentValues? Alignment {
        get {
            Justification just = properties?.Justification;
            if (just is null) {
                if (contents.Any() && !contents.Skip(1).Any() && contents.First() is IMath)
                    return AlignmentValues.Center;
                return null;
            }
            return Convert(just);
        }
    }

    public AlignmentValues? Convert(Justification just) {
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
        if (just.Val.Equals(JustificationValues.Start))
            return AlignmentValues.Left;
        if (just.Val.Equals(JustificationValues.End))
            return AlignmentValues.Right;
        return null;
    }
    public AlignmentValues? OwnAlignment => Convert(properties?.Justification);

    public AlignmentValues? GetEffectiveAlignment() {
        Justification just = properties?.Justification;
        if (just is null && Style is not null) {
            Style style = DOCX.Styles.GetStyle(main, properties);
            if (style is not null) {
                just = DOCX.Styles.GetInheritedProperty(style, s => s.StyleParagraphProperties?.Justification);
            }
        }
        return Convert(just);
    }

    internal float? LeftIndentWithNumber {
        get {
            return DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, properties);
        }
    }

    public float? LeftIndentInches {
        get {
            if (!IsFirstLineOfNumberedParagraph)
                return DOCX.Paragraphs.GetLeftIndentWithStyleButNotNumberingInInches(main, properties);
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
    public float? RightIndentInches {
        get {
            if (properties is null)
                return null;
            string right = properties.Indentation?.Right?.Value;
            if (right is null)
                right = properties.Indentation?.End?.Value;
            if (right is null)
                return null;
            return DOCX.Util.DxaToInches(right);
        }
    }
    public string RightIndent {
        get {
            float? inches = RightIndentInches;
            if (inches == null)
                return null;
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

    public static float GetFirstDefaultTabAfter(float left) {
        return (float) ((Math.Floor(left * 2) + 1) / 2);
    }

    private float? CalculateFirstLineIndentInches() {
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
        float defaultTab = GetFirstDefaultTabAfter(leftIndent) - Math.Abs(leftIndent); // relative
        if (minNumWidth.HasValue && minNumWidth.Value > defaultTab)
            defaultTab = minNumWidth.Value;

        float? firstTab = DOCX.Paragraphs.GetFirstTabAfter(main, properties, leftIndent); // absolute
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

    private float? firstLineIndentInches;

    public float? FirstLineIndentInches {
        get {
            firstLineIndentInches ??= CalculateFirstLineIndentInches();
            return firstLineIndentInches;
        }
    }

    public string FirstLineIndent {
        get {
            float? inches = FirstLineIndentInches;
            if (!inches.HasValue)
                return null;
            return CSS.ConvertSize(inches.Value, "in");
        }
    }

    /// <summary>
    /// The value of the <c>NumberingLevelReference</c> of this <c>WLine</c>, if present,
    /// as defined in the <c>Style</c>. Otherwise defaults to <c>-1</c>.
    /// </summary>
    public int NumberingLevel
    {
        get
        {
            Style style = DOCX.Styles.GetStyle(main, Style);
            StyleParagraphProperties pPr = style?.ChildElements
                .Where(c => c is StyleParagraphProperties)
                .Select(c => c as StyleParagraphProperties)
                .FirstOrDefault();
            NumberingProperties numPr = pPr?.ChildElements
                .Where(c => c is NumberingProperties)
                .Select(c => c as NumberingProperties)
                .FirstOrDefault();
            NumberingLevelReference iLvl = numPr?.ChildElements
                .Where(c => c is NumberingLevelReference)
                .Select(c => c as NumberingLevelReference)
                .FirstOrDefault();
            return iLvl?.Val ?? -1;
        }
    }

    /// <summary>
    /// Determines whether this <c>WLine</c> has a greater numbering level than <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The line with which to compare.</param>
    /// <returns><c>True</c> if this <c>WLine</c> has a greater numbering level than <paramref name="other"/>.</returns>
    public bool HasGreaterNumberingLevelThan(WLine other)
    {
        return this.NumberingLevel > other.NumberingLevel;
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
        set { contents = value.ToList(); }
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

    public float? GetFirstTabAfter(float left) {
        return DOCX.Paragraphs.GetFirstTabAfter(main, properties, left);
    }

    /* methods for detecting whether entire text is italics, bold, etc. */

    private static IEnumerable<IInline> Flatten(IEnumerable<IInline> inlines) {
        return inlines.SelectMany(inline => inline is IInlineContainer container ? Flatten(container.Contents) : [ inline ]);
    }
    private List<WText> GetNonEmptyTexts() {
        return Flatten(contents).Where(i => i is WText).Cast<WText>().Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToList();
    }

    public bool IsAllItalicized() {
        bool fromStyle = false;
        if (Style is not null) {
            Style style = DOCX.Styles.GetStyle(main, Style);
            fromStyle = DOCX.Styles.GetInheritedProperty(style, (s) => DOCX.Util.OnOffToBool(s.StyleRunProperties?.Italic)) ?? false;
        }
        var withText = GetNonEmptyTexts();
        if (fromStyle)
            return !withText.Select(t => t.Italic).Any(i => i.HasValue && !i.Value);
        else
            return withText.Select(t => t.Italic).All(i => i.HasValue && i.Value);
    }

    public bool IsPartiallyItalicized()
    {
        bool fromStyle = false;
        if (Style is not null)
        {
            Style style = DOCX.Styles.GetStyle(main, Style);
            fromStyle = DOCX.Styles.GetInheritedProperty(style, (s) => DOCX.Util.OnOffToBool(s.StyleRunProperties?.Italic)) ?? false;
        }
        var withText = GetNonEmptyTexts();
        if (fromStyle)
            return true;
        else
            return withText.Select(t => t.Italic).Any(i => i.HasValue && i.Value);
    }

    public bool IsAllBold() {
        bool fromStyle = false;
        if (Style is not null) {
            Style style = DOCX.Styles.GetStyle(main, Style);
            fromStyle = DOCX.Styles.GetInheritedProperty(style, (s) => DOCX.Util.OnOffToBool(s.StyleRunProperties?.Bold)) ?? false;
        }
        var withText = GetNonEmptyTexts();
        if (fromStyle)
            return !withText.Select(t => t.Bold).Any(b => b.HasValue && !b.Value);
        else
            return withText.Select(t => t.Bold).All(b => b.HasValue && b.Value);
    }

    public bool IsPartiallyBold()
    {
        bool fromStyle = false;
        if (Style is not null)
        {
            Style style = DOCX.Styles.GetStyle(main, Style);
            fromStyle = DOCX.Styles.GetInheritedProperty(style, (s) => DOCX.Util.OnOffToBool(s.StyleRunProperties?.Bold)) ?? false;
        }
        var withText = GetNonEmptyTexts();
        if (fromStyle)
            return true;
        else
            return withText.Select(t => t.Bold).Any(i => i.HasValue && i.Value);
    }

     public bool IsAllUnderlined() {
        bool fromStyle = false;
        if (Style is not null) {
            Style style = DOCX.Styles.GetStyle(main, Style);
            fromStyle = DOCX.Styles.GetInheritedProperty(style, (s) => (s.StyleRunProperties?.Underline?.Val ?? UnderlineValues.None) != UnderlineValues.None);
        }
        var withText = GetNonEmptyTexts();
        if (fromStyle)
            return !withText.Select(t => t.Underline).Any(u => u.HasValue && u.Value != UnderlineValues2.None);
        else
            return withText.Select(t => t.Underline).All(u => u.HasValue && u.Value != UnderlineValues2.None);
    }

}

class WUnknownLine : WLine, IUnknownLine {

    internal WUnknownLine(WLine line) : base(line) { }

    internal WUnknownLine(WLine proto, IEnumerable<IInline> contents) : base(proto, contents) { }

}

class WRestriction : WLine, IRestriction {

    internal WRestriction(WLine line) : base(line) { }

    internal WRestriction(WRestriction proto, IEnumerable<IInline> contents) : base(proto, contents) { }

}

}
