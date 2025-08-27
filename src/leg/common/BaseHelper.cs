using System.IO;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.Common {

abstract class BaseHelper {

    protected readonly LegislativeDocumentConfig Config;

    protected BaseHelper(LegislativeDocumentConfig config) {
        Config = config;
    }

    public IXmlDocument Parse(Stream docx, bool simplify = true) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify);
    }

    public IXmlDocument Parse(byte[] docx, bool simplify = true) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify);
    }

    private IXmlDocument Parse(WordprocessingDocument docx, bool simplify) {
        IDocument doc = ParseDocument(docx);
        XmlDocument xml = Builder.Build(doc);
        docx.Dispose();
        if (simplify)
            Simplifier.Simplify(xml);
        
        // Apply document-specific processing
        ApplyDocumentSpecificProcessing(xml);
        
        return new XmlDocument_ { Document = xml };
    }

    /// <summary>
    /// Parse the document using the appropriate parser for this document type.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract IDocument ParseDocument(WordprocessingDocument docx);

    /// <summary>
    /// Apply document-specific processing to the XML.
    /// Can be overridden by derived classes for custom processing.
    /// </summary>
    protected virtual void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        // Default implementation does nothing
    }

}

}
