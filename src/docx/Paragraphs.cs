
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Paragraphs {

    public static StringValue GetLeftIndentWithStyleButNotNumberingInDXA(MainDocumentPart main, ParagraphProperties props) {
        if (props is null)
            return null;
        StringValue left = props.Indentation?.Left;
        if (left is not null)
            return left;
        Style style = Styles.GetStyle(main, props);
        if (style is null)
            style = Styles.GetDefaultParagraphStyle(main);
        left = style?.StyleParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        return null;
    }

    public static float? GetLeftIndentWithStyleButNotNumberingInInches(MainDocumentPart main, ParagraphProperties props) {
        StringValue dxa = GetLeftIndentWithStyleButNotNumberingInDXA(main, props);
        return Util.DxaToInches(dxa);
    }

    /* like the above, but includes indent from style */
    /* (used, e.g, for determining whether text is true flush left) */
    public static StringValue GetLeftIndentWithNumberingAndStyleInDXA(MainDocumentPart main, ParagraphProperties props) {
        if (props is null)
            return null;
        StringValue left;

        left = props.Indentation?.Left;
        if (left is not null)
            return left;

        left = Numbering.GetOwnLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;

        Style style = Styles.GetStyle(main, props) ?? Styles.GetDefaultParagraphStyle(main);
        left = style?.StyleParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;

        left = Numbering.GetStyleLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;

        return null;
    }
    public static float? GetLeftIndentWithNumberingAndStyleInInches(MainDocumentPart main, ParagraphProperties props) {
        StringValue dxa = GetLeftIndentWithNumberingAndStyleInDXA(main, props);
        return Util.DxaToInches(dxa);
    }

    public static float? GetFirstLineIndentWithoutNumberingOrStyleInInches(MainDocumentPart main, ParagraphProperties pProps) {
        if (pProps is null)
            return null;
        string value = null;
        Indentation indentation = pProps.Indentation;
        if (value is null && indentation?.Hanging is not null)
            value = "-" + indentation.Hanging.Value;
        if (value is null && indentation?.FirstLine is not null)
            value = indentation.FirstLine.Value;
        if (value is null)
            return null;
        return Util.DxaToInches(value);
    }

    public static StringValue GetFirstLineIndentWithStyleButNotNumberingInDXA(MainDocumentPart main, ParagraphProperties pProps) {
        if (pProps is null)
            return null;
        Indentation indentation = pProps.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;
        Style style = Styles.GetStyle(main, pProps);
        if (style is null)
            style = Styles.GetDefaultParagraphStyle(main);
        indentation = style?.StyleParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;
        return null;
    }
    public static float? GetFirstLineIndentWithStyleButNotNumberingInInches(MainDocumentPart main, ParagraphProperties pProps) {
        StringValue dxa = GetFirstLineIndentWithStyleButNotNumberingInDXA(main, pProps);
        return Util.DxaToInches(dxa);
    }

    public static StringValue GetFirstLineIndentWithNumberingAndStyleInDXA(MainDocumentPart main, ParagraphProperties pProps) {
        if (pProps is null)
            return null;
        Indentation indentation;

        indentation = pProps.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;

        indentation = Numbering.GetOwnLevel(main, pProps)?.PreviousParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;

        Style style = Styles.GetStyle(main, pProps);
        if (style is null)
            style = Styles.GetDefaultParagraphStyle(main);
        indentation = style?.StyleParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;

        /* do this only if not overridden by own numbering, which can happen in two ways */
        /* 1. The p's numbering level can have a Indentation.Hanging or Indetation.FirstLine value, or */
        /* 2. The p's numbering id can point to a numbering instance that does not exist! */
        // NumberingId numberingId = pProps?.NumberingProperties?.NumberingId;
        // if (numberingId is not null) {
        //     NumberingInstance numberingInstance = Numbering.GetNumbering(main, numberingId);
        //     if (numberingInstance is not null) {
        //     }
        // }
        // if (pProps?.NumberingProperties?.NumberingId is null)
        //     indentation = null;
        // else
        indentation = Numbering.GetStyleLevel(main, pProps)?.PreviousParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;

        return null;
    }

    public static float? GetFirstLineIndentWithNumberingAndStyleInInches(MainDocumentPart main, ParagraphProperties pProps) {
        StringValue dxa = GetFirstLineIndentWithNumberingAndStyleInDXA(main, pProps);
        return Util.DxaToInches(dxa);
    }

    public static bool IsFlushLeft(MainDocumentPart main, ParagraphProperties pProps) {
        EnumValue<JustificationValues> just = pProps.Justification?.Val ?? JustificationValues.Left;
        if (just == JustificationValues.Center || just == JustificationValues.Right)
            return false;
        float left = GetLeftIndentWithNumberingAndStyleInInches(main, pProps) ?? 0f;
        float firstLine = GetFirstLineIndentWithNumberingAndStyleInInches(main, pProps) ?? 0f;
        return left + firstLine == 0f;
    }

    public static bool IsFlushLeft(MainDocumentPart main, Paragraph para) {
        // if (para.InnerText.StartsWith("Thus, the Judge specifically stated")) {
        //     System.Console.WriteLine();
        // }
        if (para.ChildElements.OfType<Run>().FirstOrDefault()?.ChildElements.Where(e => e is not RunProperties).Where(e => e is not LastRenderedPageBreak).FirstOrDefault() is TabChar)
            return false;
        if (para.ParagraphProperties is null)
            return true;
        return IsFlushLeft(main, para.ParagraphProperties);
    }

    public static float? GetFirstTab(MainDocumentPart main, ParagraphProperties pProps) {
        var x = pProps?.Tabs?.ChildElements.OfType<TabStop>().Where(t => t.Val.Value != TabStopValues.Clear).OrderBy(t => t.Position).FirstOrDefault();
        if (x is null)
            return null;
        return x.Position / 1440f;
    }

    public static float? GetNumTab(MainDocumentPart main, ParagraphProperties pProps) {
        var x = pProps?.Tabs?.ChildElements.OfType<TabStop>().Where(t => t.Val.Value == TabStopValues.Number).OrderBy(t => t.Position).FirstOrDefault();
        if (x is not null)
            return x.Position / 1440f;
        x = Numbering.GetOwnLevel(main, pProps)?.PreviousParagraphProperties?.Tabs?.ChildElements.OfType<TabStop>().Where(t => t.Val.Value == TabStopValues.Number).OrderBy(t => t.Position).FirstOrDefault();
        if (x is not null)
            return x.Position / 1440f;
        x = Numbering.GetStyleLevel(main, pProps)?.PreviousParagraphProperties?.Tabs?.ChildElements.OfType<TabStop>().Where(t => t.Val.Value == TabStopValues.Number).OrderBy(t => t.Position).FirstOrDefault();
        if (x is not null)
            return x.Position / 1440f;
        Style style = Styles.GetStyle(main, pProps) ?? Styles.GetDefaultParagraphStyle(main);
        x = style?.StyleParagraphProperties?.Tabs?.ChildElements.OfType<TabStop>().Where(t => t.Val.Value == TabStopValues.Number).OrderBy(t => t.Position).FirstOrDefault();
        if (x is not null)
            return x.Position / 1440f;
        return null;
    }

}

}
