

using System;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

static class Main {

    public static OpenXmlPartRootElement Root(this OpenXmlElement e) {
        if (e is RunProperties2 rProps)
            return rProps.Ancestors<OpenXmlPartRootElement>().First();
        return e.Ancestors<OpenXmlPartRootElement>().First();
    }

    internal static MainDocumentPart Get(OpenXmlElement e) {
        OpenXmlPartRootElement root = e.Root();
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
