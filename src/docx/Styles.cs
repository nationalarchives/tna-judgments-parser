#nullable enable

using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

public class FontInfo : IFontInfo {
    public string? Name { get; internal set; }
    public float? SizePt { get; internal set; }
}

class Styles {

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

    public static FontInfo? GetDefaultFont(MainDocumentPart main) {
        Style? style = GetDefaultParagraphStyle(main);
        if (style is null)
            return null;
        string? name = style.StyleRunProperties?.RunFonts?.Ascii?.Value;
        string? size = style.StyleRunProperties?.FontSize?.Val;
        float? sizePt = size is null ? null : float.Parse(size) / 2f;
        return new FontInfo() { Name = name, SizePt = sizePt };
    }

}

}
