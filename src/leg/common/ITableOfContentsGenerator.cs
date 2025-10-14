using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.NationalArchives;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Service for generating table of contents from legislative documents
/// </summary>
internal interface ITableOfContentsGenerator {
    
    /// <summary>
    /// Generates a table of contents from the document structure
    /// </summary>
    /// <param name="document">The document to generate TOC for</param>
    /// <returns>Table of contents result</returns>
    TableOfContentsResult GenerateTableOfContents(IDocument document);
    
    /// <summary>
    /// Creates a document with cover page containing the TOC
    /// </summary>
    /// <param name="document">Original document</param>
    /// <param name="toc">Generated table of contents</param>
    /// <param name="title">Document title for cover page</param>
    /// <returns>Document with cover page</returns>
    IDocument CreateDocumentWithCoverPage(IDocument document, ITableOfContents2 toc, string title);
}

/// <summary>
/// Result of table of contents generation operation
/// </summary>
internal class TableOfContentsResult {
    public bool Success { get; init; }
    public ITableOfContents2 TableOfContents { get; init; }
    public string ErrorMessage { get; init; }
    public int SectionCount { get; init; }
    
    public static TableOfContentsResult Successful(ITableOfContents2 toc, int sectionCount) =>
        new() { Success = true, TableOfContents = toc, SectionCount = sectionCount };
        
    public static TableOfContentsResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
        
    public static TableOfContentsResult NoSections() =>
        new() { Success = false, ErrorMessage = "Document contains no sections suitable for TOC generation" };
}

}
