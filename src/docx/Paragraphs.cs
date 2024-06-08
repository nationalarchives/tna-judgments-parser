
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Paragraphs {

    public static StringValue GetLeftIndentWithoutNumberingOrStyleInDXA(MainDocumentPart main, ParagraphProperties props) {
        return props?.Indentation?.Left ?? props?.Indentation?.Start;
    }
    public static float? GetLeftIndentWithoutNumberingOrStyleInInches(MainDocumentPart main, ParagraphProperties props) {
        StringValue dxa = GetLeftIndentWithoutNumberingOrStyleInDXA(main, props);
        return Util.DxaToInches(dxa);
    }

    public static StringValue GetLeftIndentWithStyleButNotNumberingInDXA(MainDocumentPart main, ParagraphProperties props) {
        if (props is null)
            return null;
        StringValue left;
        left = props.Indentation?.Left;
        if (left is not null)
            return left;
        left = props.Indentation?.Start;
        if (left is not null)
            return left;
        Style style = Styles.GetStyle(main, props);
        left = style?.GetInheritedProperty(s => s.StyleParagraphProperties?.Indentation?.Left ?? s.StyleParagraphProperties?.Indentation?.Start);
        if (left is not null)
            return left;
        style = Styles.GetDefaultParagraphStyle(main);
        left = style?.StyleParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        left = style?.StyleParagraphProperties?.Indentation?.Start;
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

        if (props.NumberingProperties?.NumberingId?.Val?.Value == 0)
            return GetLeftIndentWithStyleButNotNumberingInDXA(main, props);

        StringValue left;

        left = props.Indentation?.Left;
        if (left is not null)
            return left;
        left = props.Indentation?.Start;
        if (left is not null)
            return left;

        left = Numbering.GetOwnLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        left = Numbering.GetOwnLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Start;
        if (left is not null)
            return left;

        Style style = Styles.GetStyle(main, props);
        left = style?.GetInheritedProperty(s => s.StyleParagraphProperties?.Indentation?.Left ?? s.StyleParagraphProperties?.Indentation?.Start);
        if (left is not null)
            return left;

        left = Numbering.GetStyleLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        left = Numbering.GetStyleLevel(main, props)?.PreviousParagraphProperties?.Indentation?.Start;
        if (left is not null)
            return left;

        style = Styles.GetDefaultParagraphStyle(main);
        left = style?.StyleParagraphProperties?.Indentation?.Left;
        if (left is not null)
            return left;
        left = style?.StyleParagraphProperties?.Indentation?.Start;
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
        indentation = style?.GetInheritedProperty(s => s.StyleParagraphProperties?.Indentation);
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;
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

        if (pProps.NumberingProperties?.NumberingId?.Val?.Value == 0)
            return GetFirstLineIndentWithStyleButNotNumberingInDXA(main, pProps);

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
        indentation = style?.GetInheritedProperty(s => s.StyleParagraphProperties?.Indentation);
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

        style = Styles.GetDefaultParagraphStyle(main);
        indentation = style?.StyleParagraphProperties?.Indentation;
        if (indentation?.Hanging is not null)
            return "-" + indentation.Hanging.Value;
        if (indentation?.FirstLine is not null)
            return indentation.FirstLine.Value;

        indentation = Numbering.GetLevel(main, style)?.PreviousParagraphProperties?.Indentation;
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
        if (para.ChildElements.OfType<Run>().FirstOrDefault()?.ChildElements.Where(e => e is not RunProperties).Where(e => e is not LastRenderedPageBreak).FirstOrDefault() is TabChar)
            return false;
        if (para.ParagraphProperties is null)
            return true;
        return IsFlushLeft(main, para.ParagraphProperties);
    }

    /* tabs */

    private static List<TabStop> GetStyleTabs(MainDocumentPart main, ParagraphProperties props) {
        var styleId = props.ParagraphStyleId;
        if (styleId is null)
            return new List<TabStop>(0);
        var style = Styles.GetStyle(main, styleId);
        if (style is null)
            return new List<TabStop>(0);
        var tabs = style.StyleParagraphProperties?.Tabs;
        if (tabs is null)
            return new List<TabStop>(0);
        return tabs.ChildElements.Cast<TabStop>().ToList();
    }

    private static List<TabStop> GetOwnTabs(ParagraphProperties props) {
        var tabs = props.Tabs;
        if (tabs is null)
            return new List<TabStop>(0);
        return tabs.ChildElements.Cast<TabStop>().ToList();
    }

    private static List<TabStop> MergeTabs(List<TabStop> above, List<TabStop> below) {
        List<TabStop> merged = new List<TabStop>(above.Count + below.Count);
        foreach (var above1 in above) {
            if (above1.Val.Value == TabStopValues.Clear)
                continue;
            if (below.Any(below1 => below1.Val.Value == TabStopValues.Clear && below1.Position == above1.Position))
                continue;
            merged.Add(above1);
        }
        foreach (var below1 in below) {
            if (below1.Val.Value == TabStopValues.Clear)
                continue;
            merged.Add(below1);
        }
        return merged;
    }

    private static List<TabStop> GetTabs(MainDocumentPart main, ParagraphProperties props) {
        if (props is null)
            return new List<TabStop>(0);
        var style = GetStyleTabs(main, props);
        var own = GetOwnTabs(props);
        return MergeTabs(style, own);
    }

    private static float? TabPositionToInches(Int32Value position) {
        if (position is null)
            return null;
        try {
            return position / 1440f;
        } catch (System.FormatException) { // spec says it should always be an integer, but in some documents it's not
            return DOCX.Util.DxaToInches(position.InnerText);
        }
    }

    public static IEnumerable<float> GetTabPositions(MainDocumentPart main, ParagraphProperties props) {
        return GetTabs(main, props)
            .Select(t => TabPositionToInches(t.Position.Value))
            .Where(p => p.HasValue).Cast<float>()
            .OrderBy(p => p);
    }
    public static float? GetFirstTabAfter(MainDocumentPart main, ParagraphProperties props, float left) {
        IEnumerable<float> tabs = DOCX.Paragraphs.GetTabPositions(main, props);
        IEnumerable<float> after = tabs.Where(t => t > left);
        // can't use FirstOrDefault() b/c the default for float is 0
        if (!after.Any())
            return null;
        return after.First();
    }

    public static float? GetNumTab(MainDocumentPart main, ParagraphProperties pProps) {
        var x = GetTabs(main, pProps).Where(t => t.Val.Value == TabStopValues.Number).OrderBy(t => t.Position).FirstOrDefault();
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

    /* misc  */

    internal static bool IsEmptySectionBreak(Paragraph p) {
        return p.ChildElements.Any(child => child is ParagraphProperties pPr && pPr.SectionProperties is not null)
            && p.ChildElements.All(child => child is ParagraphProperties);
    }

    internal static bool IsEmptyPageBreak(Paragraph p) { // [2023] EWHC 3367 (Comm), test78
        if (p.ChildElements.Count != 2)
            return false;
        if (p.ChildElements[0] is not ParagraphProperties)
            return false;
        if (p.ChildElements[1] is not Run run)
            return false;
        if (run.ChildElements.Count != 1)
            return false;
        if (run .ChildElements[0] is not Break)
            return false;
        return true;
    }

    internal static bool IsDeleted(Paragraph p) {
        if (!p.ChildElements.OfType<DeletedRun>().Any())
            return false;
        return p.ChildElements.All(child => child is ParagraphProperties || child is DeletedRun);
    }

    internal static bool IsMergedWithFollowing(Paragraph p) {
        return p.ParagraphProperties?.ParagraphMarkRunProperties?.Deleted is not null;
    }

}

}
