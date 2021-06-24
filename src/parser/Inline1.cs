
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class Inline {

    public static IEnumerable<IInline> ParseRuns(MainDocumentPart main, IEnumerable<OpenXmlElement> elements) {
        return elements
        .Where(e => !(e is ParagraphProperties))
        .Where(e => !(e is BookmarkStart))
        .Where(e => !(e is BookmarkEnd))
        .SelectMany(e => MapRun(main, e));
    }

    private static IEnumerable<IInline> MapRun(MainDocumentPart main, OpenXmlElement e) {
        if (e is Run run1)
            return MapRunChildren(main, run1);
        if (e is Hyperlink)
            return ParseRuns(main, e.ChildElements);
        if (e is ProofError)
            return Enumerable.Empty<IInline>();
        if (e.LocalName == "smartTag")
            return ParseRuns(main, e.ChildElements);
        if (e.LocalName == "r") {
            Run run2 = new Run(e.OuterXml);
            return MapRunChildren(main, run2);
        }
        throw new Exception(e.OuterXml);
    }

    private static IEnumerable<IInline> MapRunChildren(MainDocumentPart main, Run run) {
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
            AlternateContentChoice choice = (AlternateContentChoice) altContent.FirstChild;
            if (choice.ChildElements.Count != 1)
                throw new Exception();
            Drawing child = (Drawing) choice.FirstChild;
            return null;
        }
        throw new Exception(e.OuterXml);
    }

}

}
