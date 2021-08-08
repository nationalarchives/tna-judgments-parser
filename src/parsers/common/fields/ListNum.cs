
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
        return fieldCode.StartsWith(" LISTNUM ");
    }

    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, List<OpenXmlElement> withinField, int i) {
        if (i < withinField.Count)
            throw new Exception();
        Run first = (Run) withinField.First();
        if (fieldCode == " LISTNUM LegalDefault ") {
            // if (i < withinField.Count)
            //     throw new Exception();
            int n = CountPrecedingListNumLegalDefault(first) + 1;
            string num = n.ToString() + ".";
            ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = first.RunProperties;
            INumber number = new DOCX.WNumber2(num, rProps, main, pProps);
            return new List<IInline>(1) { number };
        }
        Match match;
        match = Regex.Match(fieldCode, @"^ LISTNUM \\l (\d) $");    // EWHC/Patents/2013/2927
        if (match.Success) {
            int numId = first.Ancestors<Paragraph>().First().ParagraphProperties.NumberingProperties.NumberingId.Val.Value;
            int ilvl = int.Parse(match.Groups[1].Value) - 1;    // ilvl indexes are 0 based
            string fNum = DOCX.Numbering2.FormatNumber(numId, ilvl, 1, main);
            RunProperties rProps = first.RunProperties;
            WText wText = new WText(fNum, rProps);
            return new List<IInline>(1) { wText };
        }
        match = Regex.Match(fieldCode, @"^ LISTNUM ""([A-Z0-9]+)"" \\l (\d) \\s (\d) $");
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
        throw new Exception(fieldCode);
    }

    private static int CountPrecedingListNumLegalDefault(OpenXmlElement fc) {
        int count = 0;
        Paragraph previous = fc.Ancestors<Paragraph>().First().PreviousSibling<Paragraph>();
        while (previous != null) {
            FieldCode listNum = previous.Descendants<FieldCode>().Where(fc => fc.InnerText.Trim() == "LISTNUM LegalDefault").FirstOrDefault();
            if (listNum is not null)
                count += 1;
            previous = previous.PreviousSibling<Paragraph>();
        }
        return count;
    }

}

}
