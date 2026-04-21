
using System.IO;
using System.Xml;

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
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName, bool allowUnrenderedCharts = true, UK.Gov.Legislation.Common.Rendering.IDrawingRenderer renderer = null) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName, allowUnrenderedCharts, renderer);
    }

    /// <summary>
    /// Parse a Code of Practice document with filename for metadata lookup.
    /// </summary>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName, bool allowUnrenderedCharts = true, UK.Gov.Legislation.Common.Rendering.IDrawingRenderer renderer = null) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName, allowUnrenderedCharts, renderer);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return CodesOfPractice.Parser.Parse(docx, filename);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        TocGenerator.Generate(xml, "The whole Code of Practice", TocGenerator.TocStrategy.BoldTitleDocumentOrder);
    }

}

}
