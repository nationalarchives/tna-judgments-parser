
using System;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Formatting {

    internal static String ToLowerLetter(int n) {
        if (n < 1)
            return string.Empty;
        char digit = (char) (97 + ((n - 1) % 26));
        string rest = ToLowerLetter((n - 1) / 26);
        return rest + digit.ToString();
    }

    internal static String ToLowerRoman(int n) {
        return ToUpperRoman(n).ToLower();
    }

    internal static String ToUpperRoman(int n) {
        if (n > 3999)
            throw new ArgumentOutOfRangeException(n.ToString());
        if (n < 1)
            return string.Empty;            
        if (n >= 1000)
            return "M" + ToUpperRoman(n - 1000);
        if (n >= 900)
            return "CM" + ToUpperRoman(n - 900); 
        if (n >= 500)
            return "D" + ToUpperRoman(n - 500);
        if (n >= 400)
            return "CD" + ToUpperRoman(n - 400);
        if (n >= 100)
            return "C" + ToUpperRoman(n - 100);            
        if (n >= 90)
            return "XC" + ToUpperRoman(n - 90);
        if (n >= 50)
            return "L" + ToUpperRoman(n - 50);
        if (n >= 40)
            return "XL" + ToUpperRoman(n - 40);
        if (n >= 10)
            return "X" + ToUpperRoman(n - 10);
        if (n >= 9)
            return "IX" + ToUpperRoman(n - 9);
        if (n >= 5)
            return "V" + ToUpperRoman(n - 5);
        if (n >= 4)
            return "IV" + ToUpperRoman(n - 4);
        if (n >= 1)
            return "I" + ToUpperRoman(n - 1);
        throw new ArgumentOutOfRangeException("something bad happened");
    }

    public static StringValue GetLeftIndent(MainDocumentPart main, ParagraphProperties pProps) {
        StringValue left;
        left = Numbering.GetOwnLevel(main, pProps)?.PreviousParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        left = pProps.Indentation?.Left;
        if (left is not null)
            return left;
        left = Styles.GetStyle(main, pProps)?.StyleParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        /* if style numbering has a left indent, return it only if numbering is not overridden by pProps */
        StringValue styleNumberingValue = Numbering.GetStyleLevel(main, pProps)?.PreviousParagraphProperties?.Indentation?.Left;
        if (styleNumberingValue is null)
            return null;
        NumberingId numberingId = pProps.NumberingProperties?.NumberingId;
        if (numberingId is null)
            return styleNumberingValue;
        NumberingInstance numberingInstance = Numbering.GetNumbering(main, numberingId);
        return (numberingInstance is null) ? null : styleNumberingValue;
    }

    public static StringValue GetLeftIndent(MainDocumentPart main, Paragraph paragraph) {
        ParagraphProperties pProps = paragraph.ParagraphProperties;
        if (pProps is null)
            return null;
        return GetLeftIndent(main, pProps);
    }

    public static StringValue GetHangingIndent(MainDocumentPart main, ParagraphProperties pProps) {
        StringValue hanging = pProps.Indentation?.Hanging;
        if (hanging is not null)
            return hanging;
        hanging = Numbering.GetOwnLevel(main, pProps)?.PreviousParagraphProperties?.Indentation?.Hanging;
        if (hanging is not null)
            return hanging;
        hanging = Styles.GetStyle(main, pProps)?.StyleParagraphProperties?.Indentation?.Hanging;
        if (hanging is not null)
            return hanging;
        StringValue styleNumberingValue = Numbering.GetStyleLevel(main, pProps)?.PreviousParagraphProperties?.Indentation?.Hanging;
        if (styleNumberingValue is null)
            return null;
        NumberingId numberingId = pProps.NumberingProperties?.NumberingId;
        if (numberingId is null)
            return styleNumberingValue;
        return (Numbering.GetNumbering(main, numberingId) is null) ? null : styleNumberingValue;
    }

    public static StringValue GetHangingIndent(MainDocumentPart main, Paragraph para) {
        ParagraphProperties pProps = para.ParagraphProperties;
        if (pProps is null)
            return null;
        return GetHangingIndent(main, pProps);
    }

    public static bool IsFlushLeft(MainDocumentPart main, ParagraphProperties pProps) {
        EnumValue<JustificationValues> just = pProps.Justification?.Val ?? JustificationValues.Left;
        if (just == JustificationValues.Center || just == JustificationValues.Right)
            return false;
        string left = GetLeftIndent(main, pProps)?.Value ?? "0";
        string hanging = GetHangingIndent(main, pProps)?.Value ?? "0";
        return left == hanging;
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

