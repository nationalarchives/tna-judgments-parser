using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Service for extracting titles and text from document elements
/// </summary>
internal interface IDocumentTitleExtractor {
    
    /// <summary>
    /// Extracts plain text from an ILine
    /// </summary>
    string ExtractTextFromLine(ILine line);
    
    /// <summary>
    /// Extracts document title from metadata or header
    /// </summary>
    string ExtractDocumentTitle(IDocument document, string defaultDocumentType);
}

}
