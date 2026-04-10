
using System.Text.RegularExpressions;
using System.Xml;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Generates a table of contents from structural elements in a parsed legislative
/// document. Works on the common shapes produced by BaseLegislativeDocumentParser:
/// paragraphs with intro, sections with headings, and flat numbered paragraphs.
/// </summary>
internal static class TocGenerator {

    private const string AKN_NAMESPACE = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
    private const string UKM_NAMESPACE = "http://www.legislation.gov.uk/namespaces/metadata";

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(TocGenerator));

    /// <summary>
    /// Generate and inject a TOC into the given document's mainBody.
    /// </summary>
    /// <param name="xml">The parsed AKN document to mutate</param>
    /// <param name="wholeDocumentLabel">Label for the "whole document" TOC item (e.g. "The whole Explanatory Memorandum")</param>
    /// <param name="skipIfNoStructuralEntries">If true, do not emit a TOC when no structural entries are found. EM keeps its historical behaviour (false); CoP/OD use true to avoid emitting a single-item TOC on unstructured docs.</param>
    /// <param name="strategy">Which walking strategy to use. MultiXPath matches EM's historical behaviour: three separate XPath passes, sequential numbering, no shape filtering on titles. BoldTitleDocumentOrder (for CoP/OD) walks mainBody children in document order, accepting only top-level-integer-nummed paragraphs whose content is a single bold phrase — giving clean in-order TOCs on docs with deep sub-numbered content.</param>
    public static void Generate(XmlDocument xml, string wholeDocumentLabel, bool skipIfNoStructuralEntries = false, TocStrategy strategy = TocStrategy.MultiXPath) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        nsmgr.AddNamespace("ukm", UKM_NAMESPACE);

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) {
            logger.LogWarning("No mainBody found, skipping TOC generation");
            return;
        }

        // Strip any pre-existing TOC(s) copied from the source docx. We always own
        // TOC generation — the parser must not preserve Word-generated tables of
        // contents from the original document, even when they exist. Some fixtures
        // nest the native TOC deep inside mainBody/paragraph/content/toc, so use a
        // descendant-axis query and detach from each toc's actual parent.
        var preExistingTocs = mainBody.SelectNodes(".//akn:toc", nsmgr);
        if (preExistingTocs != null && preExistingTocs.Count > 0) {
            foreach (XmlNode existing in preExistingTocs)
                existing.ParentNode?.RemoveChild(existing);
            logger.LogInformation("Removed {Count} pre-existing <toc> element(s) from mainBody before generating", preExistingTocs.Count);
        }

        var expressionUri = xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRuri/@value", nsmgr)?.Value;
        if (string.IsNullOrEmpty(expressionUri)) {
            logger.LogWarning("No FRBRExpression URI found, using fragment references for TOC");
        }

        var toc = xml.CreateElement("toc", AKN_NAMESPACE);

        var wholeDocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        wholeDocItem.SetAttribute("href", expressionUri ?? "#doc");
        wholeDocItem.SetAttribute("level", "1");
        var wholeDocHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        wholeDocHeading.SetAttribute("name", "tocHeading");
        wholeDocHeading.InnerText = wholeDocumentLabel;
        wholeDocItem.AppendChild(wholeDocHeading);
        toc.AppendChild(wholeDocItem);

        int structuralCount = strategy switch {
            TocStrategy.BoldTitleDocumentOrder => PopulateBoldTitleInOrder(xml, toc, mainBody, expressionUri, nsmgr),
            _ => PopulateMultiXPath(xml, toc, expressionUri, nsmgr)
        };

        if (skipIfNoStructuralEntries && structuralCount == 0) {
            logger.LogInformation("No structural TOC candidates found; skipping TOC emission");
            return;
        }

        if (!toc.HasChildNodes)
            return;

        if (mainBody.FirstChild != null)
            mainBody.InsertBefore(toc, mainBody.FirstChild);
        else
            mainBody.AppendChild(toc);

        logger.LogInformation("Generated TOC with {Count} entries", toc.ChildNodes.Count);
    }

    /// <summary>
    /// Walking strategies supported by <see cref="Generate"/>.
    /// </summary>
    public enum TocStrategy {
        /// <summary>
        /// EM's historical three-pass XPath walk: paragraphs with intro, sections with headings,
        /// flat paragraphs with num. Preserves existing EM fixture output byte-identically.
        /// </summary>
        MultiXPath,
        /// <summary>
        /// Walk mainBody children in document order; accept paragraphs whose num is a
        /// top-level integer and whose first content/p is a single bold phrase.
        /// Used by CoP/OD where the multi-XPath approach floods the TOC with sub-paragraphs.
        /// </summary>
        BoldTitleDocumentOrder
    }

    private static int PopulateMultiXPath(XmlDocument xml, XmlElement toc, string expressionUri, XmlNamespaceManager nsmgr) {
        var paragraphsWithIntro = xml.SelectNodes("//akn:mainBody/akn:paragraph[akn:intro]", nsmgr);
        var sectionsWithHeadings = xml.SelectNodes("//akn:mainBody/akn:section[akn:heading]", nsmgr);
        var paragraphsWithNumNoIntro = xml.SelectNodes("//akn:mainBody/akn:paragraph[akn:num and not(akn:intro)]", nsmgr);

        // Historical EM behaviour: tocNumber always increments per loop iteration,
        // even when an Add method returns early (skipped entries still consume a number).
        // Preserved so pre-existing EM fixtures round-trip byte-identical.
        int tocNumber = 1;
        int added = 0;
        int beforeCount;

        if (paragraphsWithIntro != null) {
            foreach (XmlElement paragraph in paragraphsWithIntro) {
                beforeCount = toc.ChildNodes.Count;
                AddTocItemFromParagraph(xml, toc, paragraph, expressionUri, nsmgr, tocNumber);
                if (toc.ChildNodes.Count > beforeCount) added++;
                tocNumber++;
            }
        }

        if (sectionsWithHeadings != null) {
            foreach (XmlElement section in sectionsWithHeadings) {
                beforeCount = toc.ChildNodes.Count;
                AddTocItemFromSection(xml, toc, section, expressionUri, nsmgr, tocNumber);
                if (toc.ChildNodes.Count > beforeCount) added++;
                tocNumber++;
            }
        }

        if (paragraphsWithNumNoIntro != null) {
            foreach (XmlElement paragraph in paragraphsWithNumNoIntro) {
                beforeCount = toc.ChildNodes.Count;
                AddTocItemFromFlatParagraph(xml, toc, paragraph, expressionUri, nsmgr, tocNumber);
                if (toc.ChildNodes.Count > beforeCount) added++;
                tocNumber++;
            }
        }

        return added;
    }

    /// <summary>
    /// Document-order walk accepting only paragraphs whose num is a top-level integer
    /// ("1", "1.", "42") and whose content/p[1] consists of a single bold phrase.
    /// Produces clean, in-order TOCs on documents where many sub-paragraphs share mainBody
    /// top level alongside the real section titles.
    /// </summary>
    private static int PopulateBoldTitleInOrder(XmlDocument xml, XmlElement toc, XmlNode mainBody, string expressionUri, XmlNamespaceManager nsmgr) {
        int tocNumber = 1;
        int added = 0;
        foreach (XmlNode child in mainBody.ChildNodes) {
            if (child is not XmlElement paragraph)
                continue;
            if (paragraph.LocalName != "paragraph")
                continue;
            if (!HasTopLevelIntegerNum(paragraph, nsmgr))
                continue;
            if (!IsBoldOnlyTitleParagraph(paragraph, nsmgr, out string headingText))
                continue;

            string numText = paragraph.SelectSingleNode("akn:num", nsmgr)?.InnerText?.Trim();
            var numMatch = Regex.Match(numText ?? "", @"\d+");
            if (!numMatch.Success) continue;
            string number = numMatch.Value;

            string eId = $"paragraph_{number}";
            if (!paragraph.HasAttribute("eId"))
                paragraph.SetAttribute("eId", eId);

            string href = !string.IsNullOrEmpty(expressionUri)
                ? $"{expressionUri}/paragraph/{number}"
                : "#" + eId;

            AddTocItem(xml, toc, href, tocNumber, TruncateHeading(headingText));
            tocNumber++;
            added++;
        }
        return added;
    }

    /// <summary>
    /// True if the paragraph's first content/p consists of a single bold run whose
    /// text is the full content (modulo whitespace). Used to distinguish real section
    /// titles from body text that happens to start with a bold phrase.
    /// </summary>
    private static bool IsBoldOnlyTitleParagraph(XmlElement paragraph, XmlNamespaceManager nsmgr, out string headingText) {
        headingText = null;
        var content = paragraph.SelectSingleNode("akn:content", nsmgr);
        if (content == null)
            return false;
        var firstP = content.SelectSingleNode("akn:p", nsmgr) as XmlElement;
        if (firstP == null)
            return false;

        string fullText = firstP.InnerText?.Trim() ?? "";
        if (fullText.Length == 0)
            return false;

        var bold = firstP.SelectSingleNode("akn:b", nsmgr);
        if (bold == null)
            return false;
        string boldText = bold.InnerText?.Trim() ?? "";
        if (boldText.Length < 3)
            return false;

        // Require the bold run to cover essentially the whole p — allow trailing punctuation/spaces.
        if (boldText.Length < fullText.Length - 3)
            return false;

        headingText = boldText;
        return true;
    }

    private static void AddTocItemFromParagraph(XmlDocument xml, XmlElement toc, XmlElement paragraph, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        var numElement = paragraph.SelectSingleNode("akn:num", nsmgr);
        if (numElement == null)
            return;

        string numText = numElement.InnerText?.Trim();
        if (string.IsNullOrEmpty(numText))
            return;

        var numMatch = Regex.Match(numText, @"\d+");
        if (!numMatch.Success)
            return;
        string number = numMatch.Value;

        var intro = paragraph.SelectSingleNode("akn:intro/akn:p", nsmgr);
        if (intro == null)
            return;

        string headingText = ExtractHeadingText(intro, nsmgr);
        if (string.IsNullOrEmpty(headingText))
            return;

        string eId = $"paragraph_{number}";
        if (!paragraph.HasAttribute("eId"))
            paragraph.SetAttribute("eId", eId);

        string href = !string.IsNullOrEmpty(expressionUri)
            ? $"{expressionUri}/paragraph/{number}"
            : "#" + eId;

        AddTocItem(xml, toc, href, tocNumber, headingText);
    }

    private static void AddTocItemFromSection(XmlDocument xml, XmlElement toc, XmlElement section, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        string eId = section.GetAttribute("eId");
        var numElement = section.SelectSingleNode("akn:num", nsmgr);
        if (numElement == null)
            return;

        string numText = numElement.InnerText?.Trim();
        if (string.IsNullOrEmpty(numText))
            return;

        var numMatch = Regex.Match(numText, @"\d+");
        if (!numMatch.Success)
            return;
        string sectionNumber = numMatch.Value;

        var heading = section.SelectSingleNode("akn:heading", nsmgr);
        if (heading == null)
            return;

        string headingText = heading.InnerText?.Trim();
        if (string.IsNullOrEmpty(headingText))
            return;

        headingText = TruncateHeading(headingText);

        if (string.IsNullOrEmpty(eId)) {
            eId = $"section_{sectionNumber}";
            section.SetAttribute("eId", eId);
        }

        string href = !string.IsNullOrEmpty(expressionUri)
            ? $"{expressionUri}/section/{sectionNumber}"
            : "#" + eId;

        AddTocItem(xml, toc, href, tocNumber, headingText);
    }

    private static void AddTocItemFromFlatParagraph(XmlDocument xml, XmlElement toc, XmlElement paragraph, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        var numElement = paragraph.SelectSingleNode("akn:num", nsmgr);
        if (numElement == null)
            return;

        string numText = numElement.InnerText?.Trim();
        if (string.IsNullOrEmpty(numText))
            return;

        var numMatch = Regex.Match(numText, @"\d+");
        if (!numMatch.Success)
            return;
        string number = numMatch.Value;

        var content = paragraph.SelectSingleNode("akn:content", nsmgr);
        if (content == null)
            return;

        var firstP = content.SelectSingleNode("akn:p", nsmgr);
        if (firstP == null)
            return;

        string headingText = ExtractHeadingText(firstP, nsmgr);
        if (string.IsNullOrEmpty(headingText))
            return;

        string eId = $"paragraph_{number}";
        if (!paragraph.HasAttribute("eId"))
            paragraph.SetAttribute("eId", eId);

        string href = !string.IsNullOrEmpty(expressionUri)
            ? $"{expressionUri}/paragraph/{number}"
            : "#" + eId;

        AddTocItem(xml, toc, href, tocNumber, headingText);
    }

    private static string ExtractHeadingText(XmlNode paragraph, XmlNamespaceManager nsmgr) {
        var boldElement = paragraph.SelectSingleNode(".//akn:b", nsmgr);
        if (boldElement != null) {
            string boldText = boldElement.InnerText?.Trim();
            if (!string.IsNullOrEmpty(boldText) && boldText.Length > 3)
                return TruncateHeading(boldText);
        }

        string fullText = paragraph.InnerText?.Trim();
        if (string.IsNullOrEmpty(fullText))
            return null;

        return TruncateHeading(fullText);
    }

    /// <summary>
    /// Whether a paragraph's num is a top-level integer marker — either a bare integer
    /// ("1", "1.", "42") or a word-prefixed integer ("Chapter 1.", "Part 2", "Section 3.")
    /// — and NOT a sub-num like "1.1" or "Chapter 1.2". Used to avoid flooding CoP/OD
    /// TOCs with every sub-paragraph when the real section titles are the integer-nummed ones.
    /// </summary>
    private static bool HasTopLevelIntegerNum(XmlElement paragraph, XmlNamespaceManager nsmgr) {
        var numElement = paragraph.SelectSingleNode("akn:num", nsmgr);
        if (numElement == null)
            return false;
        string numText = numElement.InnerText?.Trim();
        if (string.IsNullOrEmpty(numText))
            return false;
        return Regex.IsMatch(numText, @"^(?:[A-Za-z]+\s+)?\d+\.?$");
    }

    private static string TruncateHeading(string text) {
        if (string.IsNullOrEmpty(text)) return text;

        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (text.Length <= 100)
            return text;

        int lastSpace = text.LastIndexOf(' ', 97);
        if (lastSpace > 20)
            return text.Substring(0, lastSpace) + "...";

        return text.Substring(0, 97) + "...";
    }

    private static void AddTocItem(XmlDocument xml, XmlElement toc, string href, int tocNumber, string headingText) {
        var tocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        tocItem.SetAttribute("href", href);
        tocItem.SetAttribute("level", "2");

        var inlineHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        inlineHeading.SetAttribute("name", "tocHeading");
        inlineHeading.InnerText = $"{tocNumber}. {headingText}";
        tocItem.AppendChild(inlineHeading);

        toc.AppendChild(tocItem);
    }

}

}
