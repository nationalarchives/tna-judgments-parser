using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.NationalArchives;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Generates table of contents from legislative document structure
/// </summary>
public static class TocGenerator {

    /// <summary>
    /// Generates a table of contents from the document's section structure
    /// </summary>
    /// <param name="document">The parsed document</param>
    /// <returns>Table of contents or null if document has no sections</returns>
    internal static ITableOfContents2 GenerateFromDocument(IDocument document) {
        if (document is not IDividedDocument dividedDoc) {
            return null; // Only generate TOC for documents with divisions/sections
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
            return null; // No sections found
        }

        return new TableOfContents { Contents = tocLines };
    }

    /// <summary>
    /// Creates a TOC line from a section
    /// </summary>
    private static ILine CreateTocLineFromSection(Section section, int sectionNumber) {
        if (section.Heading == null) {
            return null; // Skip sections without headings
        }

        // Extract the heading text
        string headingText = ExtractTextFromLine(section.Heading);
        if (string.IsNullOrWhiteSpace(headingText)) {
            return null;
        }

        // Create TOC entry: "1. Section Title"
        string tocText = $"{sectionNumber}. {headingText.Trim()}";
        
        // Create a simple TOC line
        return new TocLine(tocText);
    }

    /// <summary>
    /// Extracts plain text from an ILine (heading)
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
            // Skip other inline types like tabs, images, etc. for TOC
        }

        return string.Join(' ', textParts).Trim();
    }
}


}
