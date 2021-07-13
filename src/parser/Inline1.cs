
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class Inline {

    public static IEnumerable<IInline> ParseRuns(MainDocumentPart main, IEnumerable<OpenXmlElement> elements) {
        List<IInline> parsed = new List<IInline>();
        List<OpenXmlElement> withinField = null;
        foreach (OpenXmlElement e in elements) {
            if (e is ParagraphProperties)
                continue;
            if (e is ProofError)
                continue;
            if (e is BookmarkStart || e is BookmarkEnd)
                continue;
            if (Fields.IsFieldStart(e)) {
                if (withinField is not null)
                    throw new Exception();
                withinField = new List<OpenXmlElement>();
                continue;
            }
            if (Fields.IsFieldSeparater(e)) {
                if (withinField is null)
                    throw new Exception();
                withinField.Add(e);
                continue;
            }
            if (Fields.IsFieldEnd(e)) {
                if (withinField is null)
                    throw new Exception();
                IEnumerable<IInline> parsedFieldContents = Fields.ParseFieldContents(main, withinField);
                parsed.AddRange(parsedFieldContents);
                withinField = null;
                continue;
            }
            if (Fields.IsFieldCode(e)) {
                if (withinField is null)
                    throw new Exception();
                withinField.Add(e);
                continue;
            }
            if (e is Hyperlink link) {
                if (withinField is not null)
                    throw new Exception();
                WHyperlink2 link2 = MapHyperlink(main, link);
                parsed.Add(link2);
                continue;
            }
            if (e is Run run) {
                if (withinField is null) {
                    IEnumerable<IInline> inlines = MapRunChildren(main, run);
                    parsed.AddRange(inlines);
                } else {
                    withinField.Add(e);
                }
                continue;
            }
            if (e is OpenXmlUnknownElement) {
                if (withinField is not null)
                    throw new Exception();
                if (e.LocalName == "smartTag") {
                    var children = ParseRuns(main, e.ChildElements);
                    parsed.AddRange(children);
                    continue;
                }
                if (e.LocalName == "r") {
                    Run run2 = new Run(e.OuterXml);
                    e.InsertAfterSelf(run2);
                    e.Remove();
                    var children = MapRunChildren(main, run2);
                    parsed.AddRange(children);
                    continue;
                }
            }
            throw new Exception();
        }
        return parsed;
    }

    private static WHyperlink2 MapHyperlink(MainDocumentPart main, Hyperlink link) {
        string href;
        if (link.Id is not null)
            href = DOCX.Relationships.GetUriForHyperlink(link).AbsoluteUri;
        else
            href = link.InnerText;
        IEnumerable<IInline> contents = ParseRuns(main, link.ChildElements);
        contents = Merger.Merge(contents);
        return new WHyperlink2() { Href = href, Contents = contents };
    }

    internal static IEnumerable<IInline> MapRunChildren(MainDocumentPart main, Run run) {
        return run.ChildElements
            .Where(e => !(e is RunProperties))
            .Select(e => MapRunChild(main, run, e))
            .Where(i => i is not null);
    }

    private static IInline MapRunChild(MainDocumentPart main, Run run, OpenXmlElement e) {
        if (e is Text text)
            return new WText(text, run.RunProperties);
        if (e is TabChar tab)
            return new WTab(tab);
        if (e is Break br)
            return new WLineBreak(br);
        if (e is NoBreakHyphen hyphen)
            return new WText(hyphen, run.RunProperties);
        if (e is FootnoteReference fn)
            return new WFootnote(main, fn);
        if (e is Drawing draw)
            return new WImageRef(main, draw);
        if (e is Picture pict)
            return new WImageRef(main, pict);
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
        if (e is AlternateContent altContent) {
            if (altContent.ChildElements.Count != 2)
                throw new Exception();
            AlternateContentChoice choice = (AlternateContentChoice) altContent.FirstChild;
            AlternateContentFallback fallback = (AlternateContentFallback) altContent.ChildElements.ElementAt(1);
            if (fallback.FirstChild is Picture pict2) {
                if (pict2.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().Any(id => id.RelationshipId is not null))
                    return new WImageRef(main, pict2);
                if (pict2.ChildElements.Count == 1 && pict2.FirstChild.NamespaceUri == "urn:schemas-microsoft-com:vml"  && pict2.FirstChild.LocalName == "line")
                    return null;
            }
        }
        throw new Exception(e.OuterXml);
    }

}

}
