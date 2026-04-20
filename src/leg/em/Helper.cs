
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
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName);
    }

    /// <summary>
    /// Parse an Explanatory Memorandum document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document bytes</param>
    /// <param name="filename">The filename (e.g., uksiem_20132911_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return ExplanatoryMemoranda.Parser.Parse(docx, filename);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        nsmgr.AddNamespace("ukm", "http://www.legislation.gov.uk/namespaces/metadata");
        var documentMainType = xml.SelectSingleNode("//akn:proprietary/ukm:DocumentMainType/@Value", nsmgr)?.Value;
        TocGenerator.Generate(xml, GetWholeDocumentLabel(documentMainType));
    }

    /// <summary>
    /// Get the appropriate "whole document" label based on EM document type.
    /// </summary>
    private static string GetWholeDocumentLabel(string documentMainType) {
        if (string.IsNullOrEmpty(documentMainType))
            return "The whole Explanatory Memorandum";

        string typeLower = documentMainType.ToLowerInvariant();

        if (typeLower.Contains("policynote"))
            return "The whole Policy Note";
        if (typeLower.Contains("executivenote"))
            return "The whole Executive Note";

        return "The whole Explanatory Memorandum";
    }

}

}
