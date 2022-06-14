
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse.Fieldss {

internal class ListNum {

// https://support.microsoft.com/en-us/office/field-codes-listnum-field-557541b1-abb2-4959-a9f2-401639c8ff82

    // private static string regex = @"^ HYPERLINK ""([^""]+)""( \\l ""([^""]+)"")?( \\o ""([^""]*)"")?( \\t ""([^""]*)"")? $";

    internal static bool Is(string fieldCode) {
        return fieldCode.StartsWith(" LISTNUM ", StringComparison.InvariantCultureIgnoreCase);
    }

    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, List<OpenXmlElement> withinField, int i) {
        if (i < withinField.Count)
            throw new Exception();
        Run first = (Run) withinField.First();
        if (fieldCode == " LISTNUM LegalDefault ") {
            Paragraph p = first.Ancestors<Paragraph>().First();
            int n = CountPrecedingParagraphsWithListNumLegalDefault(p) + 1;
            string num = n.ToString() + ".";
            ParagraphProperties pProps = p.ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            INumber number = new DOCX.WNumber2(num, rProps, main, pProps);
            return new List<IInline>(1) { number };
        }
        Match match;
        match = Regex.Match(fieldCode, @"^ LISTNUM LegalDefault \\l 1 $");    // EWCA/Civ/2015/325
        if (match.Success) {
            Paragraph p = first.Ancestors<Paragraph>().First();
            int n = CountPrecedingParagraphsWithListNumLegalDefault(p) + 1;
            string num = n.ToString() + ".";
            ParagraphProperties pProps = p.ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            INumber number = new DOCX.WNumber2(num, rProps, main, pProps);
            return new List<IInline>(1) { number };
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM LegalDefault \\s (\d) $");    // EWHC/Admin/2012/3751
        if (match.Success) {
            int start = int.Parse(match.Groups[1].Value);
            int n = CountPreceding(first, fieldCode) + start;
            string num = n.ToString() + ".";
            ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            INumber number = new DOCX.WNumber2(num, rProps, main, pProps);
            return new List<IInline>(1) { number };
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM (\d) \\l (\d) $");    // EWHC/Ch/2011/3553
        if (match.Success) {
            int numId = int.Parse(match.Groups[1].Value);
            int ilvl = int.Parse(match.Groups[2].Value) - 1;    // ilvl indexes are 0 based
            Paragraph p = first.Ancestors<Paragraph>().First();
            int n = DOCX.Fields.CountPrecedingParagraphsWithListNum(numId, ilvl, p) + 1;

            NumberingInstance numbering = DOCX.Numbering.GetNumbering(main, numId);
            AbstractNum absNum = DOCX.Numbering.GetAbstractNum(main, numbering);
            n += DOCX.Numbering2.CalculateN(main, p, numId, absNum.AbstractNumberId.Value, ilvl) - 1;

            string fNum = DOCX.Numbering2.FormatNumber(numId, ilvl, n, main);
            RunProperties rProps = first.RunProperties;
            WText wText = new WText(fNum, rProps);
            return new List<IInline>(1) { wText };
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM \\l (\d) $");    // EWHC/Patents/2013/2927
        if (match.Success) {
            int numId = first.Ancestors<Paragraph>().First().ParagraphProperties.NumberingProperties.NumberingId.Val.Value;
            int ilvl = int.Parse(match.Groups[1].Value) - 1;    // ilvl indexes are 0 based
            string fNum = DOCX.Numbering2.FormatNumber(numId, ilvl, 1, main);
            RunProperties rProps = first.RunProperties;
            WText wText = new WText(fNum, rProps);
            return new List<IInline>(1) { wText };
        }
        match = Regex.Match(fieldCode, @"^ listnum ""WP List 1"" \\l (\d) $");  // EWHC/Ch/2004/1835
        if (match.Success) {
            int absNumId = 0;
            int ilvl = int.Parse(match.Groups[1].Value) - 1;
            int n = CountPreceding(withinField.First(), fieldCode) + 1;
            string fNum = DOCX.Numbering2.FormatNumberAbstract(absNumId, ilvl, n, main);
            RunProperties rProps = first.RunProperties;
            WText wText = new WText(fNum, rProps);
            return new List<IInline>(1) { wText };
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM ""([^""]+)"" \\l (\d) \\s (\d) $");
        if (match.Success) {
            string name = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;    // ilvl indexes are 0 based
            int start = int.Parse(match.Groups[3].Value);
            string fNum = DOCX.Numbering2.FormatNumber(name, ilvl, start, main);
            // ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            // INumber number = new DOCX.WNumber2(fNum, rProps, main, pProps);
            /* not sure why this should be a WText and LISTNUM LegalDefault should be an WNumber2
            /* but only example I've seen (EWCA/Crim/2011/143) is followed by a non-breaking space */
            WText wText = new WText(fNum, rProps);
            return new List<IInline>(1) { wText };
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM ""([^""]+)"" \\l (\d) $");   // EWCA/Civ/2008/1365
        /* can be combined with previous pattern? */
        if (match.Success) {
            string name = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;    // ilvl indexes are 0 based
            int start = 1;
            string fNum = DOCX.Numbering2.FormatNumber(name, ilvl, start, main);
            RunProperties rProps = first.RunProperties;
            WText wText = new WText(fNum, rProps);
            return new List<IInline>(1) { wText };
        }
        throw new Exception(fieldCode);
    }

    private static int CountPreceding(OpenXmlElement anchor, string fieldCode) {
        Paragraph p = anchor.Ancestors<Paragraph>().First();
        Func<Paragraph, bool> predicate = (p) => {
            return DOCX.Fields.ExtractAndNormalizeFieldCode(p) == fieldCode;
        };
        return DOCX.Fields.CountPrecedingParagraphs(p, predicate);
    //     int count = 0;
    //     Paragraph previous = anchor.Ancestors<Paragraph>().First().PreviousSibling<Paragraph>();
    //     while (previous != null) {
    //         string fc = Fields.ExtractAndNormalizeFieldCode(previous);
    //         if (fieldCode == fc)
    //             count += 1;
    //         previous = previous.PreviousSibling<Paragraph>();
    //     }
    //     return count;
    }

    private static int CountPrecedingParagraphsWithListNumLegalDefault(Paragraph p) {
        Func<Paragraph, bool> predicate = (p) => {
            string fc = DOCX.Fields.ExtractAndNormalizeFieldCode(p);
            if (fc == " LISTNUM LegalDefault ")
                return true;
            if (fc == " LISTNUM LegalDefault \\l 1 ")
                return true;
            return false;
        };
        return DOCX.Fields.CountPrecedingParagraphs(p, predicate);
    //     int count = 0;
    //     Paragraph previous = fc.Ancestors<Paragraph>().First().PreviousSibling<Paragraph>();
    //     Func<FieldCode, bool> f1 = (fc) => {
    //         string normal = Fields.Normalize(fc.InnerText);
    //         if (normal == " LISTNUM LegalDefault ")
    //             return true;
    //         if (normal == " LISTNUM LegalDefault \\l 1 ")
    //             return true;
    //         return false;
    //     };
    //     Func<Paragraph, bool> predicate = (p) => {
    // //         return p.Descendants<FieldCode>().Where(f1).Any();
    //     };
    //     while (previous != null) {
    //         if (f2(previous))
    //             count += 1;
    //         previous = previous.PreviousSibling<Paragraph>();
    //     }
    //     return count;
    }

    // private static int CountPrecedingWithListNum(int numId, int ilvl, Paragraph p) {
    //     string needle = @" LISTNUM " + numId + @" \l " + (ilvl + 1);
    //     int count = 0;
    //     Paragraph previous = p.PreviousSibling<Paragraph>();
    //     while (previous is not null) {
    //         string fieldCode = Fields.ExtractAndNormalizeFieldCode(previous);
    //         if (fieldCode.StartsWith(needle))
    //             count += 1;
    //         previous = previous.PreviousSibling<Paragraph>();
    //     }
    //     return count;
    // }

}

}
