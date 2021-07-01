
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Paragraphs {

    public static Document GetRoot(this Paragraph para) {
        return (Document) para.Ancestors<Document>().First();
    }

    public static MainDocumentPart GetMain(this Paragraph para) {
        Document root = para.GetRoot();
        return root.MainDocumentPart;
    }

    public static StringValue GetLeftIndentWithStyleButNotNumberingInDXA(MainDocumentPart main, ParagraphProperties props) {
        if (props is null)
            return null;
        StringValue left = props.Indentation?.Left;
        if (left is not null)
            return left;
        left = Styles.GetStyle(main, props)?.StyleParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        return null;
    }
    public static float? GetLeftIndentWithStyleButNotNumberingInInches(MainDocumentPart main, ParagraphProperties props) {
        StringValue dxa = GetLeftIndentWithStyleButNotNumberingInDXA(main, props);
        if (dxa is null)
            return null;
        return float.Parse(dxa.Value) / 1440f;
    }


    /* returns result in inches */
    /* does not include indent defined in style (that is, assumes style is already applied) */
    // public static float? GetLeftIndent1(MainDocumentPart main, ParagraphProperties props) {
    //     if (props is null)
    //         return null;
    //     StringValue left;
    //     left = props.Indentation?.Left;
    //     if (left is null)
    //         left = Numbering.GetOwnLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Left;
    //     if (left is null && props.NumberingProperties?.NumberingId is null)
    //         left = Numbering.GetStyleLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Left;
    //     if (left is null)
    //         return null;
    //     return float.Parse(left.Value) / 1440f;
    // }
    // public static float? GetLeftIndent1(this Paragraph para) {
    //     return GetLeftIndent1(para.GetMain(), para.ParagraphProperties);
    // }

    /* like the above, but includes indent from style */
    /* (used, e.g, for determining whether text is true flush left) */
    public static StringValue GetLeftIndentWithNumberingAndStyleInDXA(MainDocumentPart main, ParagraphProperties props) {
        if (props is null)
            return null;
        StringValue left = props.Indentation?.Left;
        if (left is not null)
            return left;
        left = Numbering.GetOwnLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        left = Styles.GetStyle(main, props)?.StyleParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;

        NumberingId numberingId = props?.NumberingProperties?.NumberingId;
        if (numberingId is not null) {
            NumberingInstance numberingInstance = Numbering.GetNumbering(main, numberingId);
            if (numberingInstance is null)
                return null;
        }
        left = Numbering.GetStyleLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        return null;
    }
    public static float? GetLeftIndentWithNumberingAndStyleInInches(MainDocumentPart main, ParagraphProperties props) {
        StringValue dxa = GetLeftIndentWithNumberingAndStyleInDXA(main, props);
        if (dxa is null)
            return null;
        return float.Parse(dxa.Value) / 1440f;
    }

    public static float? GetFirstLineIndentWithoutNumberingOrStyle(MainDocumentPart main, ParagraphProperties pProps) {
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
        return float.Parse(value) / 1440f;
    }

    public static StringValue GetFirstLineIndentWithStyleButNotNumberingInDXA(MainDocumentPart main, ParagraphProperties pProps) {
        if (pProps is null)
            return null;
        Indentation indentation = pProps.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;
        indentation = Styles.GetStyle(main, pProps)?.StyleParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;
        return null;
    }
    public static float? GetFirstLineIndentWithStyleButNotNumberingInInches(MainDocumentPart main, ParagraphProperties pProps) {
        StringValue dxa = GetFirstLineIndentWithStyleButNotNumberingInDXA(main, pProps);
        if (dxa is null)
            return null;
        return float.Parse(dxa.Value) / 1440f;
    }

    // public static float? GetFirstLineIndent1(MainDocumentPart main, ParagraphProperties pProps) {
    //     if (pProps is null)
    //         return null;
    //     string value = null;
    //     Indentation indentation = pProps.Indentation;
    //     if (value is null && indentation?.Hanging is not null)
    //         value = "-" + indentation.Hanging.Value;
    //     if (value is null && indentation?.FirstLine is not null)
    //         value = indentation.FirstLine.Value;

    //     indentation = Numbering.GetOwnLevel(main, pProps)?.PreviousParagraphProperties?.Indentation;
    //     if (value is null && indentation?.Hanging is not null)
    //         value = "-" + indentation.Hanging.Value;
    //     if (value is null && indentation?.FirstLine is not null)
    //         value = indentation.FirstLine.Value;

    //     indentation = Numbering.GetStyleLevel(main, pProps)?.PreviousParagraphProperties?.Indentation;
    //     if (value is null && indentation?.Hanging is not null)
    //         value = "-" + indentation.Hanging.Value;
    //     if (value is null && indentation?.FirstLine is not null)
    //         value = indentation.FirstLine.Value;

    //     if (value is null)
    //         return null;
    //     return float.Parse(value) / 1440f;
    // }

    public static StringValue GetFirstLineIndentWithNumberingAndStyleInDXA(MainDocumentPart main, ParagraphProperties pProps) {
        if (pProps is null)
            return null;
        Indentation indentation = pProps.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;

        indentation = Numbering.GetOwnLevel(main, pProps)?.PreviousParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;

        indentation = Styles.GetStyle(main, pProps)?.StyleParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;

        /* do this only if not overridden by own numbering, which can happen in two ways */
        /* 1. The p's numbering level can have a Indentation.Hanging or Indetation.FirstLine value, or */
        /* 2. The p's numbering id can point to a numbering instance that does not exist! */
        NumberingId numberingId = pProps?.NumberingProperties?.NumberingId;
        if (numberingId is not null) {
            NumberingInstance numberingInstance = Numbering.GetNumbering(main, numberingId);
            if (numberingInstance is null)
                return null;
        }
        indentation = Numbering.GetStyleLevel(main, pProps)?.PreviousParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;
        return null;
    }

    public static float? GetFirstLineIndentWithNumberingAndStyleInInches(MainDocumentPart main, ParagraphProperties pProps) {
        StringValue dxa = GetFirstLineIndentWithNumberingAndStyleInDXA(main, pProps);
        if (dxa is null)
            return null;
        return float.Parse(dxa.Value) / 1440f;
    }

    // public static float? GetHangingIndent1(MainDocumentPart main, ParagraphProperties pProps) {
    //     if (pProps is null)
    //         return null;
    //     StringValue hanging = pProps.Indentation?.Hanging;
    //     if (hanging is null)
    //         hanging = Numbering.GetOwnLevel(main, pProps)?.PreviousParagraphProperties?.Indentation?.Hanging;
    //     if (hanging is null && pProps.NumberingProperties?.NumberingId is null)
    //         hanging = Numbering.GetStyleLevel(main, pProps)?.PreviousParagraphProperties?.Indentation?.Hanging;
    //     // NumberingId numberingId = pProps.NumberingProperties?.NumberingId;
    //     // if (numberingId is null)
    //     //     return styleNumberingValue;
    //     // return (Numbering.GetNumbering(main, numberingId) is null) ? null : styleNumberingValue;
    //     if (hanging is null)
    //         return null;
    //     return float.Parse(hanging.Value) / 1440f;
    // }
    // public static float? GetHangingIndent2(MainDocumentPart main, ParagraphProperties pProps) {
    //     if (pProps is null)
    //         return null;
    //     StringValue hanging = pProps.Indentation?.Hanging;
    //     if (hanging is null)
    //         hanging = Numbering.GetOwnLevel(main, pProps)?.PreviousParagraphProperties?.Indentation?.Hanging;
    //     if (hanging is null)
    //         hanging = Styles.GetStyle(main, pProps)?.StyleParagraphProperties?.Indentation?.Hanging;
    //     if (hanging is null && pProps.NumberingProperties?.NumberingId is null)
    //         hanging = Numbering.GetStyleLevel(main, pProps)?.PreviousParagraphProperties?.Indentation?.Hanging;
    //     // NumberingId numberingId = pProps.NumberingProperties?.NumberingId;
    //     // if (numberingId is null)
    //     //     return styleNumberingValue;
    //     // return (Numbering.GetNumbering(main, numberingId) is null) ? null : styleNumberingValue;
    //     if (hanging is null)
    //         return null;
    //     return float.Parse(hanging.Value) / 1440f;
    // }

    public static bool IsFlushLeft(MainDocumentPart main, ParagraphProperties pProps) {
        EnumValue<JustificationValues> just = pProps.Justification?.Val ?? JustificationValues.Left;
        if (just == JustificationValues.Center || just == JustificationValues.Right)
            return false;
        float left = GetLeftIndentWithNumberingAndStyleInInches(main, pProps) ?? 0f;
        float firstLine = GetFirstLineIndentWithNumberingAndStyleInInches(main, pProps) ?? 0f;
        return left + firstLine == 0f;
        // if (firstLine > left)
        //     return false;
        // if (firstLine == left)
        //     return left == 0f;
    }

    public static bool IsFlushLeft(MainDocumentPart main, Paragraph para) {
        if (para.ChildElements.OfType<Run>().FirstOrDefault()?.ChildElements.FirstOrDefault() is TabChar)
            return false;
        if (para.ParagraphProperties is null)
            return true;
        return IsFlushLeft(main, para.ParagraphProperties);
    }

}

}
