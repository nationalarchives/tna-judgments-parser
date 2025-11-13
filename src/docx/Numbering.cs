
using System;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Numbering {

    public static NumberingInstance GetNumbering(MainDocumentPart main, int id) {
        return main.NumberingDefinitionsPart?.Numbering.ChildElements
            .OfType<NumberingInstance>()
            .Where(n => n.NumberID.Equals(id))
            .FirstOrDefault();
    }

    public static AbstractNum GetAbstractNum(MainDocumentPart main, string name) {
        return main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<AbstractNum>()
            .Where(abs => name.Equals(abs.AbstractNumDefinitionName?.Val?.Value, StringComparison.InvariantCultureIgnoreCase))
            .FirstOrDefault();
    }
    public static AbstractNum GetAbstractNum(MainDocumentPart main, NumberingInstance numbering) {
        return main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<AbstractNum>()
            .Where(abs => abs.AbstractNumberId.Value == numbering.AbstractNumId.Val.Value)
            .FirstOrDefault();
    }

    public static Level GetLevelAbstract(MainDocumentPart main, int absNumId, int ilvl) {
        AbstractNum abs = main.NumberingDefinitionsPart.Numbering.ChildElements
            .OfType<AbstractNum>()
            .Where(a => a.AbstractNumberId.Value == absNumId)
            .First();
        Level level = abs.ChildElements
            .OfType<Level>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();
        return level;
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
        level = abs.ChildElements
            .OfType<Level>()
            .Where(l => l.LevelIndex.Value == ilvl)
            .FirstOrDefault();  // does not exist in EWHC/Ch/2003/2902
        if (level is null && abs.NumberingStyleLink is not null) {    // // EWHC/Ch/2012/190
            string numStyleId = abs.NumberingStyleLink.Val.Value;
            Style numStyle = Styles.GetStyle(main, StyleValues.Numbering, numStyleId);
            if (numStyle is not null) { // style does not exist in 00393_ukut_iac_2012_mf_nigeria
                int styleNumId = numStyle.StyleParagraphProperties.NumberingProperties.NumberingId.Val.Value;
                level = GetLevel(main, styleNumId, ilvl);
            }
        }
        return level;
    }

    internal static int? GetNumberingIdOrNumberingChangeId(NumberingProperties props) {  // EWCA/Civ/2004/1580
        if (props.NumberingId?.Val?.Value is not null)
            return props.NumberingId.Val.Value;
        if (props.NumberingChange?.Id?.Value is not null)
            return int.Parse(props.NumberingChange.Id.Value);
        return null;
    }

    private static Level GetLevel(MainDocumentPart main, NumberingProperties props) {
        int? numId = GetNumberingIdOrNumberingChangeId(props);
        if (numId is null)
            return null;
        int ilvl = props?.NumberingLevelReference?.Val?.Value ?? 0;
        return GetLevel(main, numId.Value, ilvl);
    }
    internal static Level GetOwnLevel(MainDocumentPart main, ParagraphProperties pProps) {
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

    internal static Level GetLevel(MainDocumentPart main, Style style) {
        if (style is null)
            return null;
        int? id = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);
        if (!id.HasValue)
            return null;
        NumberingProperties props = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties);
        /* can's use Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value) */
        int ilvl = props?.NumberingLevelReference?.Val?.Value ?? 0;
        return GetLevel(main, id.Value, ilvl);
    }
    internal static Level GetStyleLevel(MainDocumentPart main, ParagraphProperties pProps) {
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

    public static Level GetLevelOrStyleLevel(MainDocumentPart main, Paragraph paragraph) {
        (int? numId, int ilvl) = GetNumberingIdAndIlvl(main, paragraph);
        if (!numId.HasValue)
            return null;
        return GetLevel(main, numId.Value, ilvl);
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

    internal static Tuple<int?, int> GetNumberingIdAndIlvl(MainDocumentPart main, Paragraph paragraph) {
        int? id = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
        if (!id.HasValue && paragraph.ParagraphProperties?.NumberingProperties?.NumberingChange?.Id?.Value is not null)
            id = int.Parse(paragraph.ParagraphProperties.NumberingProperties.NumberingChange.Id.Value);
        int? ilvl = paragraph.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
        if (!id.HasValue || !ilvl.HasValue) {
            Style style = Styles.GetStyle(main, paragraph);
            if (style is null)
                style = Styles.GetDefaultParagraphStyle(main);
            if (!id.HasValue)
                id = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);
            if (!ilvl.HasValue)
                ilvl = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value);
        }
        return new Tuple<int?, int>(id, ilvl ?? 0);
    }

}

}
