
using System;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Numbering {

    public static NumberingInstance GetNumbering(MainDocumentPart main, int id) {
        return main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<NumberingInstance>()
            .Where(n => n.NumberID.Equals(id))
            .FirstOrDefault();
    }
    public static NumberingInstance GetNumbering(MainDocumentPart main, NumberingId id) {
        return main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<NumberingInstance>()
            .Where(n => n.NumberID.Equals(id.Val))
            .FirstOrDefault();
    }

    public static NumberingInstance GetNumbering(MainDocumentPart main, Paragraph paragraph) {
        NumberingId id = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId;
        if (id is null)
            return null;
        return GetNumbering(main, id);
    }

    public static AbstractNum GetAbstractNum(MainDocumentPart main, NumberingInstance numbering) {
        return main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<AbstractNum>()
            .Where(abs => abs.AbstractNumberId.Value == numbering.AbstractNumId.Val.Value)
            .FirstOrDefault();
    }

    public static Level GetLevel(MainDocumentPart main, int numberingId, int ilvl) {
        NumberingInstance numbering = GetNumbering(main, numberingId);
        if (numbering is null)  // this does happen, I think it means that (style) numbering is removed
            return null;
        Level level = numbering.Descendants<Level>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();
        if (level is not null)
            return level;
        AbstractNum abs = main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<AbstractNum>()
            .Where(a => a.AbstractNumberId.Value == numbering.AbstractNumId.Val.Value)
            .First();
        return abs.ChildElements
            .OfType<Level>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .First();
    }
    public static Level GetLevel(MainDocumentPart main, NumberingId id, int ilvl) {
        return GetLevel(main, id.Val, ilvl);
    }
    public static Level GetLevel(MainDocumentPart main, NumberingProperties props) {
        NumberingId id = props.NumberingId;
        int ilvl = props?.NumberingLevelReference?.Val?.Value ?? 0;
        return GetLevel(main, id, ilvl);
    }
    public static Level GetOwnLevel(MainDocumentPart main, ParagraphProperties pProps) {
        NumberingProperties props = pProps?.NumberingProperties;
        if (props is null)
            return null;
        return GetLevel(main, props);
    }
    public static Level GetOwnLevel(MainDocumentPart main, Paragraph paragraph) {
        ParagraphProperties pProps = paragraph.ParagraphProperties;
        if (pProps is null)
            return null;
        return GetOwnLevel(main, pProps);
    }

    public static Level GetLevel(MainDocumentPart main, Style style) {
        NumberingProperties props = style?.StyleParagraphProperties?.NumberingProperties;
        if (props is null)
            return null;
        return GetLevel(main, props);
    }
    public static Level GetStyleLevel(MainDocumentPart main, ParagraphProperties pProps) {
        ParagraphStyleId styleId = pProps.ParagraphStyleId;
        if (styleId is null)
            return null;
        Style style = Styles.GetStyle(main, styleId);
        return GetLevel(main, style);
    }
    public static Level GetStyleLevel(MainDocumentPart main, Paragraph paragraph) {
        ParagraphProperties pProps = paragraph.ParagraphProperties;
        if (pProps is null)
            return null;
        return GetStyleLevel(main, pProps);
    }

    public static Level GetLevelOrStyleLevel(MainDocumentPart main, ParagraphProperties pProps) {
        NumberingProperties numProps = pProps?.NumberingProperties;
        if (pProps is null)
            return GetStyleLevel(main, pProps);
        return GetLevel(main, numProps);
    }
    public static Level GetLevelOrStyleLevel(MainDocumentPart main, Paragraph paragraph) {
        NumberingProperties props = paragraph.ParagraphProperties?.NumberingProperties;
        if (props is null)
            return GetStyleLevel(main, paragraph);
        return GetLevel(main, props);
    }

    public static bool HasNumberOrMarker(MainDocumentPart main, Paragraph paragraph) {
        Level level = GetLevelOrStyleLevel(main, paragraph);
        if (level is null)
            return false;
        var format = level.NumberingFormat?.Val;
        if (format is null)
            return false;
        return !format.Equals(NumberFormatValues.None);
    }

    internal static NumberingProperties GetNumberingPropertiesOrStyleNumberingProperties(MainDocumentPart main, Paragraph paragraph) {
        NumberingProperties props = paragraph.ParagraphProperties?.NumberingProperties;
        if (props is null) {
            ParagraphStyleId styleId = paragraph.ParagraphProperties?.ParagraphStyleId;
            if (styleId is not null) {
                Style style = Styles.GetStyle(main, styleId);
                props = style?.StyleParagraphProperties?.NumberingProperties;
            }
        }
        return props;
    }

}

}
