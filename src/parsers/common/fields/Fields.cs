
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

    internal static ILogger logger = Logging.Factory.CreateLogger<Parse.Fields>();

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
        if (!e.ChildElements.All(child => child is RunProperties || child is FieldCode || child.LocalName == "instrText" || child is TabChar))  // tab in EWHC/Ch/2015/448
            throw new Exception();
        return true;
    }

    internal static string GetFieldCode(OpenXmlElement e) {
        IEnumerable<FieldCode> fieldCodes = e.ChildElements.OfType<FieldCode>();
        if (fieldCodes.Count() != 1)
            throw new Exception();
        return fieldCodes.First().InnerText;
    }

    // internal static string Normalize(string fieldCode) {
    //     return Regex.Replace(" " + fieldCode + " ", @"\s+", " ");
    // }

    // internal static string ExtractRawFieldCode(Paragraph para) {
    //     return para.Descendants<FieldCode>().Select(fc => fc.InnerText).Aggregate("", (acc, x) => acc + x);
    //     // IEnumerable<string> codes = para.Descendants<FieldCode>().Select(fc => fc.InnerText);
    //     // return string.Join("", codes);
    // }
    // internal static string ExtractAndNormalizeFieldCode(Paragraph para) {
    //     string raw = ExtractRawFieldCode(para);
    //     return Normalize(raw);
    // }

    internal static IEnumerable<IInline> ParseFieldContents(MainDocumentPart main, List<OpenXmlElement> withinField) {
        if (withinField.Count == 0)
            return Enumerable.Empty<IInline>();
        // List<IInline> parsed = new List<IInline>();
        OpenXmlElement first = withinField.First();
        if (!IsFieldCode(first)) {  // EWCA/Civ/2003/215
            logger.LogWarning("field start and end with no code");
            return Enumerable.Empty<IInline>();
        }
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
        fieldCode = DOCX.Fields.NormalizeFieldCode(fieldCode);
        logger.LogDebug("field code: " + fieldCode);
        if (string.IsNullOrWhiteSpace(fieldCode))    // [2021] EWFC 89
            return Enumerable.Empty<IInline>();
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
                RunProperties rProps = first.Ancestors<Run>().FirstOrDefault()?.RunProperties;
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
        if (fieldCode == " ref RIVATE ")    // EWHC/Admin/2015/1570
            return Enumerable.Empty<IInline>();
        if (ListNum.Is(fieldCode))
            return ListNum.Parse(main, fieldCode, withinField, i);
        if (fieldCode == " FORMTEXT " || fieldCode == " FORMTEXT _ ") {  // EWCA/Crim/2004/3049
            IEnumerable<IInline> rest = RestOptional(main, withinField, i);
            if (!rest.Any()) {
                Run previous = first.PreviousSibling<Run>();
                FieldChar begin = previous
                    .ChildElements.OfType<FieldChar>()
                    .Where(chr => chr.FieldCharType == FieldCharValues.Begin)
                    .First();
                var x = begin.FormFieldData.
                    ChildElements.Where(e => e.LocalName == "textInput").First()
                    .ChildElements.Where(e => e.LocalName == "default").FirstOrDefault();
                if (x is null) {
                    logger.LogWarning("empty form");
                } else {
                    string y = x.GetAttribute("val", "http://schemas.openxmlformats.org/wordprocessingml/2006/main").Value;
                    logger.LogWarning("empty form: " + x);
                }
            }
            return rest;
        }
        if (fieldCode.StartsWith(" MACROBUTTON")) {    // EWCA/Crim/2018/2190, [2022] EWCA Civ 1242
            logger.LogWarning("skipping MACROBUTTON field");
            return RestOptional(main, withinField, i);
        }
        if (fieldCode == " PRIVATE ") { // EWCA/Civ/2003/295
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode.StartsWith(" =SUM(ABOVE) ")) {   // EWCA/Crim/2018/542, EWHC/Ch/2015/164, EWHC/QB/2010/1112
            if (i == withinField.Count) {
                TableCell cell = first.Ancestors<TableCell>().First();
                string sum = DOCX.Tables.SumAbove(cell);
                RunProperties rProps = ((Run) first).RunProperties;
                WText wText = new WText(sum, rProps);
                return new List<IInline>(1) { wText };
            } else {
                return Rest(main, withinField, i);
            }
        }
        if (fieldCode.StartsWith(" =SUM(ABOVE)")) { // Miles v Forster - Approved Judgment - 19th January 2022 v.1.docx
            if (i == withinField.Count)
                logger.LogWarning("skipping SUM field because no alternative is provided");
            return RestOptional(main, withinField, i);
        }
        if (fieldCode == " =sum(left) ") {  // EWCA/Civ/2016/138.rtf
            if (i == withinField.Count)
                logger.LogWarning("skipping SUM field because no alternative is provided");
            return RestOptional(main, withinField, i);
        }
        if (fieldCode.StartsWith(" DATE ") || fieldCode.StartsWith(" createDATE ")) {   // EWCA/Crim/2015/558, EWCA/Civ/2018/1307
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
                    logger.LogDebug("parsed date: " + content);
                    return new List<IInline>(1) { wDate };
                } catch (FormatException) {
                    logger.LogCritical("unrecognizable date: " + content);
                }
            }
            return parsed;
        }
        if (Time.Is(fieldCode))
            return Time.Parse(main, fieldCode, withinField.Skip(i));
        if (fieldCode.StartsWith(" tc ", StringComparison.InvariantCultureIgnoreCase)) {    // EWCA/Civ/2008/875_1, EWHC/Ch/2010/3727, UTAAC/2010/V 3024 2010-00.doc
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode.StartsWith(" TOC ")) {    // EWHC/Ch/2008/219, EWHC/Admin/2021/30
            return RestOptional(main, withinField, i);  //
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
            if (!remaining.Any()) // [2020] UKSC 49, [2023] EWHC 178 (IPEC)
                logger.LogWarning($"ignoring external image { match.Groups[1].Value }");
                // note that INCLUDEPICTURE fields with the \d flag are handled by the IncludedPicture class below

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
        match = Regex.Match(fieldCode, @"^ =\d");   // EWHC/Ch/2005/2793, EWHC/Ch/2011/2301
        if (match.Success) {
            IEnumerable<IInline> rest = Rest(main, withinField, i);
            string normal = Enricher.NormalizeInlines(rest);
            logger.LogWarning("using cached value:" + fieldCode + "-> " + normal);
            return rest;
        }
        if (fieldCode.StartsWith(" PAGE ")) {   // EWHC/Admin/2003/2369, [2022] EWHC 2576 (Fam)
            string rest = ILine.TextContent(RestOptional(main, withinField, i));
            logger.LogDebug("ignoring PAGE field: " + rest);
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode.StartsWith(" NUMPAGES "))  // [2022] EWFC 125, [2022] EWHC 2794 (Fam)
            return RestOptional(main, withinField, i);
        if (fieldCode.StartsWith(" NUMWORDS "))  // [2022] EWHC 2794 (Fam)
            return RestOptional(main, withinField, i);
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
        if (fieldCode.StartsWith(" SYMBOL "))   // EWHC/Comm/2010/1735
            return new List<IInline>(1) { Symbol.Parse(main, fieldCode, withinField.Skip(i)) };
        if (fieldCode.StartsWith(" INCLUDEPICTURE ")) {   // EWCA/Civ/2004/381, EWCA/Civ/2003/1792
            IInline iPicutre = IncludedPicture.Parse(main, fieldCode, withinField.Skip(i));
            if (iPicutre is null)
                return Enumerable.Empty<IInline>();
            return new List<IInline>(1) { iPicutre };
        }
        if (fieldCode.StartsWith(" KEYWORDS ")) // EWHC/Ch/2009/1330
            return RestOptional(main, withinField, i);
        if (fieldCode.StartsWith(" SUBJECT ")) // EWHC/Ch/2009/1330
            return RestOptional(main, withinField, i);
        
        if (fieldCode.EndsWith(" FORMTEXT ")) {    // [2022] EWCA Crim 6, [2022] EWHC 2488 (Ch)
            logger.LogWarning("skipping FORMTEXT: " + fieldCode);
            return RestOptional(main, withinField, i);
        }

        // "The XE field is formatted as hidden text and displays no result in the document." https://support.microsoft.com/en-us/office/field-codes-xe-index-entry-field-abaf7c78-6e21-418d-bf8b-f8186d2e4d08
        if (fieldCode.StartsWith(" XE ")) { // [2023] EWHC 424 (TCC)
            return Enumerable.Empty<IInline>();
        }

        // https://support.microsoft.com/en-us/office/list-of-field-codes-in-word-1ad6d91a-55a7-4a8d-b535-cf7888659a51
        throw new Exception(fieldCode);
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
        // if (!rest.Skip(1).Any())
        //     throw new Exception();
        return Inline.ParseRuns(main, rest.Skip(1));
    }
    internal static IEnumerable<IInline> Rest(MainDocumentPart main, List<OpenXmlElement> withinField, int i) {
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
    internal static IEnumerable<IInline> RestOptional(MainDocumentPart main, List<OpenXmlElement> withinField, int i) {
        if (i == withinField.Count)
            return Enumerable.Empty<IInline>();
        OpenXmlElement next = withinField[i];
        if (!IsFieldSeparater(next))
            throw new Exception();
        IEnumerable<OpenXmlElement> remaining = withinField.Skip(i + 1);
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

    internal static IEnumerable<IInline> ParseSimple(MainDocumentPart main, SimpleField fldSimple) {
        logger.LogWarning("simple field: " + fldSimple.Instruction);
        if (!fldSimple.ChildElements.Any())
            logger.LogError("simple field has no child content");
        return Inline.ParseRuns(main, fldSimple.ChildElements);
    }

}

}
