
using System;
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

    internal static INumber Parse(MainDocumentPart main, Run first, string fieldCode) {
        if (fieldCode == " LISTNUM LegalDefault ") {
            Paragraph p = first.Ancestors<Paragraph>().First();
            int n = CountPrecedingParagraphsWithListNumLegalDefault(p, 1) + 1;
            string num = n.ToString() + ".";
            ParagraphProperties pProps = p.ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(num, rProps, main, pProps);
        }
        Match match;
        match = Regex.Match(fieldCode, @"^ LISTNUM LegalDefault \\l (\d) $");    // EWCA/Civ/2015/325, [2022] EWHC 3114 (Admin) [test 49]
        if (match.Success) {
            int l = int.Parse(match.Groups[1].Value);
            Paragraph p = first.Ancestors<Paragraph>().First();
            int n = CountPrecedingParagraphsWithListNumLegalDefault(p, l) + 1;
            string num = n.ToString() + ".";
            for (int lAbove = l - 1; lAbove > 0; lAbove--) { // [2022] EWHC 3114 (Admin)
                int nAbove = CountPrecedingParagraphsWithListNumLegalDefault(p, lAbove) + 1;
                num = nAbove.ToString() + "." + num;
            }
            ParagraphProperties pProps = p.ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(num, rProps, main, pProps);
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM LegalDefault \\s (\d) $");    // EWHC/Admin/2012/3751
        if (match.Success) {
            int start = int.Parse(match.Groups[1].Value);
            int n = CountPreceding(first, fieldCode) + start;
            string num = n.ToString() + ".";
            ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(num, rProps, main, pProps);
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM (\d+) \\l (\d) $");    // EWHC/Ch/2011/3553 (test 12)
        if (match.Success) {
            int numId = int.Parse(match.Groups[1].Value);
            int ilvl = int.Parse(match.Groups[2].Value) - 1;    // ilvl indexes are 0 based
            Paragraph p = first.Ancestors<Paragraph>().First();

            int n = DOCX.Numbering2.CalculateN(main, p, ilvl);

            string fNum = DOCX.Numbering2.FormatNumber(numId, ilvl, n, main);
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(fNum, rProps, main, p.ParagraphProperties);
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM \\l (\d) $");    // EWHC/Patents/2013/2927
        if (match.Success) {
            Paragraph p = first.Ancestors<Paragraph>().First();
            int numId = p.ParagraphProperties.NumberingProperties.NumberingId.Val.Value;
            int ilvl = int.Parse(match.Groups[1].Value) - 1;    // ilvl indexes are 0 based
            string fNum = DOCX.Numbering2.FormatNumber(numId, ilvl, 1, main);
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(fNum, rProps, main, p.ParagraphProperties);
        }
        match = Regex.Match(fieldCode, @"^ listnum ""WP List \d"" \\l (\d) $");  // EWHC/Ch/2004/1835, [2022] EWHC 3185 (Admin)
        if (match.Success) {
            int l = int.Parse(match.Groups[1].Value);
            int n = CountPreceding(first, fieldCode) + 1;
            string fNum = FormatNNumberDefault(l, n);
            ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(fNum, rProps, main, pProps);
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM ""SEQ1"" \\l (\d) \\s (\d) $");  // [2022] EWHC 3114 (Admin) [test 49]
        if (match.Success) {
            int l = int.Parse(match.Groups[1].Value);
            int s = int.Parse(match.Groups[2].Value);
            int n = CountPreceding(first, fieldCode) + s;
            string fNum = FormatNNumberDefault(l, n);
            ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(fNum, rProps, main, pProps);
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM ""([^""]+)"" \\l (\d) \\s (\d) $");
        if (match.Success) {
            string name = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;    // ilvl indexes are 0 based
            int start = int.Parse(match.Groups[3].Value);
            string fNum = DOCX.Numbering2.FormatNumber(name, ilvl, start, main);
            ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(fNum, rProps, main, pProps);
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM ""([^""]+)"" \\l (\d) $");   // EWCA/Civ/2008/1365
        /* can be combined with previous pattern? */
        if (match.Success) {
            string name = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;    // ilvl indexes are 0 based
            int start = 1;
            string fNum = DOCX.Numbering2.FormatNumber(name, ilvl, start, main);
            ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            return new DOCX.WNumber2(fNum, rProps, main, pProps);
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

    private static int CountPrecedingParagraphsWithListNumLegalDefault(Paragraph p, int l) {
        string test = " LISTNUM LegalDefault \\l " + l + " ";
        Func<Paragraph, bool> predicate = (p) => {
            string fc = DOCX.Fields.ExtractAndNormalizeFieldCode(p);
            if (fc == test)
                return true;
            if (l == 1 && fc == " LISTNUM LegalDefault ")
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

    private static string FormatNNumberDefault(int l, int n) {
        if (l == 1)
            return n + ")";
        if (l == 2)
            return DOCX.Util.ToLowerLetter(n) + ")";
        if (l == 3)
            return DOCX.Util.ToLowerRoman(n) + ")";
        if (l == 4)
            return "(" + n + ")";
        if (l == 5)
            return "(" + DOCX.Util.ToLowerLetter(n) + ")";
        if (l == 6)
            return "(" + DOCX.Util.ToLowerRoman(n) + ")";
        if (l == 7)
            return n + ".";
        if (l == 8)
            return DOCX.Util.ToLowerLetter(n) + ".";
        if (l == 9)
            return DOCX.Util.ToLowerRoman(n) + ".";
        throw new Exception();
    }

    private static string FormatNOutlineDefault(int l, int n) {
        if (l == 1)
            return DOCX.Util.ToUpperRoman(n) + ".";
        if (l == 2)
            return DOCX.Util.ToUpperLetter(n) + ".";
        if (l == 3)
            return n + ".";
        if (l == 4)
            return DOCX.Util.ToLowerLetter(n) + ")";
        if (l == 5)
            return "(" + n + ")";
        if (l == 6)
            return "(" + DOCX.Util.ToLowerLetter(n) + ")";
        if (l == 7)
            return "(" + DOCX.Util.ToLowerRoman(n) + ")";
        if (l == 8)
            return "(" + DOCX.Util.ToLowerLetter(n) + ")";
        if (l == 9)
            return "(" + DOCX.Util.ToLowerRoman(n) + ")";
        throw new Exception();
    }

}

}
