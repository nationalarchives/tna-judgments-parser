
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OMML = DocumentFormat.OpenXml.Math;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse {

class Inline {

    private static ILogger logger = Logging.Factory.CreateLogger<Parse.Inline>();

    public static IEnumerable<IInline> ParseRuns(MainDocumentPart main, IEnumerable<OpenXmlElement> elements) {
        List<IInline> parsed = new List<IInline>();
        List<OpenXmlElement> withinField = null;
        IEnumerator<OpenXmlElement> enumerator = elements.GetEnumerator();
        while (enumerator.MoveNext()) {
            OpenXmlElement e = enumerator.Current;
            if (e is ParagraphProperties)
                continue;
            if (e is ProofError)
                continue;
            if (e is BookmarkStart || e is BookmarkEnd)
                continue;
            if (e is OpenXmlUnknownElement && e.LocalName == "bookmarkStart")
                continue;
            if (e is OpenXmlUnknownElement && e.LocalName == "bookmarkEnd")
                continue;
            if (Fields.IsFieldStart(e)) {
                if (withinField is null) {
                    withinField = new List<OpenXmlElement>();
                } else if (withinField.First() is not null && Fields.IsFieldCode(withinField.First()) && Fields.GetFieldCode(withinField.First()).StartsWith("tc ")) {  // EWHC/Ch/2015/448
                    logger.LogWarning("field within field");
                    enumerator.MoveNext();
                    e = enumerator.Current;
                    while (!Fields.IsFieldEnd(e)) {
                        enumerator.MoveNext();
                        e = enumerator.Current;
                    }
                } else {
                    throw new Exception();
                }
                continue;
            }
            if (Fields.IsFieldSeparater(e)) {
                if (withinField is null)
                    throw new Exception();
                withinField.Add(e);
                continue;
            }
            if (Fields.IsFieldEnd(e)) {
                if (withinField is null) {  // EWHC/Comm/2004/999
                    logger.LogWarning("field end without start in same paragraph");
                    continue;
                }
                IEnumerable<IInline> parsedFieldContents = Fields.ParseFieldContents(main, withinField);
                parsed.AddRange(parsedFieldContents);
                withinField = null;
                continue;
            }
            if (Fields.IsFieldCode(e)) {
                if (withinField is null)
                    throw new Exception(e.InnerText);
                withinField.Add(e);
                continue;
            }
            if (withinField is not null) {
                withinField.Add(e);
                continue;
            }
            // if (e is InsertedRun iRun2 && withinField is not null && e.ChildElements.Count == 1 && Fields.IsFieldCode(e.FirstChild)) {    // EWCA/Crim/2004/3049
            //     withinField.Add(e.FirstChild);
            //     continue;
            // }
            if (e is Hyperlink link) {
                // if (withinField is not null) {  // EWHC/Ch/2007/1044
                //     withinField.Add(e);
                //     continue;
                // }
                if (link.Id is null && link.Descendants<FootnoteReference>().Any()) {   // EWHC/Admin/2012/914
                    IEnumerable<IInline> content = ParseRuns(main, link.ChildElements);
                    parsed.AddRange(content);
                    continue;
                }
                WHyperlink2 link2 = MapHyperlink(main, link);
                if (link2 is null) {
                    IEnumerable<IInline> content = ParseRuns(main, link.ChildElements);
                    parsed.AddRange(content);
                    continue;
                }
                parsed.Add(link2);
                continue;
            }
            if (e is Run run) {
                // if (withinField is null) {
                    IEnumerable<IInline> inlines = MapRunChildren(main, run);
                    parsed.AddRange(inlines);
                // } else {
                //     withinField.Add(e);
                // }
                continue;
            }
            if (e is OpenXmlUnknownElement && e.LocalName == "r") {
                // if (withinField is null) {
                    IEnumerable<IInline> inlines = MapRunChildren(main, (OpenXmlUnknownElement) e);
                    parsed.AddRange(inlines);
                // } else {
                //     withinField.Add(e);
                // }
                continue;
            }
            if (e is InsertedRun iRun || (e is OpenXmlUnknownElement && e.LocalName == "ins")) {    // EWCA/Civ/2004/1580, EWHC/Comm/2014/3124, EWCA/Crim/2004/3049, EWHC/Ch/2008/2961
                // if (withinField is null) {
                    var children = ParseRuns(main, e.ChildElements);
                    parsed.AddRange(children);
                // } else {
                //     withinField.Add(e);
                // }
                continue;
            }
            if (e is DeletedRun dRun) {    // EWCA/Civ/2004/1580
                continue;
            }
            if (e is OpenXmlUnknownElement) {
                // if (withinField is not null) {
                //     withinField.Add(e);
                //     continue;
                // }
                if (e.LocalName == "smartTag") {
                    var children = ParseRuns(main, e.ChildElements);
                    parsed.AddRange(children);
                    continue;
                }
                if (e.LocalName == "smartTagPr")
                    continue;
                if (e.LocalName == "proofErr")   // EWCA/Civ/2017/320
                    continue;
                // if (e.LocalName == "r") {
                //     Run run2 = new Run(e.OuterXml);
                //     e.InsertAfterSelf(run2);
                //     e.Remove();
                //     var children = MapRunChildren(main, run2);
                //     parsed.AddRange(children);
                //     continue;
                // }
                throw new Exception();
            }
            if (e is PermStart perm) {  // https://docs.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.permstart
                if (perm.EditorGroup == RangePermissionEditingGroupValues.Everyone) // EWCA/Civ/2014/823
                    continue;
                else
                    throw new Exception();
            }
            if (e is PermEnd) {
                continue;
            }
            if (e is CommentRangeStart || e is CommentRangeEnd) // EWHC/Comm/2016/869
                continue;
            if (e is OMML.OfficeMath omml) { // EWHC/Comm/2018/335
                IMath mathML = Math2.Parse(main, omml);
                parsed.Add(mathML);
                continue;
            }
            if (e is SimpleField fldSimple) { // EWHC/Admin/2006/983
                var p = Fields.ParseSimple(main, fldSimple);
                parsed.AddRange(p);
            }
            throw new Exception();
        }
        if (withinField is not null) {  // EWHC/Comm/2004/999
            logger.LogWarning("paragraph end before field end");
            IEnumerable<IInline> parsedFieldContents = Fields.ParseFieldContents(main, withinField);
            parsed.AddRange(parsedFieldContents);
        }
        return parsed;
    }

    private static WHyperlink2 MapHyperlink(MainDocumentPart main, Hyperlink link) {
        string href;
        if (link.Id is not null) {
            Uri uri = DOCX.Relationships.GetUriForHyperlink(link);
            if (uri.IsAbsoluteUri) {
                href = uri.AbsoluteUri;
            } else {
                logger.LogWarning("ignoring internal hyperlink: id = " + link.Id);
                return null;
            }
        }
        else if (Uri.IsWellFormedUriString(link.InnerText, UriKind.Absolute))
            href = link.InnerText;
        else if (link.Anchor is not null) {
            logger.LogWarning("ignoring internal hyperlink: @anchor = " + link.Anchor);
            return null;    // EWHC/Ch/2018/2285
        } else {
            logger.LogWarning("ignoring hyperlink: InnerText = " + link.InnerText);
            return null;    // EWHC/Ch/2007/1044 contains field codes
        }
        IEnumerable<IInline> contents = ParseRuns(main, link.ChildElements);
        contents = Merger.Merge(contents);
        return new WHyperlink2() { Href = href, Contents = contents };
    }

    internal static IEnumerable<IInline> MapRunChildren(MainDocumentPart main, Run run) {
        return run.ChildElements
            .Where(e => !(e is RunProperties))
            .Select(e => MapRunChild(main, run.RunProperties, e))
            .Where(i => i is not null);
    }
    internal static IEnumerable<IInline> MapRunChildren(MainDocumentPart main, OpenXmlUnknownElement run) {
        OpenXmlElement rPropsRaw = run.ChildElements.Where(e => e.LocalName == "rPr").FirstOrDefault();
        RunProperties rProps;
        if (rPropsRaw is null)
            rProps = null;
        else {
            rProps = new RunProperties2(rPropsRaw);
            // rProps = new RunProperties(rPropsRaw.OuterXml);
            // rPropsRaw.AppendChild(rProps);
        }
        return run.ChildElements
            .Where(e => e.LocalName != "rPr")
            .Select(e => MapRunChild(main, rProps, e))
            .Where(i => i is not null);
    }

    internal static IInline MapRunChild(MainDocumentPart main, RunProperties rProps, OpenXmlElement e) {
        logger.LogTrace(e.GetType().Name + " " + e.LocalName);
        if (e is Text text)
            return new WText(text, rProps);
        if (e is OpenXmlElement && e.LocalName == "t")
            return new WText(e.InnerText, rProps);
        if (e is TabChar tab)
            return new WTab(tab);
        if (e is Break br)
            return new WLineBreak(br);
        if (e is NoBreakHyphen hyphen)
            return new WText(hyphen, rProps);
        if (e is SoftHyphen soft)
            return null;
        if (e is FootnoteReference)
            logger.LogInformation("footnote reference");
        if (e is FootnoteReference fn)
            return new WFootnote(main, fn);
        if (e is EndnoteReference)
            logger.LogInformation("endnote reference");
        if (e is EndnoteReference en)
            return new WFootnote(main, en);
        if (e is Drawing draw)
            return new WImageRef(main, draw);
        if (e is Picture pict)
            return WImageRef.Make(main, pict);
        if (e.LocalName == "object") {
            DocumentFormat.OpenXml.Vml.Shape shape = e.ChildElements.OfType<DocumentFormat.OpenXml.Vml.Shape>().FirstOrDefault();
            if (shape is not null)
                return new WImageRef(main, shape);
        }
        if (e is RunProperties)
            return null;
        if (e is FieldChar || e is FieldCode)
            return null;
        if (e is LastRenderedPageBreak)
            return null;
        if (e is FootnoteReferenceMark)
            return null;
        if (e is EndnoteReferenceMark)
            return null;
        if (e is AlternateContent altContent)
            return AlternateContent2.Map(main, rProps, altContent);
        if (e is SymbolChar sym)  // EWCA/Civ/2013/470
            return SpecialCharacter.Make(sym, rProps);
        if (e is CommentReference) // EWCA/Civ/2004/55
            return null;
        // if (e is PageNumber)    // EWHC/Fam/2006/3743
        //     return null;
        throw new Exception(e.OuterXml);
    }
    // internal static IInline MapRunChild(MainDocumentPart main, Run run, OpenXmlElement e) {
    //     return MapRunChild(main, run.RunProperties, e);
    // }

}

}
