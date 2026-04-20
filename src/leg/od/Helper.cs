
using System.IO;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.OtherDocuments {

class Helper : BaseHelper {

    private static readonly Helper Instance = new Helper();

    private Helper() : base(LegislativeDocumentConfig.ForOtherDocuments()) { }

    public static new IXmlDocument Parse(Stream docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static new IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    /// <summary>
    /// Parse an Other Document with filename for metadata lookup.
    /// </summary>
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName);
    }

    /// <summary>
    /// Parse an Other Document with filename for metadata lookup.
    /// </summary>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return OtherDocuments.Parser.Parse(docx, filename);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        TocGenerator.Generate(xml, "The whole Document", TocGenerator.TocStrategy.BoldTitleDocumentOrder);
    }

}

}
