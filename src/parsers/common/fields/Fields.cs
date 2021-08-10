
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments.Parse.Fieldss;

namespace UK.Gov.Legislation.Judgments.Parse {

class Fields {

    private static ILogger logger = Logging.Factory.CreateLogger<Parse.Fields>();

    internal static bool IsFieldStart(OpenXmlElement e) {
        if (e is not Run run)
            return false;
        return DOCX.Fields.IsFieldStart(run);
    }

    internal static bool IsFieldSeparater(OpenXmlElement e) {
        if (e is not Run run)
            return false;
        return DOCX.Fields.IsFieldSeparater(run);
    }

    internal static bool IsFieldEnd(OpenXmlElement e) {
        if (e is not Run run)
            return false;
        return DOCX.Fields.IsFieldEnd(run);
    }

    internal static bool IsFieldCode(OpenXmlElement e) {
        if (e is not Run)
            return false;
        if (!e.ChildElements.Any(child => child is FieldCode || child.LocalName == "instrText"))
            return false;
        if (!e.ChildElements.All(child => child is RunProperties || child is FieldCode))
            throw new Exception();
        return true;
    }

    private static string GetFieldCode(OpenXmlElement e) {
        IEnumerable<FieldCode> fieldCodes = e.ChildElements.OfType<FieldCode>();
        if (fieldCodes.Count() != 1)
            throw new Exception();
        return fieldCodes.First().InnerText;
    }

