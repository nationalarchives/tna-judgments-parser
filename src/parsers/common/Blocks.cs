
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
        .SelectMany<OpenXmlElement, IBlock>(e => ParseBlock(main, e));
    }

    internal static IEnumerable<IBlock> ParseBlock(MainDocumentPart main, OpenXmlElement e) {
        if (e is Paragraph para)
            return new List<IBlock>(1) { ParseParagraph(main, para) };
        if (e is Table table)
            return new List<IBlock>(1) { new WTable(main, table) };
        if (e is SdtBlock sdt)
            return ParseStdBlock(main, sdt);
        throw new Exception(e.GetType().ToString());
    }

    internal static IBlock ParseParagraph(MainDocumentPart main, Paragraph paragraph) {
        if (paragraph.InnerText.StartsWith("93.")) {

        }
        DOCX.NumberInfo? number = DOCX.Numbering2.GetFormattedNumber(main, paragraph);
        if (number is null)
            return new WLine(main, paragraph);
        return new WOldNumberedParagraph(number.Value, main, paragraph);
    }

    internal static IEnumerable<IBlock> ParseStdBlock(MainDocumentPart main, SdtBlock sdt) {
        return Blocks.ParseBlocks(main, sdt.SdtContentBlock.ChildElements);
    }

    internal static IDivision ParseStructuredDocumentTag2(MainDocumentPart main, SdtBlock sdt) {
        var dpg = sdt.Descendants<DocPartGallery>().FirstOrDefault()?.Val?.Value;
        if (dpg != "Table of Contents")
            throw new Exception();
        IEnumerable<IBlock> contents = ParseStdBlock(main, sdt);
        if (!contents.All(block => block is WLine))
            throw new Exception();
        return new WTableOfContents(contents.Cast<WLine>());
    }

}

}
