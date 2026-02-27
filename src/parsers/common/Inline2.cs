
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OMML = DocumentFormat.OpenXml.Math;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using Inline1 = UK.Gov.Legislation.Judgments.Parse.Inline;
using Fields = UK.Gov.Legislation.Judgments.Parse.Fields;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class Inline2 {

    internal static List<IInline> ParseContents(MainDocumentPart main, Paragraph p) {
        return ParseRuns(main, p.ChildElements);
    }

    internal static List<IInline> ParseRuns(MainDocumentPart main, OpenXmlElementList elements) {
        var instance = new Inline2 { Main = main, Elements = elements };
        List<IInline> parsed = instance.ParseRuns();
        if (instance.i < elements.Count)
            throw new Exception();
        return parsed;
    }

    private MainDocumentPart Main { get; init; }
    private OpenXmlElementList Elements { get; init; }
    private int i = 0;

    private ILogger Logger = Logging.Factory.CreateLogger<Inline2>();

    internal static bool IsSkippable(OpenXmlElement e) {
        if (e is ParagraphProperties)
            return true;
        if (e is ProofError || e.LocalName == "proofErr")
            return true;
        if (e is BookmarkEnd)
            return true;
        if (e is OpenXmlUnknownElement && e.LocalName == "bookmarkEnd")
            return true;
        if (e is DeletedRun)
            return true;
        if (e.LocalName == "smartTagPr")
            return true;
        if (e is PermStart perm) // https://docs.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.permstart
            return true;
        if (e is PermEnd)
            return true;
        if (e is CommentRangeStart || e is CommentRangeEnd)
            return true;
        return false;
    }

    private List<IInline> ParseRuns(bool stopAtFieldEnd = false) {
        List<IInline> parsed = new List<IInline>();
        while (i < Elements.Count) {
            OpenXmlElement e = Elements[i];
            if (IsSkippable(e)) {
                i += 1;
                continue;
            }

            if (Bookmarks.IsBookmark(e)) {
                i += 1;
                WBookmark made = Bookmarks.Parse(e);
                if (made is null)
                    continue;
                parsed.Add(made);
                continue;
            }

            if (Fields.IsFieldStart(e)) {
                i += 1;
                IEnumerable<IInline> sub = ParseFieldStart();
                parsed.AddRange(sub);
                continue;
            }
            // if (Fields.IsFieldSeparater(e)) {
            //     i += 1;
            //     continue;
            // }
            if (Fields.IsFieldEnd(e)) {
                i += 1;
                if (stopAtFieldEnd)
                    return parsed;
                continue;
            }

            if (e is Run run) {
                IEnumerable<IInline> inlines = Inline1.MapRunChildren(Main, run);
                parsed.AddRange(inlines);
                i += 1;
                continue;
            }
            if (e is OpenXmlUnknownElement && e.LocalName == "r") {
                IEnumerable<IInline> inlines = Inline1.MapRunChildren(Main, (OpenXmlUnknownElement) e);
                parsed.AddRange(inlines);
                i += 1;
                continue;
            }
            if (e is InsertedRun iRun || (e is OpenXmlUnknownElement && e.LocalName == "ins")) {    // EWCA/Civ/2004/1580, EWHC/Comm/2014/3124, EWCA/Crim/2004/3049, EWHC/Ch/2008/2961
                var children = ParseRuns(Main, e.ChildElements);
                parsed.AddRange(children);
                i += 1;
                continue;
            }
            if (e is SdtRun || e is SdtContentRun) {   // [2022] EWHC 3214 (Admin)
                var children = ParseRuns(Main, e.ChildElements);
                parsed.AddRange(children);
                i += 1;
                continue;
            }
            if (e is SdtProperties) {  // ukftt/grc/2023/816
                Logger.LogDebug("ignoring SDT properties");
                i += 1;
                continue;
            }
            if (e is SdtEndCharProperties) {  // [2023] UKFTT 1022 (GRC)
                Logger.LogDebug("ignoring <w:sdtEndPr>");
                i += 1;
                continue;
            }
            if (e is Hyperlink link) {
                var parsedLink = ParseHyperlink(Main, link);
                parsed.AddRange(parsedLink);
                i += 1;
                continue;
            }
            if (e.LocalName == "smartTag") {
                var children = ParseRuns(Main, e.ChildElements);
                parsed.AddRange(children);
                i += 1;
                continue;
            }
            if (e is BidirectionalOverride) {  // Bidirectional text override element
                var children = ParseRuns(Main, e.ChildElements);
                parsed.AddRange(children);
                i += 1;
                continue;
            }
            if (e is OMML.Paragraph oMathPara) { // [2022] EWHC 2363 (Pat)
                var children = ParseRuns(Main, e.ChildElements);
                parsed.AddRange(children);
                i += 1;
                continue;
            }
            if (e is OMML.OfficeMath omml) { // EWHC/Comm/2018/335
                IMath mathML = Math2.Parse(Main, omml);
                parsed.Add(mathML);
                i += 1;
                continue;
            }
            if (e is SimpleField fldSimple) { // EWHC/Admin/2006/983
                // List<IInline> children = ParseRuns(Main, fldSimple.ChildElements);
                // List<IInline> field = Fields2.Parse(Main, run, fldSimple.Instruction, children);
                // parsed.AddRange(field);
                var p = Fields.ParseSimple(Main, fldSimple);
                parsed.AddRange(p);
                i += 1;
                continue;
            }
            // Unknown element - try to extract content from children
            if (e.HasChildren) {
                Logger.LogDebug("Unknown inline element type: {Type} (LocalName: {LocalName}), parsing children", 
                    e.GetType().Name, e.LocalName);
                var children = ParseRuns(Main, e.ChildElements);
                parsed.AddRange(children);
                i += 1;
                continue;
            }
            // No children - try to get inner text as fallback
            string innerText = e.InnerText;
            if (!string.IsNullOrEmpty(innerText)) {
                Logger.LogDebug("Unknown inline element type: {Type} (LocalName: {LocalName}), using inner text", 
                    e.GetType().Name, e.LocalName);
                parsed.Add(new WText(innerText, null));
                i += 1;
                continue;
            }
            Logger.LogWarning("Unknown inline element type: {Type} (LocalName: {LocalName}), no content to extract", 
                e.GetType().Name, e.LocalName);
            i += 1;
        }
        return parsed;
    }

    private IEnumerable<IInline> ParseFieldStart() {

        while (i < Elements.Count && IsSkippable(Elements[i]))
            i += 1;
        if (i == Elements.Count)
            return Enumerable.Empty<IInline>();

        OpenXmlElement e;
        e = Elements[i];
        if (Fields.IsFieldEnd(e)) {
            i += 1;
            return Enumerable.Empty<IInline>();
        }

        Run run = null;
        string code = "";
        while (Fields.IsFieldCode(e) || e.ChildElements.All(child => child is RunProperties)) {
            if (Fields.IsFieldCode(e)) {
                run = (Run) e;
                code += Fields.GetFieldCode(e);
            }
            i += 1;
            if (i == Elements.Count)
                break;
            e = Elements[i];
        }
        code = DOCX.Fields.NormalizeFieldCode(code);
        Logger.LogDebug("field code: " + code);

        if (i < Elements.Count && e.ChildElements.All(child => child is RunProperties)) {
            i += 1;
            if (i < Elements.Count)
                e = Elements[i];
        }

        if (i < Elements.Count && Fields.IsFieldSeparater(e))
            i += 1;

        List<IInline> contents = ParseRuns(true);
        return Fields2.Parse(Main, run, code, contents);
    }

    internal static IEnumerable<IInline> ParseHyperlink(MainDocumentPart main, Hyperlink link) {
        IEnumerable<IInline> contents = ParseRuns(main, link.ChildElements);
        if (link.Id is not null) {
            Uri uri = DOCX.Relationships.GetUriForHyperlink(link);
            if (uri.IsAbsoluteUri) {
                contents = Merger.Merge(contents);
                WHyperlink2 link2 = new  WHyperlink2() { Href = uri.AbsoluteUri, Contents = contents };
                return new List<IInline>(1) { link2 };
            } else {
                return contents;
            }
        }
        if (link.Anchor is not null) {
            contents = Merger.Merge(contents);
            InternalLink iLink = new() { Target = link.Anchor, Contents = contents.ToList() };
            return new List<IInline>(1) { iLink };
        }
        if (Uri.IsWellFormedUriString(link.InnerText, UriKind.Absolute)) {
            contents = Merger.Merge(contents);
            WHyperlink2 link2 = new  WHyperlink2() { Href = link.InnerText, Contents = contents };
            return new List<IInline>(1) { link2 };
        }
        return contents;
    }

}

}
