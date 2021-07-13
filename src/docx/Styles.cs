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

}

}
