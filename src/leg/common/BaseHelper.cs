using System.Collections.Generic;
using System.IO;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

abstract class BaseHelper {

    protected readonly LegislativeDocumentConfig Config;

    protected BaseHelper(LegislativeDocumentConfig config) {
        Config = config;
    }

    public IXmlDocument Parse(Stream docx, bool simplify = true, string filename = null, string manifestationName = Builder.DefaultManifestationName) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify, filename, manifestationName);
    }

    public IXmlDocument Parse(byte[] docx, bool simplify = true, string filename = null, string manifestationName = Builder.DefaultManifestationName) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify, filename, manifestationName);
    }

    private IXmlDocument Parse(WordprocessingDocument docx, bool simplify, string filename, string manifestationName) {
        IDocument doc = ParseDocument(docx, filename);

        IEnumerable<Judgments.IImage> processedImages = LegImageProcessor.ProcessImages(doc);

        doc.Meta.Statistics = StatisticsCalculator.Calculate(doc);

        XmlDocument xml = Builder.Build(doc, manifestationName);
        docx.Dispose();
        if (simplify)
            Simplifier.Simplify(xml);

        ApplyDocumentSpecificProcessing(xml);

        SyncTotalImagesWithXml(xml);

        return new XmlDocument_ { Document = xml, Images = processedImages };
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
