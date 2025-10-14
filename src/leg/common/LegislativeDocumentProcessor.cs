using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Enhanced legislative document processor with improved separation of concerns and testability
/// </summary>
abstract class LegislativeDocumentProcessor {

    protected readonly LegislativeDocumentConfig Config;
    private readonly ITableOfContentsGenerator _tocGenerator;
    private readonly IDocumentTitleExtractor _titleExtractor;
    private readonly ILogger<LegislativeDocumentProcessor> _logger;

    protected LegislativeDocumentProcessor(
        LegislativeDocumentConfig config,
        ITableOfContentsGenerator tocGenerator,
        IDocumentTitleExtractor titleExtractor,
        ILogger<LegislativeDocumentProcessor> logger) {
        Config = config ?? throw new System.ArgumentNullException(nameof(config));
        _tocGenerator = tocGenerator ?? throw new System.ArgumentNullException(nameof(tocGenerator));
        _titleExtractor = titleExtractor ?? throw new System.ArgumentNullException(nameof(titleExtractor));
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
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
        try {
            _logger.LogDebug("Starting document parsing, TOC generation: {GenerateToc}", generateToc);
            
            // Set TOC generation in config
            Config.GenerateTableOfContents = generateToc;
            
            IDocument doc = ParseDocument(docx);
            
            // Generate TOC if enabled
            if (generateToc) {
                doc = GenerateTableOfContentsForDocument(doc);
            }
            
            XmlDocument xml = Builder.Build(doc);
            docx.Dispose();
            
            if (simplify) {
                Simplifier.Simplify(xml);
            }
            
            // Apply document-specific processing
            ApplyDocumentSpecificProcessing(xml);
            
            _logger.LogInformation("Document parsing completed successfully");
            return new XmlDocument_ { Document = xml };
        }
        catch (System.Exception ex) {
            _logger.LogError(ex, "Error during document parsing");
            throw;
        }
    }

    /// <summary>
    /// Generates table of contents for the document using the TOC service
    /// </summary>
    private IDocument GenerateTableOfContentsForDocument(IDocument document) {
        var tocResult = _tocGenerator.GenerateTableOfContents(document);
        
        if (!tocResult.Success) {
            _logger.LogInformation("TOC generation skipped: {Reason}", tocResult.ErrorMessage);
            return document;
        }

        string title = _titleExtractor.ExtractDocumentTitle(document, Config.DefaultDocumentType);
        var documentWithToc = _tocGenerator.CreateDocumentWithCoverPage(document, tocResult.TableOfContents, title);
        
        _logger.LogInformation("TOC generated successfully with {SectionCount} sections", tocResult.SectionCount);
        return documentWithToc;
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
}

/// <summary>
/// Factory for creating LegislativeDocumentProcessor instances with proper dependency injection
/// </summary>
internal static class LegislativeDocumentProcessorFactory {
    
    /// <summary>
    /// Creates a LegislativeDocumentProcessor instance with all required dependencies
    /// </summary>
    public static T Create<T>(
        LegislativeDocumentConfig config,
        ILogger<T> logger = null) where T : LegislativeDocumentProcessor {
        
        // Create default services if not provided
        var tocLogger = logger as ILogger<TableOfContentsGenerator> ?? 
                       Microsoft.Extensions.Logging.Abstractions.NullLogger<TableOfContentsGenerator>.Instance;
        var titleLogger = logger as ILogger<DocumentTitleExtractor> ?? 
                         Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentTitleExtractor>.Instance;
        var processorLogger = logger as ILogger<LegislativeDocumentProcessor> ?? 
                          Microsoft.Extensions.Logging.Abstractions.NullLogger<LegislativeDocumentProcessor>.Instance;
        
        var titleExtractor = new DocumentTitleExtractor(titleLogger);
        var tocGenerator = new TableOfContentsGenerator(tocLogger, titleExtractor);
        
        // Use reflection or factory pattern to create the specific processor type
        return (T)System.Activator.CreateInstance(typeof(T), config, tocGenerator, titleExtractor, processorLogger);
    }
}

}
