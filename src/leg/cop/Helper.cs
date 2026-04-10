
using System.IO;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.CodesOfPractice {

class Helper : BaseHelper {

    private static readonly Helper Instance = new Helper();

    private Helper() : base(LegislativeDocumentConfig.ForCodesOfPractice()) { }

    public static new IXmlDocument Parse(Stream docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static new IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    /// <summary>
    /// Parse a Code of Practice document with filename for metadata lookup.
    /// </summary>
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    /// <summary>
    /// Parse a Code of Practice document with filename for metadata lookup.
    /// </summary>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return CodesOfPractice.Parser.Parse(docx, filename);
    }

}

}
