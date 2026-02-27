
using System;
using System.IO;
using System.Linq;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

class Helper : BaseHelper {

    private const string AKN_NAMESPACE = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    private static readonly Helper Instance = new Helper();

    private Helper() : base(LegislativeDocumentConfig.ForExplanatoryMemoranda()) { }

    public static new IXmlDocument Parse(Stream docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static new IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    /// <summary>
    /// Parse an Explanatory Memorandum document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document stream</param>
    /// <param name="filename">The filename (e.g., uksiem_20132911_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    /// <summary>
    /// Parse an Explanatory Memorandum document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document bytes</param>
    /// <param name="filename">The filename (e.g., uksiem_20132911_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return ExplanatoryMemoranda.Parser.Parse(docx, filename);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        GenerateTableOfContents(xml);
    }

    /// <summary>
    /// Generate table of contents from paragraphs with intro headings.
    /// Creates a toc element with tocItem entries linking to each paragraph using full URLs.
    /// Adds eId attributes to paragraphs for proper linking.
    /// </summary>
    private static void GenerateTableOfContents(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        nsmgr.AddNamespace("uk", "https://legislation.gov.uk/akn");
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) {
            logger.LogWarning("No mainBody found, skipping TOC generation");
            return;
        }

        // Get expression URI from metadata
        var expressionUri = xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRuri/@value", nsmgr)?.Value;
        if (string.IsNullOrEmpty(expressionUri)) {
            logger.LogWarning("No FRBRExpression URI found, using fragment references for TOC");
        }

        // Get EM type for the "whole document" label
        var documentMainType = xml.SelectSingleNode("//akn:proprietary/uk:documentMainType", nsmgr)?.InnerText;
        string wholeDocLabel = GetWholeDocumentLabel(documentMainType);

        // Find structural elements that should be in the TOC:
        // 1. Paragraphs with intro (older EM style)
        // 2. Sections with headings (newer EM style)
        var paragraphsWithIntro = xml.SelectNodes("//akn:mainBody/akn:paragraph[akn:intro]", nsmgr);
        var sectionsWithHeadings = xml.SelectNodes("//akn:mainBody/akn:section[akn:heading]", nsmgr);
        
        var toc = xml.CreateElement("toc", AKN_NAMESPACE);
        
        // Add "whole document" link first (always present)
        var wholeDocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        wholeDocItem.SetAttribute("href", expressionUri ?? "#doc");
        wholeDocItem.SetAttribute("level", "1");
        var wholeDocHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        wholeDocHeading.SetAttribute("name", "tocHeading");
        wholeDocHeading.InnerText = wholeDocLabel;
        wholeDocItem.AppendChild(wholeDocHeading);
        toc.AppendChild(wholeDocItem);
        
        // Add paragraph entries (for EMs with paragraph/intro structure)
        int tocNumber = 1;
        if (paragraphsWithIntro != null && paragraphsWithIntro.Count > 0) {
            foreach (XmlElement paragraph in paragraphsWithIntro) {
                AddTocItemFromParagraph(xml, toc, paragraph, expressionUri, nsmgr, tocNumber);
                tocNumber++;
            }
        }
        
        // Add section entries (for EMs with section/heading structure)
        if (sectionsWithHeadings != null && sectionsWithHeadings.Count > 0) {
            foreach (XmlElement section in sectionsWithHeadings) {
                AddTocItemFromSection(xml, toc, section, expressionUri, nsmgr, tocNumber);
                tocNumber++;
            }
        }