    internal static IEnumerable<IInline> ParseFieldContents(MainDocumentPart main, List<OpenXmlElement> withinField) {
        if (withinField.Count == 0)
            return Enumerable.Empty<IInline>();
        // List<IInline> parsed = new List<IInline>();
        OpenXmlElement first = withinField.First();
        if (!IsFieldCode(first))
            throw new Exception();
        string fieldCode = GetFieldCode(first);
        int i = 1;
        while (i < withinField.Count) {
            OpenXmlElement next = withinField[i];
            if (next.ChildElements.Count == 1 && next.FirstChild is RunProperties) {
                i += 1;
                continue;
            }
            if (!IsFieldCode(next))
                break;
            fieldCode += GetFieldCode(next);
            i += 1;
        }
        fieldCode = Regex.Replace(" " + fieldCode + " ", @"\s+", " ");
        logger.LogDebug("field code: " + fieldCode);
        if (Advance.Is(fieldCode))
            return Advance.Parse(main, fieldCode, withinField.Skip(i));
        Match match = Regex.Match(fieldCode, "^ FILENAME \\\\\\* MERGEFORMAT ?$");
        if (match.Success) {
            if (i == withinField.Count)
                return Enumerable.Empty<IInline>();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            return Inline.ParseRuns(main, withinField.Skip(i + 1));
        }
        match = Regex.Match(fieldCode, "^ FILLIN ");
        if (!match.Success)
            match = Regex.Match(fieldCode, "^ MERGEFIELD \"(.+?)\" $");
        if (!match.Success)
            match = Regex.Match(fieldCode, "^ MERGEFIELD \"(.+?)\" " + @"\\" + "\\* MERGEFORMAT $");
        if (match.Success) {
            if (i == withinField.Count) {
                string field = match.Groups[1].Value;
                RunProperties rProps = first.Ancestors<Run>().FirstOrDefault()?.Descendants<RunProperties>().FirstOrDefault();
                WText wText = new WText(field, rProps);
                return new List<IInline>(1) { wText };
            }
            if (i > withinField.Count - 1)
                throw new Exception();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            return Inline.ParseRuns(main, withinField.Skip(i + 1));
        }
        if (UK.Gov.Legislation.Judgments.Parse.Fieldss.Hyperlink.Is(fieldCode))
            return UK.Gov.Legislation.Judgments.Parse.Fieldss.Hyperlink.Parse(main, fieldCode, withinField, i);
        if (Ref.Is(fieldCode))
            return Ref.Parse(main, fieldCode, withinField, i);
        if (NoteRef.Is(fieldCode))
            return NoteRef.Parse(main, fieldCode, withinField, i);
        if (fieldCode == " ref PRI,ATE ")    // EWCA/Crim/2010/354
            return Enumerable.Empty<IInline>();
        if (fieldCode == " ref PRIATE ") // EWCA/Crim/2006/2899
            return Enumerable.Empty<IInline>();
        if (fieldCode == " ref PIVATE ") // EWCA/Crim/2007/1035
            return Enumerable.Empty<IInline>();
        if (fieldCode == " ref PTE ") // EWCA/Crim/2010/2144
            return Enumerable.Empty<IInline>();
        if (fieldCode == " ref PE ")    // EWCA/Crim/2011/462
            return Enumerable.Empty<IInline>();
        if (fieldCode == " ref PRVATE ")    // EWCA/Crim/2004/287
            return Enumerable.Empty<IInline>();
        if (ListNum.Is(fieldCode))
            return ListNum.Parse(main, fieldCode, withinField, i);
        // if (fieldCode == " LISTNUM LegalDefault ") {
        //     if (i < withinField.Count)
        //         throw new Exception();
        //     int n = CountPrecedingListNumLegalDefault(first) + 1;
        //     string num = n.ToString() + ".";
        //     ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
        //     RunProperties rProps = ((Run) first).RunProperties;
        //     INumber number = new DOCX.WNumber2(num, rProps, main, pProps);
        //     return new List<IInline>(1) { number };
        // }
        // // https://support.microsoft.com/en-us/office/field-codes-listnum-field-557541b1-abb2-4959-a9f2-401639c8ff82
        // match = Regex.Match(fieldCode, @"^ LISTNUM ""([A-Z0-9]+)"" \\l (\d) \\s (\d) $");
        // if (match.Success) {
        //     if (withinField.Count != 1)
        //         throw new Exception();
        //     string name = match.Groups[1].Value;
        //     int ilvl = int.Parse(match.Groups[2].Value) - 1;    // ilvl indexes are 0 based
        //     int start = int.Parse(match.Groups[3].Value);
        //     string fNum = DOCX.Numbering2.FormatNumber(name, ilvl, start, main);
        //     // ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
        //     RunProperties rProps = ((Run) first).RunProperties;
        //     // INumber number = new DOCX.WNumber2(fNum, rProps, main, pProps);
        //     /* not sure why this should be a WText and LISTNUM LegalDefault should be an WNumber2
        //     /* but only example I've seen (EWCA/Crim/2011/143) is followed by a non-breaking space */
        //     WText wText = new WText(fNum, rProps);
        //     return new List<IInline>(1) { wText };
        // }
        if (fieldCode == " FORMTEXT " || fieldCode == " FORMTEXT _ ") {  // EWCA/Crim/2004/3049
            if (i == withinField.Count)
                return Enumerable.Empty<IInline>();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            var rest = withinField.Skip(i + 1);
            if (!rest.Any())
                throw new Exception();
            return Inline.ParseRuns(main, rest);
        }
        if (fieldCode.StartsWith(" MACROBUTTON ")) {    // EWCA/Crim/2018/2190
            if (i != withinField.Count)
                throw new Exception();
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode == " PRIVATE ") { // EWCA/Civ/2003/295
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode == " =SUM(ABOVE) ") {   // EWCA/Crim/2018/542, EWHC/Ch/2015/164
            if (i == withinField.Count) {
                TableCell cell = first.Ancestors<TableCell>().First();
                string sum = DOCX.Tables.SumAbove(cell);
                RunProperties rProps = ((Run) first).RunProperties;
                WText wText = new WText(sum, rProps);
                return new List<IInline>(1) { wText };
            } else {
                OpenXmlElement next = withinField[i];
                if (!IsFieldSeparater(next))
                    throw new Exception();
                IEnumerable<OpenXmlElement> rest = withinField.Skip(i + 1);
                if (!rest.Any())
                    throw new Exception();
                return Inline.ParseRuns(main, rest);
            }
        }
        if (fieldCode == " DATE \\@ \"dd MMMM yyyy\" ") {
            if (i == withinField.Count)
                throw new Exception();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            IEnumerable<OpenXmlElement> rest = withinField.Skip(i + 1);
            if (!rest.Any())
                throw new Exception();
            IEnumerable<IInline> parsed = Inline.ParseRuns(main, rest);
            if (parsed.All(inline => inline is IFormattedText)) {
                string content = Enricher.NormalizeInlines(parsed);
                try {
                    CultureInfo culture = new CultureInfo("en-GB");
                    DateTime date = DateTime.Parse(content, culture);
                    WDate wDate = new WDate(parsed.Cast<IFormattedText>(), date);
                    return new List<IInline>(1) { wDate };
                } catch (FormatException) {
                }
            }
            return parsed;
        }
        if (Time.Is(fieldCode))
            return Time.Parse(main, fieldCode, withinField.Skip(i));
        // if (fieldCode == " TIME \\@ \"dddd d MMMM yyyy\" ") { // EWHC/Ch/2005/2793, EWCA/Civ/2006/1103
        //     if (i == withinField.Count)
        //         throw new Exception();
        //     OpenXmlElement next = withinField[i];
        //     if (!IsFieldSeparater(next))
        //         throw new Exception();
        //     IEnumerable<OpenXmlElement> rest = withinField.Skip(i + 1);
        //     if (!rest.Any())
        //         throw new Exception();
        //     IEnumerable<IInline> parsed = Inline.ParseRuns(main, rest);
        //     if (parsed.All(inline => inline is IFormattedText)) {
        //         string content = Enricher.NormalizeInlines(parsed);
        //         try {
        //             CultureInfo culture = new CultureInfo("en-GB");
        //             DateTime date = DateTime.Parse(content, culture);
        //             WDate wDate = new WDate(parsed.Cast<IFormattedText>(), date);
        //             return new List<IInline>(1) { wDate };
        //         } catch (FormatException) {
        //         }
        //     }
        //     return parsed;
        // }
        if (fieldCode.StartsWith(" tc \"")) {    // EWCA/Civ/2008/875_1
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode.StartsWith(" TOC ")) {    // EWHC/Ch/2008/219
            return Enumerable.Empty<IInline>();
        }
        match = Regex.Match(fieldCode, @"^ INCLUDEPICTURE ""(.+?)"" \\\* MERGEFORMATINET $");   // EWHC/Patents/2008/2127
        if (match.Success) {
            if (i == withinField.Count) {
                string url = match.Groups[1].Value;
                WExternalImage image = new WExternalImage() { URL = url };
                return new List<IInline>(1) { image };
            }
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            IEnumerable<OpenXmlElement> remaining = withinField.Skip(i + 1);
            if (!remaining.Any())
                throw new Exception();
            return Inline.ParseRuns(main, remaining);
        }
        if (Seq.Is(fieldCode))
            return Seq.Parse(main, fieldCode, withinField.Skip(1));
        if (fieldCode == " AUTONUM ")  // EWHC/Ch/2005/2793
            return new List<IInline>(1) { Autonum(main, (Run) first) };
        string regex = @"^ AUTONUM +\\\* Arabic *$";
        match = Regex.Match(fieldCode, regex);   // EWHC/Ch/2007/2841
        if (match.Success) {
            return new List<IInline>(1) { Autonum(main, (Run) first, regex) };
        }
        if (fieldCode == " =179000*0.3 \\# \"£#,##0;(£#,##0)\" ")   // EWHC/Ch/2005/2793
            return Rest(main, withinField, i);
        
        if (fieldCode == " PAGE ") {   // EWHC/Admin/2003/2369
            if (!first.Ancestors<Header>().Any())
                throw new Exception();
            return Enumerable.Empty<IInline>();
        }
        match = Regex.Match(fieldCode, @" PAGEREF [_A-Za-z0-9]+ \\h ");   // EWHC/Ch/2007/1044
        if (match.Success) {
            return Enumerable.Empty<IInline>();
        }

        if (fieldCode == " QUOTE ") {  // EWHC/Comm/2013/2118
            while (i < withinField.Count) {
                OpenXmlElement next = withinField[i];
                i += 1;
                if (IsFieldSeparater(next))
                    break;
            }
            if (i == withinField.Count)
                logger.LogError("no separator after QUOTE field");
            IEnumerable<OpenXmlElement> remaining = withinField.Skip(i);
            return Inline.ParseRuns(main, remaining);
        }

        match = Regex.Match(fieldCode, @" ASK [_A-Za-z0-9]+ ""[^""]+"" \\\* MERGEFORMAT $"); // EWHC/Admin/2020/287
        if (match.Success)
            return Rest(main, withinField, i);
        if (fieldCode == " ADDIN CiteCheck Marker ")    // EWHC/Comm/2012/3586
            return RestOptional(main, withinField, i);
        if (fieldCode.StartsWith(" TA "))   // EWHC/Comm/2005/735
            return Enumerable.Empty<IInline>();

        // https://support.microsoft.com/en-us/office/list-of-field-codes-in-word-1ad6d91a-55a7-4a8d-b535-cf7888659a51
        throw new Exception();
    }

