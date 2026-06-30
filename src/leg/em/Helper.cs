
using System;
using System.IO;
using System.Linq;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class Helper : BaseHelper {

    private const string AKN_NAMESPACE = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    private static readonly Helper Instance = new Helper();

    private Helper() : base(LegislativeDocumentConfig.ForExplanatoryMemoranda()) { }

    public static IXmlDocument Parse(Stream docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    /// <summary>
    /// Parse an Explanatory Memorandum document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document stream</param>
    /// <param name="filename">The filename (e.g., uksiem_20132911_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName, bool allowUnrenderedCharts = true, UK.Gov.Legislation.Common.Rendering.IDrawingRenderer renderer = null) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName, allowUnrenderedCharts, renderer);
    }

    /// <summary>
    /// Parse an Explanatory Memorandum document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document bytes</param>
    /// <param name="filename">The filename (e.g., uksiem_20132911_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName, bool allowUnrenderedCharts = true, UK.Gov.Legislation.Common.Rendering.IDrawingRenderer renderer = null) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName, allowUnrenderedCharts, renderer);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return ExplanatoryMemoranda.Parser.Parse(docx, filename);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        StripLeadingTabMarkers(xml);
        TocGenerator.Generate(xml);
    }

}

}
