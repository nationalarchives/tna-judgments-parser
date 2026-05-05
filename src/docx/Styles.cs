#nullable enable

using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

public class FontInfo : IFontInfo {
    public string? Name { get; internal set; }
    public float? SizePt { get; internal set; }
}

static class Styles {

    public static Style? GetStyle(DocumentFormat.OpenXml.Wordprocessing.Styles styles, StyleValues type, string id) {
        return styles.ChildElements
            .OfType<Style>()
            .Where(s => s.Type is not null && s.Type == type)
            .Where(s => s.StyleId?.Value is not null && s.StyleId.Value.Equals(id))
            .FirstOrDefault();
    }
    public static Style? GetStyle(MainDocumentPart main, StyleValues type, string id) {
        DocumentFormat.OpenXml.Wordprocessing.Styles? styles = main.StyleDefinitionsPart?.Styles;
        if (styles is null)
            return null;
        return GetStyle(styles, type, id);
    }

    public static Style? GetStyle(DocumentFormat.OpenXml.Wordprocessing.Styles styles, string id) {
        return styles.ChildElements
            .OfType<Style>()
            .Where(s => s.StyleId?.Value is not null && s.StyleId.Value.Equals(id))
            .FirstOrDefault();
    }
    public static Style? GetStyle(StyleDefinitionsPart part, string id) {
        if (part.Styles is null)
            return null;
        return GetStyle(part.Styles, id);
    }
    public static Style? GetStyle(MainDocumentPart main, string id) {
        return main.StyleDefinitionsPart?.Styles?.ChildElements
            .OfType<Style>()
            .Where(s => s.StyleId?.Value is not null && s.StyleId.Value.Equals(id))
            .FirstOrDefault();
    }
    public static Style? GetStyle(MainDocumentPart main, ParagraphStyleId id) {
        if (id.Val?.Value is null)
            return null;
        return GetStyle(main, id.Val.Value);
    }
    public static Style? GetStyle(MainDocumentPart main, ParagraphProperties pProps) {
        if (pProps.ParagraphStyleId is null)
            return null;
        return GetStyle(main, pProps.ParagraphStyleId);
    }
    public static Style? GetStyle(MainDocumentPart main, Paragraph paragraph) {
        if (paragraph.ParagraphProperties is null)
            return null;
        return GetStyle(main, paragraph.ParagraphProperties);
    }

    public static Style? GetDefaultParagraphStyle(MainDocumentPart main) {
        return main.StyleDefinitionsPart?.Styles?.ChildElements
            .OfType<Style>()
            .Where(s => s.Type is not null && s.Type == StyleValues.Paragraph)
            .Where(s => s.Default is not null && s.Default.Value)
            .FirstOrDefault();
    }
    public static Style? GetDefaultCharacterStyle(MainDocumentPart main) {
        return main.StyleDefinitionsPart?.Styles?.ChildElements
            .OfType<Style>()
            .Where(s => s.Type is not null && s.Type == StyleValues.Character)
            .Where(s => s.Default is not null && s.Default.Value)
            .FirstOrDefault();
    }

    internal delegate T? StylePropertyGetter<T>(Style style);

    internal static T? GetStyleProperty<T>(Style? style, StylePropertyGetter<T> getValue) {
        if (style is null)
            return default(T);
        T? value = getValue(style);
        if (value is not null)
            return value;
        string? parentId = style.BasedOn?.Val?.Value;
        if (parentId is null)
            return default(T);
        DocumentFormat.OpenXml.Wordprocessing.Styles root = (DocumentFormat.OpenXml.Wordprocessing.Styles) style.Ancestors<OpenXmlPartRootElement>().First();
        Style? parent = GetStyle(root, parentId);
        return GetStyleProperty(parent, getValue);
    }

    public static T? GetInheritedProperty<T>(this Style style, StylePropertyGetter<T> getValue) {
        return GetStyleProperty(style, getValue);
    }

    internal enum HeadingSignal {
        Authoritative,
        // Bold + size only. Some templates style address blocks and
        // signature lines the same way as chapter headings, so callers must
        // confirm with content-based checks.
        Visual
    }

    internal record struct HeadingClassification(int Depth, HeadingSignal Signal);

    /// Returns depth (1..6) + signal tier, or null for body paragraphs.
    /// Tries: outlineLvl on the style or its basedOn chain, "Heading\d"
    /// name match, then bold + size as a visual fallback.
    internal static HeadingClassification? ClassifyHeading(MainDocumentPart? main, string? styleId) {
        if (main is null || string.IsNullOrEmpty(styleId)) return null;
        Style? style = GetStyle(main, styleId);
        if (style is null) return null;

        var outlineLvl = style.GetInheritedProperty(s => s.StyleParagraphProperties?.OutlineLevel);
        if (outlineLvl?.Val?.Value is int level && level >= 0 && level < 6)
            return new HeadingClassification(level + 1, HeadingSignal.Authoritative);

        var m = System.Text.RegularExpressions.Regex.Match(styleId!, @"^heading\s*(\d)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success) {
            int d = int.Parse(m.Groups[1].Value);
            if (d >= 1 && d <= 6) return new HeadingClassification(d, HeadingSignal.Authoritative);
        }

        var bold = style.GetInheritedProperty(s => s.StyleRunProperties?.Bold);
        var fontSize = style.GetInheritedProperty(s => s.StyleRunProperties?.FontSize?.Val);
        if (bold is null || fontSize?.Value is null) return null;
        if (!int.TryParse(fontSize.Value, out int sizeHalfPts)) return null;
        if (sizeHalfPts < 26) return null;
        int visualDepth = sizeHalfPts >= 36 ? 1 : (sizeHalfPts >= 30 ? 2 : 3);
        return new HeadingClassification(visualDepth, HeadingSignal.Visual);
    }

}

}
