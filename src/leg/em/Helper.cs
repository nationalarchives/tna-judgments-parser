
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

    public static new IXmlDocument Parse(Stream docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static new IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    /// <summary>
    /// Parse an Explanatory Memorandum document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document stream</param>
    /// <param name="filename">The filename (e.g., uksiem_20132911_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    /// <summary>
    /// Parse an Explanatory Memorandum document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document bytes</param>
    /// <param name="filename">The filename (e.g., uksiem_20132911_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return ExplanatoryMemoranda.Parser.Parse(docx, filename);
    }

}

}
