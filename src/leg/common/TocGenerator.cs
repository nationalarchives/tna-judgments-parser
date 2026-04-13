
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
    /// Walking strategies supported by <see cref="Generate"/>. BoldTitleDocumentOrder
    /// also implies "skip if no structural entries found" (produces no TOC on
    /// unstructured content rather than a lone whole-document link).
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

    public static void Generate(XmlDocument xml, string wholeDocumentLabel, TocStrategy strategy = TocStrategy.MultiXPath) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        nsmgr.AddNamespace("ukm", UKM_NAMESPACE);

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) {
            logger.LogWarning("No mainBody found, skipping TOC generation");
            return;
        }

        // Always strip pre-existing TOCs from the source docx — the parser must own
        // TOC generation so hrefs stay keyed to AKN structural elements rather than
        // Word page numbers. Some fixtures nest the native TOC inside
        // mainBody/paragraph/content/toc, so use the descendant axis.
        foreach (XmlNode existing in mainBody.SelectNodes(".//akn:toc", nsmgr))
            existing.ParentNode?.RemoveChild(existing);

        var expressionUri = xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRuri/@value", nsmgr)?.Value;
        if (string.IsNullOrEmpty(expressionUri))
            logger.LogWarning("No FRBRExpression URI found, using fragment references for TOC");

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

        // BoldTitleDocumentOrder callers (CoP/OD/TN) don't want a TOC that's just the
        // whole-document link on unstructured content; MultiXPath (EM) historically always emits.
        if (strategy == TocStrategy.BoldTitleDocumentOrder && structuralCount == 0)
            return;

        if (mainBody.FirstChild != null)
            mainBody.InsertBefore(toc, mainBody.FirstChild);
        else
            mainBody.AppendChild(toc);

        logger.LogInformation("Generated TOC with {Count} entries", toc.ChildNodes.Count);
    }

    private static int PopulateMultiXPath(XmlDocument xml, XmlElement toc, string expressionUri, XmlNamespaceManager nsmgr) {
        // Historical EM behaviour: tocNumber always increments per loop iteration, even
        // when an Add method returns early (skipped entries still consume a number).
        // Preserved so pre-existing EM fixtures round-trip byte-identical.
        int tocNumber = 1;
        int added = 0;

        added += WalkAndEmit(xml, toc, "//akn:mainBody/akn:paragraph[akn:intro]", nsmgr, ref tocNumber,
            (p, n) => AddTocItemFromParagraph(xml, toc, p, expressionUri, nsmgr, n));

        added += WalkAndEmit(xml, toc, "//akn:mainBody/akn:section[akn:heading]", nsmgr, ref tocNumber,
            (s, n) => AddTocItemFromSection(xml, toc, s, expressionUri, nsmgr, n));

        added += WalkAndEmit(xml, toc, "//akn:mainBody/akn:paragraph[akn:num and not(akn:intro)]", nsmgr, ref tocNumber,
            (p, n) => AddTocItemFromFlatParagraph(xml, toc, p, expressionUri, nsmgr, n));

        return added;
    }

    private delegate void TocEmitter(XmlElement element, int tocNumber);

    private static int WalkAndEmit(XmlDocument xml, XmlElement toc, string xpath, XmlNamespaceManager nsmgr, ref int tocNumber, TocEmitter emit) {
        var nodes = xml.SelectNodes(xpath, nsmgr);
        if (nodes == null) return 0;
        int added = 0;
        foreach (XmlElement element in nodes) {
            int before = toc.ChildNodes.Count;
            emit(element, tocNumber);
            if (toc.ChildNodes.Count > before) added++;
            tocNumber++;
        }
        return added;
    }

    private static int PopulateBoldTitleInOrder(XmlDocument xml, XmlElement toc, XmlNode mainBody, string expressionUri, XmlNamespaceManager nsmgr) {
        int tocNumber = 1;
        int added = 0;
        foreach (XmlNode child in mainBody.ChildNodes) {
            if (child is not XmlElement paragraph || paragraph.LocalName != "paragraph")
                continue;
            if (!HasTopLevelIntegerNum(paragraph, nsmgr))
                continue;
            if (!IsBoldOnlyTitleParagraph(paragraph, nsmgr, out string headingText))
                continue;

            EmitTocEntry(xml, toc, paragraph, "paragraph", tocNumber, expressionUri, TruncateHeading(headingText));
            tocNumber++;
            added++;
        }
        return added;
    }

    private static void AddTocItemFromParagraph(XmlDocument xml, XmlElement toc, XmlElement paragraph, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        if (!HasParseableNum(paragraph, nsmgr))
            return;

        var intro = paragraph.SelectSingleNode("akn:intro/akn:p", nsmgr);
        if (intro == null)
            return;

        string headingText = ExtractHeadingText(intro, nsmgr);
        if (string.IsNullOrEmpty(headingText))
            return;

        EmitTocEntry(xml, toc, paragraph, "paragraph", tocNumber, expressionUri, headingText);
    }

    private static void AddTocItemFromSection(XmlDocument xml, XmlElement toc, XmlElement section, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        if (!HasParseableNum(section, nsmgr))
            return;

        var heading = section.SelectSingleNode("akn:heading", nsmgr);
        if (heading == null)
            return;

        string headingText = heading.InnerText?.Trim();
        if (string.IsNullOrEmpty(headingText))
            return;

        EmitTocEntry(xml, toc, section, "section", tocNumber, expressionUri, TruncateHeading(headingText));
    }

    private static void AddTocItemFromFlatParagraph(XmlDocument xml, XmlElement toc, XmlElement paragraph, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        if (!HasParseableNum(paragraph, nsmgr))
            return;

        var firstP = paragraph.SelectSingleNode("akn:content/akn:p", nsmgr);
        if (firstP == null)
            return;

        string headingText = ExtractHeadingText(firstP, nsmgr);
        if (string.IsNullOrEmpty(headingText))
            return;

        EmitTocEntry(xml, toc, paragraph, "paragraph", tocNumber, expressionUri, headingText);
    }

    /// <summary>
    /// Assigns an eId on <paramref name="target"/> (unless one is already present),
    /// builds an href pointing at it, and appends a tocItem entry. eIds are numbered
    /// by TOC position rather than by the document's own num text — some source docs
    /// restart numbering per part ("Part A: 1., 2., 3.; Part B: 1., 2."), and the
    /// AKN 3.0 `eId-doc` uniqueness key rejects collisions.
    /// </summary>
    private static void EmitTocEntry(XmlDocument xml, XmlElement toc, XmlElement target, string kind, int tocNumber, string expressionUri, string headingText) {
        string eId = target.GetAttribute("eId");
        if (string.IsNullOrEmpty(eId)) {
            eId = $"{kind}_{tocNumber}";
            target.SetAttribute("eId", eId);
        }

        string href = !string.IsNullOrEmpty(expressionUri)
            ? $"{expressionUri}/{kind}/{tocNumber}"
            : "#" + eId;

        var tocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        tocItem.SetAttribute("href", href);
        tocItem.SetAttribute("level", "2");

        var inlineHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        inlineHeading.SetAttribute("name", "tocHeading");
        inlineHeading.InnerText = $"{tocNumber}. {headingText}";
        tocItem.AppendChild(inlineHeading);

        toc.AppendChild(tocItem);
    }

    private static bool IsBoldOnlyTitleParagraph(XmlElement paragraph, XmlNamespaceManager nsmgr, out string headingText) {
        headingText = null;
        var firstP = paragraph.SelectSingleNode("akn:content/akn:p", nsmgr) as XmlElement;
        if (firstP == null)
            return false;

        string fullText = firstP.InnerText?.Trim() ?? "";
        if (fullText.Length == 0)
            return false;

        string boldText = firstP.SelectSingleNode("akn:b", nsmgr)?.InnerText?.Trim() ?? "";
        if (boldText.Length < 3)
            return false;

        // Require the bold run to cover essentially the whole p — allow trailing punctuation/spaces.
        if (boldText.Length < fullText.Length - 3)
            return false;

        headingText = boldText;
        return true;
    }

    private static string ExtractHeadingText(XmlNode paragraph, XmlNamespaceManager nsmgr) {
        var boldElement = paragraph.SelectSingleNode(".//akn:b", nsmgr);
        if (boldElement != null) {
            string boldText = boldElement.InnerText?.Trim();
            if (!string.IsNullOrEmpty(boldText) && boldText.Length > 3)
                return TruncateHeading(boldText);
        }

        string fullText = paragraph.InnerText?.Trim();
        return string.IsNullOrEmpty(fullText) ? null : TruncateHeading(fullText);
    }

    /// <summary>
    /// Accepts bare integers ("1", "1.", "42") or word-prefixed integers
    /// ("Chapter 1.", "Part 2", "Section 3.") but rejects sub-nums like "1.1".
    /// Keeps BoldTitleDocumentOrder TOCs to real section titles rather than every
    /// sub-paragraph at mainBody top level.
    /// </summary>
    private static bool HasTopLevelIntegerNum(XmlElement paragraph, XmlNamespaceManager nsmgr) {
        string numText = paragraph.SelectSingleNode("akn:num", nsmgr)?.InnerText?.Trim();
        return !string.IsNullOrEmpty(numText)
            && Regex.IsMatch(numText, @"^(?:[A-Za-z]+\s+)?\d+\.?$");
    }

    private static bool HasParseableNum(XmlElement element, XmlNamespaceManager nsmgr) {
        string numText = element.SelectSingleNode("akn:num", nsmgr)?.InnerText?.Trim();
        return !string.IsNullOrEmpty(numText) && Regex.IsMatch(numText, @"\d");
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

}

}
