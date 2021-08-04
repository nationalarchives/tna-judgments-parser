
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

// https://support.microsoft.com/en-us/office/field-codes-ref-field-b2531c23-05d6-4e3b-b54f-aee24447ceb2

internal class Ref {

    private static string regex = @"^ REF ([_A-Za-z0-9]+)( \\r)?( \\p)?(\\w )? +\\h(  \\\* MERGEFORMAT)? $";

    internal static bool Is(string fieldCode) {
        return Regex.IsMatch(fieldCode, regex);
    }

    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, List<OpenXmlElement> withinField, int i) {
        Match match = Regex.Match(fieldCode, regex);
        if (!match.Success)
            throw new Exception();
        string bookmarkName = match.Groups[1].Value;
        bool rSwitch = !string.IsNullOrEmpty(match.Groups[2].Value);
        bool pSwitch = !string.IsNullOrEmpty(match.Groups[3].Value);
        bool wSwitch = !string.IsNullOrEmpty(match.Groups[4].Value);

        if (i == withinField.Count)
            return IgnoreFollowing(main, bookmarkName, rSwitch, pSwitch, wSwitch, (Run) withinField.First());
        
        OpenXmlElement next = withinField[i];
        if (!Fields.IsFieldSeparater(next))
            throw new Exception();
        
        try {
            return IgnoreFollowing(main, bookmarkName, rSwitch, pSwitch, wSwitch, (Run) withinField.First());
        } catch (Exception) {
            System.Console.WriteLine("no bookmark for " + bookmarkName);
        }

        IEnumerable<OpenXmlElement> remaining = withinField.Skip(i + 1);
        return Inline.ParseRuns(main, remaining);
    }

    private static bool BookmarkIsAbove(Paragraph refParagraph, Paragraph bookmarkParagrah) {
        bool above = true;
        Paragraph nextPara = refParagraph.NextSibling<Paragraph>();
        while (nextPara is not null) {
            if (nextPara == bookmarkParagrah)
                above = false;
            nextPara = nextPara.NextSibling<Paragraph>();
        }
        return above;
    }

    private static IEnumerable<IInline> IgnoreFollowing(MainDocumentPart main, string bookmarkName, bool rSwitch, bool pSwitch, bool wSwitch, Run field) {
        BookmarkStart bookmark = DOCX.Bookmarks.Get(main, bookmarkName);
        if (bookmark is null)
            throw new Exception();
        Paragraph bookmarkParagraph = bookmark.Ancestors<Paragraph>().First();
        string num;
        if (wSwitch) {  // EWHC/Comm/2018/1368
            num = DOCX.Numbering2.GetNumberInFullContext(main, bookmarkParagraph);
        } else {
            DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, bookmarkParagraph);
            if (info is null)
                throw new Exception("REF has no content and target has no number");
            num = info.Value.Number;
        }
        num = num.TrimEnd('.');
        if (pSwitch) {
            bool above = BookmarkIsAbove(field.Ancestors<Paragraph>().First(), bookmarkParagraph);
            num += above ? " above" : " below";
        }
        WText numberInThisFormat = new WText(num, field.RunProperties);
        return new List<IInline>(1) { numberInThisFormat };
    }

}

}
