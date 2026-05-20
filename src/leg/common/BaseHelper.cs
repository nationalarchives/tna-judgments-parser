using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Common.Rendering;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

abstract class BaseHelper {

    protected readonly LegislativeDocumentConfig Config;

    protected BaseHelper(LegislativeDocumentConfig config) {
        Config = config;
    }

    public IXmlDocument Parse(
        Stream docx, bool simplify = true, string filename = null,
        string manifestationName = Builder.DefaultManifestationName,
        bool allowUnrenderedCharts = true,
        IDrawingRenderer renderer = null) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify, filename, manifestationName, allowUnrenderedCharts, renderer, docxBytes: null);
    }

    public IXmlDocument Parse(
        byte[] docx, bool simplify = true, string filename = null,
        string manifestationName = Builder.DefaultManifestationName,
        bool allowUnrenderedCharts = true,
        IDrawingRenderer renderer = null) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify, filename, manifestationName, allowUnrenderedCharts, renderer, docxBytes: docx);
    }

    private IXmlDocument Parse(
        WordprocessingDocument docx, bool simplify, string filename,
        string manifestationName, bool allowUnrenderedCharts,
        IDrawingRenderer renderer, byte[] docxBytes) {
        using var session = RenderSession.Begin(
            renderer ?? new NullRenderer(), docxBytes, filename, allowUnrenderedCharts);

        // Stash docx bytes for the duration of the parse so DocxLastModified
        // can fall back to a direct ZIP read of docProps/core.xml when the
        // SDK's PackageProperties.Modified misses it (LibreOffice's bad rels
        // URI; see DocxLastModified for details).
        using var bytesScope = docxBytes is null ? null : DocxLastModified.WithDocxBytes(docxBytes);

        TextBoxLifter.Lift(docx);

        IDocument doc = ParseDocument(docx, filename);

        MergeRenderedImages(doc, RenderSession.Current);

        IEnumerable<Judgments.IImage> processedImages = LegImageProcessor.ProcessImages(doc);

        doc.Meta.Statistics = StatisticsCalculator.Calculate(doc);

        XmlDocument xml = Builder.Build(doc, manifestationName);
        docx.Dispose();
        if (simplify)
            LegSimplifier.Simplify(xml);

        ApplyDocumentSpecificProcessing(xml);

        // IA consumed uk:headingDepth/headingSignal above; strip the rest.
        LegHeadingClassifier.StripHeadingMetadataAttributes(xml);

        SyncTotalImagesWithXml(xml);

        return new XmlDocument_ { Document = xml, Images = processedImages };
    }

    private static void MergeRenderedImages(IDocument doc, RenderSession session) {
        var rendered = session?.RenderedImages;
        if (rendered == null) return;
        var list = new List<Judgments.IImage>(rendered);
        if (list.Count == 0) return;

        var existing = doc.Images?.ToList() ?? new List<Judgments.IImage>();
        existing.AddRange(list);

        if (doc is Models.DividedDocument dd)
            dd.Images = existing;
        else if (doc is Models.UndividedDocument ud)
            ud.Images = existing;
    }

    /// <summary>
    /// Drop a leading <c>&lt;marker name="tab"/&gt;</c> from each
    /// <c>&lt;p&gt;</c> when nothing real precedes it — a hanging-indent
    /// hangover Word leaves behind that renders as a stray nbsp.
    /// </summary>
    /// <remarks>
    /// EM-only by call-site choice. Excluded contexts where a leading
    /// tab is the rendering convention, not a hangover:
    /// <list type="bullet">
    ///   <item><c>&lt;authorialNote&gt;</c> — footnote marker separator.</item>
    ///   <item><c>&lt;td&gt;</c> / <c>&lt;th&gt;</c> — cell alignment.</item>
    ///   <item><c>&lt;paragraph&gt;</c> with no <c>&lt;num&gt;</c> —
    ///   bare list items where the tab is the bullet indent.</item>
    /// </list>
    /// </remarks>
    protected static void StripLeadingTabMarkers(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        var paragraphs = xml.SelectNodes(
            "//akn:p[not(ancestor::akn:authorialNote) and not(ancestor::akn:td) and not(ancestor::akn:th) and not(ancestor::akn:paragraph[not(akn:num)])]",
            nsmgr);
        if (paragraphs == null) return;
        foreach (XmlNode p in paragraphs) {
            XmlNode firstSubstantive = null;
            foreach (XmlNode child in p.ChildNodes) {
                if (child.NodeType == XmlNodeType.Text && string.IsNullOrWhiteSpace(child.Value))
                    continue;
                firstSubstantive = child;
                break;
            }
            if (firstSubstantive is XmlElement el
                && el.LocalName == "marker"
                && el.GetAttribute("name") == "tab") {
                p.RemoveChild(el);
            }
        }
    }

    private static void SyncTotalImagesWithXml(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        nsmgr.AddNamespace("ukm", "http://www.legislation.gov.uk/namespaces/metadata");
        var totalImages = xml.SelectSingleNode("//ukm:Statistics/ukm:TotalImages", nsmgr) as XmlElement;
        if (totalImages == null) return;
        var imgs = xml.SelectNodes("//akn:img", nsmgr);
        int count = imgs?.Count ?? 0;
        totalImages.SetAttribute("Value", count.ToString());
    }

    protected abstract IDocument ParseDocument(WordprocessingDocument docx, string filename = null);

    protected virtual void ApplyDocumentSpecificProcessing(XmlDocument xml) {
    }

}

}
