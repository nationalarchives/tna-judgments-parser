
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

// https://support.microsoft.com/en-us/office/field-codes-noteref-field-e1eba482-aec9-4ea8-a922-46c83bacfb57

internal class NoteRef {

    private static string regex = @"^ NOTEREF ([_A-Za-z0-9]+)( \\[fhp])+ (\\\* MERGEFORMAT )?$";

    internal static bool Is(string fieldCode) {
        return Regex.IsMatch(fieldCode, regex);
    }

    internal static List<IInline> Construct(MainDocumentPart main, Run run, string fieldCode) {
        Match match = Regex.Match(fieldCode, regex);
        if (!match.Success)
            throw new Exception();
        string bkmkName = match.Groups[1].Value;
        bool fSwitch = match.Groups[2].Captures.Where(v => v.Value == @" \f").Any();
        /* In EWHC/Ch/2014/1316, the \p switch is present, but the surrounding text includes the word "above" */
        bool pSwitch = false;
        BookmarkStart bkmk = DOCX.Bookmarks.Get(main, bkmkName);
        if (bkmk is null)
            throw new Exception();
        FootnoteEndnoteReferenceType note = bkmk.NextSibling().ChildElements.OfType<FootnoteEndnoteReferenceType>().First();
        string marker = WFootnote.GetMarker(note);
        if (pSwitch) {
            bool above = Ref.BookmarkIsAbove(run, bkmk);
            marker += above ? " above" : " below";
        }
        WText numberInThisFormat = new WText(marker, run.RunProperties);
        return new List<IInline>(1) { numberInThisFormat };
    }

}

}
