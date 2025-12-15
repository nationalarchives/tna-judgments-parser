
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse {

// https://support.microsoft.com/en-us/office/field-codes-ref-field-b2531c23-05d6-4e3b-b54f-aee24447ceb2

internal class Ref {

    private static ILogger logger = Logging.Factory.CreateLogger<Ref>();

    private static string pattern = @"^ REF ([_A-Za-z0-9]+)( ?\\?[hnrpw])*( \\\* MERGEFORMAT)? $";  // no space before switch in EWHC/Ch/2015/448

    internal static bool Is(string fieldCode) {
        return fieldCode.StartsWith(" REF ");
    }

    [Obsolete]
    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, List<OpenXmlElement> withinField, int i) {
        Match match = Regex.Match(fieldCode, pattern);
        if (!match.Success)
            throw new Exception();
        string bookmarkName = match.Groups[1].Value;

        CaptureCollection swtchs = match.Groups[2].Captures;
        bool rSwitch = swtchs.Where(v => v.Value == @" \r" || v.Value == @" r").Any();  // EWCA/Civ/2009/1119 has no \ character
        bool nSwitch = swtchs.Where(v => v.Value == @" \n" || v.Value == @" n").Any();  // EWHC/Patents/2013/2927
        bool pSwitch = swtchs.Where(v => v.Value == @" \p" || v.Value == @" p").Any();
        bool wSwitch = swtchs.Where(v => v.Value == @" \w" || v.Value == @" w").Any();
        bool hSwitch = swtchs.Where(v => v.Value == @" \h" || v.Value == @" h").Any();  // EWCA/Civ/2009/1119 has no \ character

        if (i == withinField.Count)
            return IgnoreFollowing(main, bookmarkName, rSwitch, pSwitch, wSwitch, (Run) withinField.First());

        OpenXmlElement next = withinField[i];
        if (next is InsertedRun && !next.ChildElements.Any()) { // EWCA/Civ/2008/643.rtf
            i += 1;
            next = withinField[i];
        }
        if (!Fields.IsFieldSeparater(next))
            throw new Exception();

        try {
            return IgnoreFollowing(main, bookmarkName, rSwitch, pSwitch, wSwitch, (Run) withinField.First());
        } catch (Exception) {
            logger.LogWarning("no bookmark for " + bookmarkName);
        }

        IEnumerable<OpenXmlElement> remaining = withinField.Skip(i + 1);
        return Inline.ParseRuns(main, remaining);
    }

    internal static List<IInline> Construct(MainDocumentPart main, Run run, string fieldCode) {
        // with the current regex pattern, the regex will never match a Lawmaker reference and
        // we assume the reference is invalid if it's empty.
        // If/When Lawmaker diverges this part of the codebase (specifically references and
        // the functionality of PreParser.cs) Then we can possibly expand this part for Lawmaker
        Match match = Regex.Match(fieldCode, pattern);
        if (!match.Success) {
            logger.LogWarning("Malformed field code {} will be ignored!", fieldCode);
            return [new WInvalidRef()];
        }
        string bookmarkName = match.Groups[1].Value;
        CaptureCollection swtchs = match.Groups[2].Captures;
        bool rSwitch = swtchs.Where(v => v.Value == @" \r" || v.Value == @" r").Any();  // EWCA/Civ/2009/1119 has no \ character
        bool nSwitch = swtchs.Where(v => v.Value == @" \n" || v.Value == @" n").Any();  // EWHC/Patents/2013/2927
        bool pSwitch = swtchs.Where(v => v.Value == @" \p" || v.Value == @" p").Any();
        bool wSwitch = swtchs.Where(v => v.Value == @" \w" || v.Value == @" w").Any();
        bool hSwitch = swtchs.Where(v => v.Value == @" \h" || v.Value == @" h").Any();  // EWCA/Civ/2009/1119 has no \ character
        return IgnoreFollowing(main, bookmarkName, rSwitch, pSwitch, wSwitch, run);
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

    internal static bool BookmarkIsAbove(OpenXmlElement anchor, BookmarkStart bookmark) {
        Paragraph anchorParagraph = anchor.Ancestors<Paragraph>().First();
        Paragraph bookmarkParagraph = bookmark.Ancestors<Paragraph>().First();
        return BookmarkIsAbove(anchorParagraph, bookmarkParagraph);
    }

    private static List<IInline> IgnoreFollowing(MainDocumentPart main, string bookmarkName, bool rSwitch, bool pSwitch, bool wSwitch, Run field) {
        BookmarkStart bookmark = DOCX.Bookmarks.Get(main, bookmarkName);
        if (bookmark is null) {
            logger.LogWarning("can't find bookmark {}", bookmarkName);
            return [];
        }
        Paragraph bookmarkParagraph = bookmark.Ancestors<Paragraph>().FirstOrDefault();
        if (bookmarkParagraph is null) {
            logger.LogWarning("bookmark {} has no parent paragraph", bookmarkName);
            return [];
        }
        string num;
        if (wSwitch) {  // EWHC/Comm/2018/1368
            num = DOCX.Numbering2.GetNumberInFullContext(main, bookmarkParagraph);
        } else {
            DOCX.NumberInfo? info = DOCX.Numbering2.GetFormattedNumber(main, bookmarkParagraph);
            if (info is null) {
                // REF target has no number - use the paragraph text content instead
                string text = bookmarkParagraph.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text)) {
                    logger.LogDebug("REF target bookmark {} has no number, using paragraph text", bookmarkName);
                    WText textInThisFormat = new WText(text, field.RunProperties);
                    return new List<IInline>(1) { textInThisFormat };
                }
                logger.LogWarning("REF target bookmark {} has no number and no text content", bookmarkName);
                return [];
            }
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
