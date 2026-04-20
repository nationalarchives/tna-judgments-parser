
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.ExplanatoryNotes {

class Helper : BaseHelper {

    private const string AKN_NAMESPACE = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    private static readonly Helper Instance = new Helper();

    private Helper() : base(LegislativeDocumentConfig.ForExplanatoryNotes()) { }

    public static new IXmlDocument Parse(Stream docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static new IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName);
    }

    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return ExplanatoryNotes.Parser.Parse(docx, filename);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        GenerateTableOfContents(xml);
    }

    private static void GenerateTableOfContents(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        nsmgr.AddNamespace("ukm", "http://www.legislation.gov.uk/namespaces/metadata");
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) {
            logger.LogWarning("No mainBody found, skipping TOC generation");
            return;
        }

        // Remove any document-parsed TOCs (they have no real hrefs)
        var existingTocs = xml.SelectNodes("//akn:toc", nsmgr);
        foreach (XmlElement existing in existingTocs) {
            existing.ParentNode.RemoveChild(existing);
        }

        // Remove legacy Word TOC placeholder paragraphs. These arise when the source
        // docx contains a native TOC rendered as a table: the parser strips the data
        // rows but leaves a <paragraph> with "Table of Contents" heading and an empty
        // table (header row only). Detect by a leading paragraph text of "Table of
        // Contents" followed by a table child.
        foreach (XmlNode child in mainBody.ChildNodes.Cast<XmlNode>().ToList()) {
            if (child is not XmlElement para || para.LocalName != "paragraph") continue;
            var contentNode = para.SelectSingleNode("akn:content", nsmgr);
            if (contentNode == null) continue;
            var firstP = contentNode.SelectSingleNode("akn:p", nsmgr);
            if (firstP == null) continue;
            string firstPText = firstP.InnerText?.Trim();
            if (!string.Equals(firstPText, "Table of Contents", StringComparison.OrdinalIgnoreCase))
                continue;
            if (contentNode.SelectSingleNode("akn:table", nsmgr) == null) continue;
            mainBody.RemoveChild(para);
            logger.LogDebug("Removed legacy Word TOC placeholder paragraph");
        }

        var expressionUri = xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRuri/@value", nsmgr)?.Value;
        if (string.IsNullOrEmpty(expressionUri)) {
            logger.LogWarning("No FRBRExpression URI found, using fragment references for TOC");
        }

        var toc = xml.CreateElement("toc", AKN_NAMESPACE);

        // Add "whole document" link first
        var wholeDocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        wholeDocItem.SetAttribute("href", expressionUri ?? "#doc");
        wholeDocItem.SetAttribute("level", "1");
        var wholeDocHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        wholeDocHeading.SetAttribute("name", "tocHeading");
        wholeDocHeading.InnerText = "The whole Explanatory Note";
        wholeDocItem.AppendChild(wholeDocHeading);
        toc.AppendChild(wholeDocItem);

        // Walk direct children of mainBody in document order
        int sectionCounter = 0;
        foreach (XmlNode child in mainBody.ChildNodes) {
            if (child is not XmlElement element) continue;

            if (element.LocalName == "section") {
                sectionCounter++;
                AddSectionToToc(xml, toc, element, expressionUri, nsmgr, sectionCounter);
            } else if (element.LocalName == "paragraph") {
                AddParagraphToToc(xml, toc, element, expressionUri, nsmgr);
            }
        }

        var annexBodies = xml.SelectNodes("//akn:attachments/akn:attachment/akn:doc/akn:mainBody", nsmgr);
        if (annexBodies != null) {
            int annexIndex = 0;
            foreach (XmlNode annexBody in annexBodies) {
                annexIndex++;
                foreach (XmlNode child in annexBody.ChildNodes) {
                    if (child is not XmlElement el) continue;
                    if (el.LocalName == "section") {
                        sectionCounter++;
                        AddSectionToToc(xml, toc, el, expressionUri, nsmgr, sectionCounter);
                    } else if (el.LocalName == "paragraph") {
                        AddParagraphToToc(xml, toc, el, expressionUri, nsmgr);
                    } else if (el.LocalName == "p"
                               && IsFlatBoldHeadingParagraph(el, nsmgr, out string headingText)) {
                        sectionCounter++;
                        string eId = el.GetAttribute("eId");
                        if (string.IsNullOrEmpty(eId)) {
                            eId = $"annex_{annexIndex}_p_{sectionCounter}";
                            el.SetAttribute("eId", eId);
                        }
                        string href = !string.IsNullOrEmpty(expressionUri)
                            ? $"{expressionUri}#{eId}"
                            : "#" + eId;
                        var tocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
                        tocItem.SetAttribute("href", href);
                        tocItem.SetAttribute("level", "2");
                        var inlineHeading = xml.CreateElement("inline", AKN_NAMESPACE);
                        inlineHeading.SetAttribute("name", "tocHeading");
                        inlineHeading.InnerText = $"{sectionCounter}. {headingText}";
                        tocItem.AppendChild(inlineHeading);
                        toc.AppendChild(tocItem);
                    }
                }
            }
        }

        if (toc.HasChildNodes) {
            if (mainBody.FirstChild != null) {
                mainBody.InsertBefore(toc, mainBody.FirstChild);
            } else {
                mainBody.AppendChild(toc);
            }
            logger.LogInformation("Generated TOC with {Count} entries", toc.ChildNodes.Count);
        }
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
        string trimmed = boldText.TrimEnd(':', '.', ' ');
        if (string.IsNullOrEmpty(trimmed)) return false;
        headingText = trimmed.Length > 120 ? trimmed.Substring(0, 117) + "..." : trimmed;
        return true;
    }

    private static void AddSectionToToc(XmlDocument xml, XmlElement toc, XmlElement section, string expressionUri, XmlNamespaceManager nsmgr, int sectionNumber) {
        var heading = section.SelectSingleNode("akn:heading", nsmgr);
        if (heading == null) return;

        string headingText = heading.InnerText?.Trim();
        if (string.IsNullOrEmpty(headingText)) return;

        // Assign eId — use <num> if present, otherwise use sequential number
        string eId = section.GetAttribute("eId");
        if (string.IsNullOrEmpty(eId)) {
            var numElement = section.SelectSingleNode("akn:num", nsmgr);
            if (numElement != null) {
                string num = Regex.Replace(numElement.InnerText?.Trim() ?? "", @"[^\d]", "");
                if (!string.IsNullOrEmpty(num))
                    eId = $"section_{num}";
            }
            if (string.IsNullOrEmpty(eId))
                eId = $"section_{sectionNumber}";
            section.SetAttribute("eId", eId);
        }

        if (headingText.Length > 100)
            headingText = headingText.Substring(0, 97) + "...";

        string href = !string.IsNullOrEmpty(expressionUri)
            ? $"{expressionUri}/section/{sectionNumber}"
            : "#" + eId;

        AddTocItem(xml, toc, href, headingText, "2");

        // Add nested level headings (e.g. "Commentary on provisions" sub-sections)
        var nestedLevels = section.SelectNodes("akn:level[akn:heading]", nsmgr);
        if (nestedLevels != null) {
            foreach (XmlElement level in nestedLevels) {
                AddLevelToToc(xml, toc, level, expressionUri, nsmgr, eId);
            }
        }
    }

    private static void AddLevelToToc(XmlDocument xml, XmlElement toc, XmlElement level, string expressionUri, XmlNamespaceManager nsmgr, string parentEId) {
        var heading = level.SelectSingleNode("akn:heading", nsmgr);
        if (heading == null) return;

        string headingText = heading.InnerText?.Trim();
        if (string.IsNullOrEmpty(headingText)) return;

        if (headingText.Length > 100)
            headingText = headingText.Substring(0, 97) + "...";

        string href = "#" + parentEId;
        AddTocItem(xml, toc, href, headingText, "3");

        // Recurse into nested levels (e.g. "Section 1: ..." under "Part 1")
        var childLevels = level.SelectNodes("akn:level[akn:heading]", nsmgr);
        if (childLevels != null) {
            foreach (XmlElement child in childLevels) {
                AddLevelToToc(xml, toc, child, expressionUri, nsmgr, parentEId);
            }
        }
    }

    private static void AddParagraphToToc(XmlDocument xml, XmlElement toc, XmlElement paragraph, string expressionUri, XmlNamespaceManager nsmgr) {
        var numElement = paragraph.SelectSingleNode("akn:num", nsmgr);
        if (numElement == null) return;

        string numText = numElement.InnerText?.Trim();
        if (string.IsNullOrEmpty(numText)) return;

        string number = Regex.Replace(numText, @"[^\d]", "");
        if (string.IsNullOrEmpty(number)) return;

        var intro = paragraph.SelectSingleNode("akn:intro/akn:p", nsmgr);
        if (intro == null) return;

        string headingText = intro.InnerText?.Trim();
        if (string.IsNullOrEmpty(headingText)) return;

        if (headingText.Length > 100)
            headingText = headingText.Substring(0, 97) + "...";

        string eId = $"paragraph_{number}";
        if (!paragraph.HasAttribute("eId"))
            paragraph.SetAttribute("eId", eId);

        string href = !string.IsNullOrEmpty(expressionUri)
            ? $"{expressionUri}/paragraph/{number}"
            : "#" + eId;

        AddTocItem(xml, toc, href, headingText, "2");
    }

    private static void AddTocItem(XmlDocument xml, XmlElement toc, string href, string text, string level) {
        var tocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        tocItem.SetAttribute("href", href);
        tocItem.SetAttribute("level", level);
        var inlineHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        inlineHeading.SetAttribute("name", "tocHeading");
        inlineHeading.InnerText = text;
        tocItem.AppendChild(inlineHeading);
        toc.AppendChild(tocItem);
    }

}

}
