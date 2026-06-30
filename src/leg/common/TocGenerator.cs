
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
    /// Walking strategies supported by <see cref="Generate"/>. Either way, no TOC is
    /// emitted when there are no structural entries.
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

    public static void Generate(XmlDocument xml, TocStrategy strategy = TocStrategy.MultiXPath) {
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

        int structuralCount = strategy switch {
            TocStrategy.BoldTitleDocumentOrder => PopulateBoldTitleInOrder(xml, toc, mainBody, expressionUri, nsmgr),
            _ => PopulateMultiXPath(xml, toc, expressionUri, nsmgr)
        };

        structuralCount += PopulateFromAttachments(xml, toc, expressionUri, nsmgr);

        // Don't emit an empty TOC; the whole-document link that used to guarantee one
        // entry is now supplied by the publisher, so unstructured content gets no TOC.
        if (structuralCount == 0)
            return;

        if (mainBody.FirstChild != null)
            mainBody.InsertBefore(toc, mainBody.FirstChild);
        else
            mainBody.AppendChild(toc);

        logger.LogInformation("Generated TOC with {Count} entries", toc.ChildNodes.Count);
    }

    private static int PopulateMultiXPath(XmlDocument xml, XmlElement toc, string expressionUri, XmlNamespaceManager nsmgr) {
        // Walk mainBody children in document order, single pass. tocNumber increments
        // per emitted entry so eIds are dense (paragraph_1, paragraph_2, ...) and
        // match the order paragraphs appear in the body.
        //
        // Historical (pre-LEG-em-toc-doc-order): three sequential XPath passes --
        // paragraphs-with-intro, sections, then paragraphs-with-num-but-no-intro --
        // sharing one tocNumber counter that incremented per iteration even on
        // skipped emits. The result was that an EM's first body paragraph (which
        // typically has a num but no <intro>, e.g. the boilerplate "This explanatory
        // memorandum has been prepared by..."  paragraph) landed in the THIRD pass
        // and ended up at the bottom of the TOC with the highest eId, while later
        // paragraphs took eIds 1..N-1. Regenerated all EM fixtures to match the
        // single-pass document order behaviour.
        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) return 0;

        int tocNumber = 1;
        int added = 0;
        foreach (XmlNode child in mainBody.ChildNodes) {
            if (child is not XmlElement el) continue;
            int before = toc.ChildNodes.Count;

            if (el.LocalName == "paragraph") {
                // Prefer the with-intro emitter; fall back to the flat (no-intro)
                // emitter for paragraphs that have a num but only <content>.
                if (el.SelectSingleNode("akn:intro/akn:p", nsmgr) != null) {
                    AddTocItemFromParagraph(xml, toc, el, expressionUri, nsmgr, tocNumber);
                } else if (el.SelectSingleNode("akn:num", nsmgr) != null) {
                    AddTocItemFromFlatParagraph(xml, toc, el, expressionUri, nsmgr, tocNumber);
                }
            } else if (el.LocalName == "section") {
                AddTocItemFromSection(xml, toc, el, expressionUri, nsmgr, tocNumber);
            }

            if (toc.ChildNodes.Count > before) {
                added++;
                tocNumber++;
            }
        }
        return added;
    }

    private static int PopulateFromAttachments(XmlDocument xml, XmlElement toc, string expressionUri, XmlNamespaceManager nsmgr) {
        var annexBodies = xml.SelectNodes("//akn:attachments/akn:attachment/akn:doc/akn:mainBody", nsmgr);
        if (annexBodies == null || annexBodies.Count == 0) return 0;
        // Continue 1-based numbering after the body entries already in the TOC.
        int nextTocNumber = toc.ChildNodes.Count + 1;
        int added = 0;
        foreach (XmlNode annexBody in annexBodies) {
            foreach (XmlNode child in annexBody.ChildNodes) {
                if (child is not XmlElement el) continue;
                if (el.LocalName != "p" || el.NamespaceURI != AKN_NAMESPACE) continue;
                if (!IsFlatBoldHeadingParagraph(el, nsmgr, out string headingText)) continue;
                EmitTocEntry(xml, toc, el, "paragraph", nextTocNumber, expressionUri, headingText);
                nextTocNumber++;
                added++;
            }
        }
        return added;
    }

    private static bool IsFlatBoldHeadingParagraph(XmlElement paragraph, XmlNamespaceManager nsmgr, out string headingText) {
        headingText = null;
        string fullText = paragraph.InnerText?.Trim() ?? "";
        if (fullText.Length < 3 || fullText.Length > 200) return false;
        var bold = paragraph.SelectSingleNode("akn:b", nsmgr);
        if (bold == null) return false;
        string boldText = bold.InnerText?.Trim() ?? "";
        if (boldText.Length < 3) return false;
        if (boldText.Length < fullText.Length - 3) return false;
        headingText = TruncateHeading(boldText.TrimEnd(':', '.', ' '));
        return !string.IsNullOrEmpty(headingText);
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
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

}

}
