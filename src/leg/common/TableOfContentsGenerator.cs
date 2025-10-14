using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.NationalArchives;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Service for generating table of contents from legislative documents
/// </summary>
internal class TableOfContentsGenerator : ITableOfContentsGenerator {
    
    private readonly ILogger<TableOfContentsGenerator> _logger;
    private readonly IDocumentTitleExtractor _titleExtractor;
    
    public TableOfContentsGenerator(
        ILogger<TableOfContentsGenerator> logger,
        IDocumentTitleExtractor titleExtractor) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _titleExtractor = titleExtractor ?? throw new ArgumentNullException(nameof(titleExtractor));
    }
    
    /// <inheritdoc />
    public TableOfContentsResult GenerateTableOfContents(IDocument document) {
        try {
            if (document is not IDividedDocument dividedDoc) {
                _logger.LogDebug("Document is not divided, skipping TOC generation");
                return TableOfContentsResult.NoSections();
            }

            var tocLines = new List<ILine>();
            int sectionNumber = 1;

            foreach (var division in dividedDoc.Body) {
                if (division is Section section) {
                    var tocLine = CreateTocLineFromSection(section, sectionNumber);
                    if (tocLine != null) {
                        tocLines.Add(tocLine);
                        sectionNumber++;
                    }
                }
            }

            if (!tocLines.Any()) {
                _logger.LogInformation("No sections found in document for TOC generation");
                return TableOfContentsResult.NoSections();
            }

            var toc = new TableOfContents { Contents = tocLines };
            _logger.LogInformation("Generated TOC with {SectionCount} sections", tocLines.Count);
            
            return TableOfContentsResult.Successful(toc, tocLines.Count);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating table of contents");
            return TableOfContentsResult.Failed($"TOC generation failed: {ex.Message}");
        }
    }
    
    /// <inheritdoc />
    public IDocument CreateDocumentWithCoverPage(IDocument document, ITableOfContents2 toc, string title) {
        if (document is not IDividedDocument dividedDoc) {
            _logger.LogWarning("Cannot create cover page for non-divided document");
            return document;
        }
        
        var coverPageBlocks = new List<IBlock>();
        
        // Add title block if provided
        if (!string.IsNullOrWhiteSpace(title)) {
            var titleBlock = CreateTitleBlock(title);
            coverPageBlocks.Add(titleBlock);
        }
        
        // Add TOC
        coverPageBlocks.Add(toc);
        
        return new DividedDocumentWithCoverPage {
            Meta = document.Meta,
            CoverPage = coverPageBlocks,
            Header = document.Header,
            Body = dividedDoc.Body,
            Annexes = document.Annexes,
            Images = document.Images
        };
    }
    
    /// <summary>
    /// Creates a TOC line from a section
    /// </summary>
    private ILine CreateTocLineFromSection(Section section, int sectionNumber) {
        if (section.Heading == null) {
            _logger.LogDebug("Skipping section {SectionNumber} - no heading", sectionNumber);
            return null;
        }

        string headingText = _titleExtractor.ExtractTextFromLine(section.Heading);
        if (string.IsNullOrWhiteSpace(headingText)) {
            _logger.LogDebug("Skipping section {SectionNumber} - empty heading text", sectionNumber);
            return null;
        }

        string tocText = $"{sectionNumber}. {headingText.Trim()}";
        return new TocLine(tocText);
    }
    
    /// <summary>
    /// Creates a title block for the cover page
    /// </summary>
    private static IBlock CreateTitleBlock(string title) {
        return new TocLine(title);
    }
}

}
