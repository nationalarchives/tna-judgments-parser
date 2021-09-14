
using System;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Fields {

    internal static bool IsFieldStart(Run run) {
        if (!run.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Begin)))
            return false;
        if (!run.ChildElements.All(child => child is RunProperties || child is LastRenderedPageBreak || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Begin))))
            throw new Exception();
        return true;
    }

    internal static bool IsFieldSeparater(Run run) {
        if (!run.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Separate)))
            return false;
        if (!run.ChildElements.All(child => child is RunProperties || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Separate))))
            throw new Exception();
        return true;
    }

    internal static bool IsFieldEnd(Run run) {
        if (!run.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.End)))
            return false;
        if (!run.ChildElements.All(child => child is RunProperties || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.End))))
            throw new Exception();
        return true;
    }

    private static Paragraph CloneAndRemoveFieldCodes(this Paragraph p) {
        Paragraph p2 = (Paragraph) p.CloneNode(true);
        foreach (FieldCode fc in p2.Descendants().Where(e => e.LocalName == "instrText"))
            fc.Remove();
        return p2;
    }

    public static string ExtractInnerTextExcludingFieldCodes(Paragraph p) {
        return p.CloneAndRemoveFieldCodes().InnerText;
    }

    internal static string NormalizeFieldCode(string fieldCode) {
        return Regex.Replace(" " + fieldCode + " ", @"\s+", " ");
    }
    internal static string ExtractRawFieldCode(Paragraph para) {
        return para.Descendants<FieldCode>().Select(fc => fc.InnerText).Aggregate("", (acc, x) => acc + x);
    }
    internal static string ExtractAndNormalizeFieldCode(Paragraph para) {
        string raw = ExtractRawFieldCode(para);
        return NormalizeFieldCode(raw);
    }

    internal static int CountPrecedingParagraphs(Paragraph p, Func<Paragraph, bool> predicate) {
        int count = 0;
        Paragraph previous = p.PreviousSibling<Paragraph>();
        while (previous is not null) {
            string fieldCode = Fields.ExtractAndNormalizeFieldCode(previous);
            if (predicate(previous))
                count += 1;
            previous = previous.PreviousSibling<Paragraph>();
        }
        return count;
    }

    // internal static int CountPrecedingParagraphsWithListNumLegalDefault(Paragraph p) {
    //     Func<Paragraph, bool> predicate = (p) => {
    //         string fc = Fields.ExtractAndNormalizeFieldCode(p);
    //         if (fc == " LISTNUM LegalDefault ")
    //             return true;
    //         if (fc == " LISTNUM LegalDefault \\l 1 ")
    //             return true;
    //         return false;
    //     };
    //     return CountPrecedingParagraphs(p, predicate);
    // }

    internal static int CountPrecedingParagraphsWithListNum(int numId, int ilvl, Paragraph p) {
        string needle = @" LISTNUM " + numId + @" \l " + (ilvl + 1);
        Func<Paragraph, bool> predicate = p => Fields.ExtractAndNormalizeFieldCode(p).StartsWith(needle);
        return CountPrecedingParagraphs(p, predicate);
    }

}

}
