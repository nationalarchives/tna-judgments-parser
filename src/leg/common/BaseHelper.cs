using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.Common {

abstract class BaseHelper {

    protected readonly LegislativeDocumentConfig Config;

    protected BaseHelper(LegislativeDocumentConfig config) {
        Config = config;
    }

    public IXmlDocument Parse(Stream docx, bool simplify = true, bool generateToc = false) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify, generateToc);
    }

    public IXmlDocument Parse(byte[] docx, bool simplify = true, bool generateToc = false) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify, generateToc);
    }

    private IXmlDocument Parse(WordprocessingDocument docx, bool simplify, bool generateToc) {
        // Set TOC generation in config
        Config.GenerateTableOfContents = generateToc;
        
        IDocument doc = ParseDocument(docx);
        
        // Generate TOC if enabled
        if (generateToc) {
            doc = GenerateTableOfContents(doc);
        }
        
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
        // TOC generation is now handled before XML building
    }

    /// <summary>
    /// Generates table of contents and creates a new document with TOC as cover page
    /// </summary>
    private IDocument GenerateTableOfContents(IDocument document) {
        var toc = TocGenerator.GenerateFromDocument(document);
        if (toc == null) {
            return document; // No TOC generated, return original document
        }

        // Create cover page with document title and TOC
        var coverPageBlocks = new List<IBlock>();
        
        // Add document title block
        var titleBlock = CreateTitleBlock(document);
        if (titleBlock != null) {
            coverPageBlocks.Add(titleBlock);
        }
        
        // Add TOC
        coverPageBlocks.Add(toc);

        // Create new document with cover page
        if (document is IDividedDocument dividedDoc) {
            return new DividedDocumentWithCoverPage {
                Meta = document.Meta,
                CoverPage = coverPageBlocks,
                Header = document.Header,
                Body = dividedDoc.Body,
                Annexes = document.Annexes,
                Images = document.Images
            };
        }

        // For undivided documents, just return original (TOC not applicable)
        return document;
    }

    /// <summary>
    /// Creates a title block for the cover page
    /// </summary>
    private IBlock CreateTitleBlock(IDocument document) {
        // Extract title from document metadata or header
        string title = ExtractDocumentTitle(document);
        if (string.IsNullOrWhiteSpace(title)) {
            return null;
        }

        return new TocLine(title) { };
    }

    /// <summary>
    /// Extracts document title from metadata or header
    /// </summary>
    private string ExtractDocumentTitle(IDocument document) {
        // Try to extract from document header first
        if (document.Header != null) {
            foreach (var block in document.Header) {
                if (block is ILine line) {
                    string text = ExtractTextFromLine(line);
                    if (!string.IsNullOrWhiteSpace(text) && 
                        (text.Contains("Act") || text.Contains("Regulation") || text.Contains("Order"))) {
                        return text.Trim();
                    }
                }
            }
        }

        // Fallback to a generic title
        return Config.DefaultDocumentType switch {
            "ExplanatoryMemorandum" => "Explanatory Memorandum",
            "ImpactAssessment" => "Impact Assessment",
            _ => "Legislative Document"
        };
    }

    /// <summary>
    /// Extracts plain text from an ILine
    /// </summary>
    private static string ExtractTextFromLine(ILine line) {
        if (line?.Contents == null) {
            return string.Empty;
        }

        var textParts = new List<string>();
        foreach (var inline in line.Contents) {
            if (inline is IFormattedText text) {
                textParts.Add(text.Text ?? string.Empty);
            }
        }

        return string.Join(" ", textParts).Trim();
    }

}

}
