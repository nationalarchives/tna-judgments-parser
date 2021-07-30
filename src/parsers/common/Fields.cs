
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;


namespace UK.Gov.Legislation.Judgments.Parse {

class Fields {

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
            match = Regex.Match(fieldCode, "^ MERGEFIELD \"(.+?)\" ?$");
        if (!match.Success)
            match = Regex.Match(fieldCode, "^ MERGEFIELD \"(.+?)\" " + @"\\" + "\\* MERGEFORMAT ?$");
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
        match = Regex.Match(fieldCode, "^ HYPERLINK \"(.+?)\" ?$");
        if (match.Success) {
            // if (i != withinField.Count - 2)
            //     throw new Exception();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            IEnumerable<OpenXmlElement> rest = withinField.Skip(i + 1);
            IEnumerable<IInline> contents = Inline.ParseRuns(main, rest);
            string href = match.Groups[1].Value;
            WHyperlink2 hyperlink = new WHyperlink2() { Contents = contents, Href = href };
            // OpenXmlElement last = withinField[i+1];
            // if (last is not Run run)
            //     throw new Exception();
            // IEnumerable<IInline> children = Inline.MapRunChildren(main, run);
            // if (children.Count() != 1)
            //     throw new Exception();
            // IInline firstChild = children.First();
            // if (firstChild is not WText wt)
            //     throw new Exception();
            // string href = match.Groups[1].Value;
            // WHyperlink1 hyperlink = new WHyperlink1(wt) { Href = href };
            return new List<IInline>(1) { hyperlink };
        }
        match = Regex.Match(fieldCode, "^ HYPERLINK \"(.+?)\" " + @"\\o" +" \"(.+?)\" ?$");
        if (match.Success) {
            if (i != withinField.Count)
                throw new Exception();
            string address = match.Groups[1].Value;
            string text = match.Groups[2].Value;
            RunProperties rProps = first.Ancestors<Run>().FirstOrDefault().Descendants<RunProperties>().FirstOrDefault();
            WText wText = new WText(text, rProps);
            WHyperlink1 hyperlink = new WHyperlink1(wText) { Href = address };
            return new List<IInline>(1) { hyperlink };
        }
        match = Regex.Match(fieldCode, @"^ REF ([_A-Za-z0-9]+) \\r( \\p)? \\h(  \\\* MERGEFORMAT)? $");
        // no \\h in EWCA/Civ/2009/755
        if (match.Success) {
            string rf = match.Groups[1].Value;
            OpenXmlElement root = first;
            while (root.Parent is not null)
                root = root.Parent;
            BookmarkStart bkmk = DOCX.Bookmarks.Get(main, rf);
            if (bkmk is null)
                throw new Exception();
            Paragraph bkmkPara = bkmk.Ancestors<Paragraph>().First();
            DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, bkmkPara);
            string aboveBelow = "";
            if (!string.IsNullOrEmpty(match.Groups[2].Value)) {
                bool above = true;
                Paragraph nextPara = first.Ancestors<Paragraph>().First().NextSibling<Paragraph>();
                while (nextPara is not null) {
                    if (nextPara == bkmkPara)
                        above = false;
                    nextPara = nextPara.NextSibling<Paragraph>();
                }
                aboveBelow = above ? " above" : " below";
            }
            if (i == withinField.Count) {
                if (info is null)
                    throw new Exception("REF has no content and target has no number");
                RunProperties rProps = first is Run run ? run.RunProperties : null;
                string numWithoutPunc = info.Value.Number.Trim('.', '(',')');
                string numPlusAboveBelow = numWithoutPunc + aboveBelow;
                WText numberInThisFormat = new WText(numPlusAboveBelow, rProps);
                return new List<IInline>(1) { numberInThisFormat };
            } else {
                OpenXmlElement next = withinField[i];
                // while (next.ChildElements.Count == 1 && next.FirstChild is RunProperties) {
                //     i += 1;
                //     next = withinField[i];
                // }
                if (!IsFieldSeparater(next))
                    throw new Exception();
                if (info is null) {
                    IEnumerable<OpenXmlElement> remaining = withinField.Skip(i + 1);
                    return Inline.ParseRuns(main, remaining);
                } else {
                    RunProperties rProps = first is Run run ? run.RunProperties : null;
                    string numWithoutPunc = info.Value.Number.Trim('.', '(',')');
                    string numPlusAboveBelow = numWithoutPunc + aboveBelow;
                    WText numberInThisFormat = new WText(numPlusAboveBelow, rProps);
                    return new List<IInline>(1) { numberInThisFormat };
                }
            }
        }
        if (fieldCode == "ref PRI,ATE ") { // EWCA/Crim/2010/354
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode == "ref PRIATE ") { // EWCA/Crim/2006/2899
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode == " LISTNUM LegalDefault ") {
            if (i < withinField.Count)
                throw new Exception();
            int n = CountPrecedingListNumLegalDefault(first) + 1;
            string num = n.ToString() + ".";
            ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = ((Run) first).RunProperties;
            INumber number = new DOCX.WNumber2(num, rProps, main, pProps);
            return new List<IInline>(1) { number };
        }
        // https://support.microsoft.com/en-us/office/field-codes-listnum-field-557541b1-abb2-4959-a9f2-401639c8ff82
        match = Regex.Match(fieldCode, @"^ LISTNUM ""([A-Z0-9]+)"" \\l (\d) \\s (\d) $");
        if (match.Success) {
            if (withinField.Count != 1)
                throw new Exception();
            string name = match.Groups[1].Value;
            int ilvl = int.Parse(match.Groups[2].Value) - 1;    // ilvl indexes are 0 based
            int start = int.Parse(match.Groups[3].Value);
            string fNum = DOCX.Numbering2.FormatNumber(name, ilvl, start, main);
            // ParagraphProperties pProps = first.Ancestors<Paragraph>().First().ParagraphProperties;
            RunProperties rProps = ((Run) first).RunProperties;
            // INumber number = new DOCX.WNumber2(fNum, rProps, main, pProps);
            /* not sure why this should be a WText and LISTNUM LegalDefault should be an WNumber2
            /*  but only example I've seen (EWCA/Crim/2011/143) is followed by a non-breaking space */
            WText wText = new WText(fNum, rProps);
            return new List<IInline>(1) { wText };
        }
        if (fieldCode == " FORMTEXT " || fieldCode == " FORMTEXT _") {  // EWCA/Crim/2004/3049
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
        if (fieldCode == "PRIVATE ") { // EWCA/Civ/2003/295
            return Enumerable.Empty<IInline>();
        }
        if (fieldCode == " =SUM(ABOVE) ") {   // EWCA/Crim/2018/542
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            IEnumerable<OpenXmlElement> rest = withinField.Skip(i + 1);
            if (!rest.Any())
                throw new Exception();
            return Inline.ParseRuns(main, rest);
        }
        if (fieldCode == " DATE \\@ \"dd MMMM yyyy\" ") {
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
        if (fieldCode.StartsWith("tc \"")) {    // EWCA/Civ/2008/875_1
            return Enumerable.Empty<IInline>();
        }
        match = Regex.Match(fieldCode, @"^ INCLUDEPICTURE ""(.+?)"" \\\* MERGEFORMATINET $");   // EWHC/Patents/2008/2127
        if (match.Success) {
            throw new Exception("check to see if an image follows the separator");
            string url = match.Groups[1].Value;
            WExternalImage image = new WExternalImage() { URL = url };
            return new List<IInline>(1) { image };
        }
        // https://support.microsoft.com/en-us/office/list-of-field-codes-in-word-1ad6d91a-55a7-4a8d-b535-cf7888659a51
        throw new Exception();
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

}

}
