
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse {

class Inline {

    private static ILogger logger = Logging.Factory.CreateLogger<Parse.Inline>();

    internal static IEnumerable<IInline> MapRunChildren(MainDocumentPart main, Run run) {
        return run.ChildElements
            .Where(e => !(e is RunProperties))
            .Select(e => MapRunChild(main, run.RunProperties, e))
            .Where(i => i is not null);
    }
    internal static IEnumerable<IInline> MapRunChildren(MainDocumentPart main, OpenXmlUnknownElement run) {
        OpenXmlElement rPropsRaw = run.ChildElements.Where(e => e.LocalName == "rPr").FirstOrDefault();
        RunProperties rProps = rPropsRaw is null ? null : new RunProperties2(rPropsRaw);
        return run.ChildElements
            .Where(e => e.LocalName != "rPr")
            .Select(e => MapRunChild(main, rProps, e))
            .Where(i => i is not null);
    }

    internal static IInline MapRunChild(MainDocumentPart main, RunProperties rProps, OpenXmlElement e) {
        logger.LogTrace($"parsing element: type = { e.GetType().Name }, name = { e.LocalName }");
        if (e is Text text)
            return new WText(text, rProps);
        if (e is OpenXmlElement && e.LocalName == "t")
            return new WText(e.InnerText, rProps);
        if (e is TabChar tab)
            return new WTab(tab);
        if (e is OpenXmlUnknownElement && e.LocalName == "tab")
            return new WTab(e);
        if (e is Break br) {
            if (br.Type is not null && br.Type == BreakValues.Page)    // [2023] EWHC 323 (Ch)
                return null;
            // if (br.Type == BreakValues.Column)   // ?
            //     return null;
            return new WLineBreak(br);
        }
        if (e is OpenXmlUnknownElement && e.LocalName == "br")
            return new WLineBreak(e);
        if (e is NoBreakHyphen hyphen)
            return new WText(hyphen, rProps);
        if (e.LocalName == "noBreakHyphen") // EWHC/Admin/2007/2606
            return WText.MakeHyphen(rProps);
        if (e is SoftHyphen soft)
            return null;
        if (e is FootnoteReference)
            logger.LogDebug("footnote reference");
        if (e is FootnoteReference fn)
            return new WFootnote(main, fn);
        if (e is EndnoteReference)
            logger.LogDebug("endnote reference");
        if (e is EndnoteReference en)
            return new WFootnote(main, en);
        if (e is Drawing draw)
            return new WImageRef(main, draw);
        if (e is Picture pict) {
            // look for text box?
            return WImageRef.Make(main, pict);
        }
        if (e is OpenXmlUnknownElement && e.LocalName == "pict" && e.NamespaceUri == "http://schemas.openxmlformats.org/wordprocessingml/2006/main") // ukftt/grc/2023/782
            return WImageRef.Make(main, new Picture(e.OuterXml));
        if (e.LocalName == "object") {
            DocumentFormat.OpenXml.Vml.Shape shape = e.ChildElements.OfType<DocumentFormat.OpenXml.Vml.Shape>().FirstOrDefault();
            if (shape is not null)
                return new WImageRef(main, shape);
        }
        if (e is RunProperties)
            return null;
        if (e is FieldChar || e is FieldCode)
            return null;
        if (e is LastRenderedPageBreak || e.LocalName == "lastRenderedPageBreak")
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
        if (e is PageNumber)    // EWCA/Civ/2018/2837.pdf
            return null;
        if (e is CarriageReturn cr) // EWCA/Civ/2018/2837.pdf
            return new WLineBreak(cr);
        if (e is SeparatorMark || e is ContinuationSeparatorMark) // EWCA/Civ/2018/2837.pdf
            return null;
        if (e is PositionalTab pTab)    // EWCA/Civ/2018/1825.rtf in header
            return new WTab(pTab);
        throw new Exception(e.OuterXml);
    }

    private static bool IsDuplicateSumAboveField(List<OpenXmlElement> withinField, BidirectionalEnumerator<OpenXmlElement> enumerator) {
        if (withinField.Count != 2)
            return false;
        var first = withinField[0];
        if (!Fields.IsFieldCode(first))
            return false;
        if (Fields.GetFieldCode(first) != " =SUM(ABOVE) ")
            return false;
        var second = withinField[1];
        if (!Fields.IsFieldSeparater(second))
            return false;
        if (!enumerator.MoveNext())
            return false;
        var next = enumerator.Current;
        if (!Fields.IsFieldStart(next)) {
            enumerator.MovePrevious();
            return false;
        }
        if (!enumerator.MoveNext()) {
            enumerator.MovePrevious();
            return false;
        }
        var nextNext = enumerator.Current;
        enumerator.MovePrevious();
        enumerator.MovePrevious();
        return Fields.IsFieldCode(nextNext) && Fields.GetFieldCode(nextNext) == " =SUM(ABOVE) ";
    }

}

}
