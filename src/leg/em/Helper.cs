
using System.IO;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class Helper : BaseHelper {

    private static readonly Helper Instance = new Helper();

    private Helper() : base(LegislativeDocumentConfig.ForExplanatoryMemoranda()) { }

    public static new IXmlDocument Parse(Stream docx, bool simplify = true, bool generateToc = false) {
        return ((BaseHelper)Instance).Parse(docx, simplify, generateToc);
    }

    public static new IXmlDocument Parse(byte[] docx, bool simplify = true, bool generateToc = false) {
        return ((BaseHelper)Instance).Parse(docx, simplify, generateToc);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx) {
        return ExplanatoryMemoranda.Parser.Parse(docx);
    }

}

}
