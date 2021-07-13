
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;


namespace UK.Gov.Legislation.Judgments.Parse {

class Fields {

    internal static bool IsFieldStart(OpenXmlElement e) {
        if (e is not Run)
            return false;
        if (((Run) e).RsidRunAddition == "0065663B")
            System.Console.WriteLine();
        if (!e.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Begin)))
            return false;
        if (!e.ChildElements.All(child => child is RunProperties || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Begin))))
            throw new Exception();
        return true;
    }

    internal static bool IsFieldSeparater(OpenXmlElement e) {
        if (e is not Run)
            return false;
        if (!e.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Separate)))
            return false;
        if (!e.ChildElements.All(child => child is RunProperties || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.Separate))))
            throw new Exception();
        return true;
    }

    internal static bool IsFieldEnd(OpenXmlElement e) {
        if (e is not Run)
            return false;
        if (!e.ChildElements.Any(child => child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.End)))
            return false;
        if (!e.ChildElements.All(child => child is RunProperties || (child is FieldChar chr && chr.FieldCharType.Equals(FieldCharValues.End))))
            throw new Exception();
        return true;
    }

    internal static bool IsFieldCode(OpenXmlElement e) {
        if (e is not Run)
            return false;
        if (!e.ChildElements.Any(child => child is FieldCode))
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
            if (!IsFieldCode(next))
                break;
            fieldCode += GetFieldCode(next);
            i += 1;
        }
        // if (fieldCode == @" FILENAME \* MERGEFORMAT ?$") {
        //     return Inline.ParseRuns(main, withinField.Skip(i));
        // }
        // if (fieldCode == @"^ FILENAME \\\* MERGEFORMAT ?$") {
        //     return Inline.ParseRuns(main, withinField.Skip(i));
        // }
        Match match = Regex.Match(fieldCode, "^ FILENAME \\\\\\* MERGEFORMAT ?$");
        if (match.Success) {
            if (i == withinField.Count)
                return Enumerable.Empty<IInline>();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            return Inline.ParseRuns(main, withinField.Skip(i + 1));
        }
        // match = Regex.Match(fieldCode, "^ FILLIN \"(.+?)\" ?$");
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
        // match = Regex.Match(fieldCode, "^ FILLIN \"(.+?)\" ");
        // match = Regex.Match(fieldCode, "^ FILLIN ");
        // if (match.Success) {
        //     if (i > withinField.Count - 1)
        //         throw new Exception();
        //     OpenXmlElement next = withinField[i];
        //     if (!IsFieldSeparater(next))
        //         throw new Exception();
        //     return Inline.ParseRuns(main, withinField.Skip(i + 1));
        // }
        match = Regex.Match(fieldCode, "^ HYPERLINK \"(.+?)\" ?$");
        if (match.Success) {
            if (i != withinField.Count - 2)
                throw new Exception();
            OpenXmlElement next = withinField[i];
            if (!IsFieldSeparater(next))
                throw new Exception();
            OpenXmlElement last = withinField[i+1];
            if (last is not Run run)
                throw new Exception();
            IEnumerable<IInline> children = Inline.MapRunChildren(main, run);
            if (children.Count() != 1)
                throw new Exception();
            IInline firstChild = children.First();
            if (firstChild is not WText wt)
                throw new Exception();
            string href = match.Groups[1].Value;
            WHyperlink1 hyperlink = new WHyperlink1(wt) { Href = href };
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
        match = Regex.Match(fieldCode, @"^ REF ([_A-Za-z0-9]+) \\r \\h  \\\* MERGEFORMAT $");
        if (match.Success) {
            string rf = match.Groups[1].Value;
            OpenXmlElement root = first;
            while (root.Parent is not null)
                root = root.Parent;
            BookmarkStart bkmk = root.Descendants<BookmarkStart>().Where(b => b.Name == rf).First();
            Paragraph bkmkPara = bkmk.Ancestors<Paragraph>().First();
            // IFormattedText refNumber = DOCX.Numbering2.GetFormattedNumber(main, bkmkPara);
            DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, bkmkPara);
            RunProperties rProps = first is Run run ? run.RunProperties : null;
            WText numberInThisFormat = new WText(info.Value.Number, rProps);
            return new List<IInline>(1) { numberInThisFormat };
        }
        throw new Exception();
    }
}

}
