
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

internal class Blocks {

    internal static IEnumerable<IBlock> ParseBlocks(MainDocumentPart main, IEnumerable<OpenXmlElement> elements) {
        return elements
        .Where(e => e is not SectionProperties)
        .Where(e => e is not BookmarkStart)
        .Where(e => e is not BookmarkEnd)
        .Select<OpenXmlElement, IBlock>(e => ParseBlock(main, e));
    }

    internal static IBlock ParseBlock(MainDocumentPart main, OpenXmlElement e) {
        if (e is Paragraph para)
            return ParseParagraph(main, para);
        if (e is Table table)
            return new WTable(main, table);
        throw new Exception(e.GetType().ToString());
    }

    internal static IBlock ParseParagraph(MainDocumentPart main, Paragraph paragraph) {
        string number = DOCX.Numbering2.GetFormattedNumber(main, paragraph)?.Number;
        if (number is null)
            return new WLine(main, paragraph);
        return new WOldNumberedParagraph(number, main, paragraph);
    }

}

}