    internal static IEnumerable<IInline> Rest(MainDocumentPart main, IEnumerable<OpenXmlElement> rest) {
        if (!rest.Any())
            throw new Exception();
        OpenXmlElement first = rest.First();
        if (!IsFieldSeparater(first))
            throw new Exception();
        if (!rest.Skip(1).Any())
            throw new Exception();
        return Inline.ParseRuns(main, rest.Skip(1));
    }
    internal static IEnumerable<IInline> RestOptional(MainDocumentPart main, IEnumerable<OpenXmlElement> rest) {
        if (!rest.Any()) {
            logger.LogWarning("field code with no text content");
            return Enumerable.Empty<IInline>();
        }
        OpenXmlElement first = rest.First();
        if (!IsFieldSeparater(first))
            throw new Exception();
        if (!rest.Skip(1).Any())
            throw new Exception();
        return Inline.ParseRuns(main, rest.Skip(1));
    }
    private static IEnumerable<IInline> Rest(MainDocumentPart main, List<OpenXmlElement> withinField, int i) {
            if (i == withinField.Count)
                throw new Exception();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            IEnumerable<OpenXmlElement> remaining = withinField.Skip(i + 1);
            if (!remaining.Any())
                throw new Exception();
            return Inline.ParseRuns(main, remaining);
    }
    private static IEnumerable<IInline> RestOptional(MainDocumentPart main, List<OpenXmlElement> withinField, int i) {
            if (i == withinField.Count)
                return Enumerable.Empty<IInline>();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            IEnumerable<OpenXmlElement> remaining = withinField.Skip(i + 1);
            if (!remaining.Any())
                throw new Exception();
            return Inline.ParseRuns(main, remaining);
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

    internal static INumber RemoveListNum(WLine line) {
        if (line.Contents.Count() == 0)
            return null;
        IInline first = line.Contents.First();
        if (first is INumber firstNumber) {
            line.Contents = line.Contents.Skip(1);
            if (line.Contents.First() is WTab)
                line.Contents = line.Contents.Skip(1);
            return firstNumber;
        }
        return null;

    }

    /* AUTONUM */
    /* incomplete: will not format correctly */
    private static INumber Autonum(MainDocumentPart main, Run run) {  // EWHC/Ch/2005/2793
        Paragraph paragraph = run.Ancestors<Paragraph>().First();
        int n = 1;
        Paragraph preceding = paragraph.PreviousSibling<Paragraph>();
        while (preceding is not null) {
            if (preceding.Descendants().Where(e => e is FieldCode || e.LocalName == "instrText").Where(e => e.InnerText.Trim() == "AUTONUM").Any())
                n += 1;
            preceding = preceding.PreviousSibling<Paragraph>();
        }
        return new DOCX.WNumber2(n.ToString() + ".", run.RunProperties, main, paragraph.ParagraphProperties);
    }
    private static INumber Autonum(MainDocumentPart main, Run run, string regex) {  // EWHC/Ch/2007/2841
        Paragraph paragraph = run.Ancestors<Paragraph>().First();
        int n = 1;
        Paragraph preceding = paragraph.PreviousSibling<Paragraph>();
        while (preceding is not null) {
            if (preceding.Descendants().Where(e => e is FieldCode || e.LocalName == "instrText").Where(e => Regex.IsMatch(e.InnerText, regex)).Any())
                n += 1;
            preceding = preceding.PreviousSibling<Paragraph>();
        }
        return new DOCX.WNumber2(n.ToString() + ".", run.RunProperties, main, paragraph.ParagraphProperties);
    }


}

}
