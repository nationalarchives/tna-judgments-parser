

using System;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Main {

    internal static MainDocumentPart Get(OpenXmlElement e) {
        OpenXmlPartRootElement root = e.Ancestors<OpenXmlPartRootElement>().First();
        if (root is Document document)
            return document.MainDocumentPart;
        if (root is Header header)
            return ((WordprocessingDocument) header.HeaderPart.OpenXmlPackage).MainDocumentPart;
        if (root is Footnotes footnotes)
            return ((WordprocessingDocument) footnotes.FootnotesPart.OpenXmlPackage).MainDocumentPart;
        if (root is Endnotes endnotes)
            return ((WordprocessingDocument) endnotes.EndnotesPart.OpenXmlPackage).MainDocumentPart;
        if (root is DocumentFormat.OpenXml.Wordprocessing.Numbering numbering)
            return ((WordprocessingDocument) numbering.NumberingDefinitionsPart.OpenXmlPackage).MainDocumentPart;
        throw new Exception();
    }

}

}
