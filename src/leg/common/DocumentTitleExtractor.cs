using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Service for extracting titles and text from document elements
/// </summary>
internal class DocumentTitleExtractor : IDocumentTitleExtractor {
    
    private readonly ILogger<DocumentTitleExtractor> _logger;
    
    public DocumentTitleExtractor(ILogger<DocumentTitleExtractor> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public string ExtractTextFromLine(ILine line) {
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
    
    /// <inheritdoc />
    public string ExtractDocumentTitle(IDocument document, string defaultDocumentType) {
        try {
            // Try to extract from document header first
            if (document.Header != null) {
                foreach (var block in document.Header) {
                    if (block is ILine line) {
                        string text = ExtractTextFromLine(line);
                        if (!string.IsNullOrWhiteSpace(text) && 
                            (text.Contains("Act", StringComparison.OrdinalIgnoreCase) || 
                             text.Contains("Regulation", StringComparison.OrdinalIgnoreCase) || 
                             text.Contains("Order", StringComparison.OrdinalIgnoreCase))) {
                            return text.Trim();
                        }
                    }
                }
            }

            // Fallback to default titles based on document type
            return defaultDocumentType switch {
                "ExplanatoryMemorandum" => TableOfContentsConstants.DefaultTitles.ExplanatoryMemorandum,
                "ImpactAssessment" => TableOfContentsConstants.DefaultTitles.ImpactAssessment,
                _ => TableOfContentsConstants.DefaultTitles.LegislativeDocument
            };
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error extracting document title, using fallback");
            return TableOfContentsConstants.DefaultTitles.LegislativeDocument;
        }
    }
}

}