        // Insert TOC at the beginning of mainBody for consistency
        if (toc.HasChildNodes) {
            if (mainBody.FirstChild != null) {
                mainBody.InsertBefore(toc, mainBody.FirstChild);
            } else {
                mainBody.AppendChild(toc);
            }
            
            logger.LogInformation("Generated TOC with {Count} entries", toc.ChildNodes.Count);
        }
    }

    /// <summary>
    /// Add a TOC item from a paragraph with intro element
    /// </summary>
    private static void AddTocItemFromParagraph(XmlDocument xml, XmlElement toc, XmlElement paragraph, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        // Extract paragraph number from <num> element
        var numElement = paragraph.SelectSingleNode("akn:num", nsmgr);
        if (numElement == null) {
            return;
        }

        string numText = numElement.InnerText?.Trim();
        if (string.IsNullOrEmpty(numText)) {
            return;
        }

        // Extract number (e.g., "1" from "1." or "<b>1.</b>")
        string number = System.Text.RegularExpressions.Regex.Replace(numText, @"[^\d]", "");
        if (string.IsNullOrEmpty(number)) {
            return;
        }

        // Get heading text from intro/p
        var intro = paragraph.SelectSingleNode("akn:intro/akn:p", nsmgr);
        if (intro == null) {
            return;
        }

        string headingText = intro.InnerText?.Trim();
        if (string.IsNullOrEmpty(headingText)) {
            return;
        }

        // Truncate long headings
        if (headingText.Length > 100) {
            headingText = headingText.Substring(0, 97) + "...";
        }

        // Generate and add eId attribute if not present
        string eId = $"paragraph_{number}";
        if (!paragraph.HasAttribute("eId")) {
            paragraph.SetAttribute("eId", eId);
        }

        // Generate href
        string href;
        if (!string.IsNullOrEmpty(expressionUri)) {
            href = $"{expressionUri}/paragraph/{number}";
        } else {
            href = "#" + eId;
        }

        var tocItem = xml.CreateElement("tocItem", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        tocItem.SetAttribute("href", href);
        tocItem.SetAttribute("level", "2");
        
        var inlineHeading = xml.CreateElement("inline", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        inlineHeading.SetAttribute("name", "tocHeading");
        inlineHeading.InnerText = $"{tocNumber}. {headingText}";
        tocItem.AppendChild(inlineHeading);
        
        toc.AppendChild(tocItem);
    }

    /// <summary>
    /// Add a TOC item from a section with heading element
    /// </summary>
    private static void AddTocItemFromSection(XmlDocument xml, XmlElement toc, XmlElement section, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        // Extract section number/eId
        string eId = section.GetAttribute("eId");
        var numElement = section.SelectSingleNode("akn:num", nsmgr);
        
        if (numElement == null) {
            return;
        }

        string numText = numElement.InnerText?.Trim();
        if (string.IsNullOrEmpty(numText)) {
            return;
        }

        // Extract number (e.g., "1" from "1." or "<b>1.</b>")
        string sectionNumber = System.Text.RegularExpressions.Regex.Replace(numText, @"[^\d]", "");
        if (string.IsNullOrEmpty(sectionNumber)) {
            return;
        }

        // Get heading text
        var heading = section.SelectSingleNode("akn:heading", nsmgr);
        if (heading == null) {
            return;
        }

        string headingText = heading.InnerText?.Trim();
        if (string.IsNullOrEmpty(headingText)) {
            return;
        }

        // Truncate long headings
        if (headingText.Length > 100) {
            headingText = headingText.Substring(0, 97) + "...";
        }

        // Generate and add eId attribute if not present
        if (string.IsNullOrEmpty(eId)) {
            eId = $"section_{sectionNumber}";
            section.SetAttribute("eId", eId);
        }

        // Generate href
        string href;
        if (!string.IsNullOrEmpty(expressionUri)) {
            href = $"{expressionUri}/section/{sectionNumber}";
        } else {
            href = "#" + eId;
        }

        var tocItem = xml.CreateElement("tocItem", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        tocItem.SetAttribute("href", href);
        tocItem.SetAttribute("level", "2");
        
        var inlineHeading = xml.CreateElement("inline", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        inlineHeading.SetAttribute("name", "tocHeading");
        inlineHeading.InnerText = $"{tocNumber}. {headingText}";
        tocItem.AppendChild(inlineHeading);
        
        toc.AppendChild(tocItem);
    }

    /// <summary>
    /// Get the appropriate "whole document" label based on document type.
    /// </summary>
    private static string GetWholeDocumentLabel(string documentMainType) {
        if (string.IsNullOrEmpty(documentMainType)) {
            return "The whole Explanatory Memorandum";
        }

        string typeLower = documentMainType.ToLowerInvariant();
        
        if (typeLower.Contains("policynote")) {
            return "The whole Policy Note";
        }
        if (typeLower.Contains("executivenote")) {
            return "The whole Executive Note";
        }
        
        // Default for all memorandum types
        return "The whole Explanatory Memorandum";
    }

}

}
