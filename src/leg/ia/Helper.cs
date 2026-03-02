
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.ImpactAssessments {

class Helper : BaseHelper {

    private const string AKN_NAMESPACE = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";

    private static readonly Helper Instance = new Helper();

    private Helper() : base(LegislativeDocumentConfig.ForImpactAssessments()) { }

    public static new IXmlDocument Parse(Stream docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static new IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    /// <summary>
    /// Parse an Impact Assessment document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document stream</param>
    /// <param name="filename">The filename (e.g., ukia_20250001_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    /// <summary>
    /// Parse an Impact Assessment document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document bytes</param>
    /// <param name="filename">The filename (e.g., ukia_20250001_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return ImpactAssessments.Parser.Parse(docx, filename);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        MovePrefaceTablesToMainBody(xml);
        ApplyIAStyleMappings(xml);
        AddDateAttributesToDocDate(xml);
        UpdateFRBRDatesFromDocDate(xml);
        TransformHeaderStructure(xml);
        TransformContentSections(xml);
        RemoveEmptyHeadings(xml);
        ReplaceThWithTd(xml);
        RemoveUnsupportedElements(xml);
        FixSectionHeadingPosition(xml);
        FixNestedAnchors(xml);
        GenerateTableOfContents(xml);
    }

    private static void ApplyIAStyleMappings(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();
    
        var paragraphs = xml.SelectNodes("//akn:p", nsmgr);
        int classified = 0;
        
        XmlNode previousParagraph = null;
        
        foreach (XmlNode p in paragraphs) {
            string cleanContent = CleanContent(p.InnerText?.Trim() ?? "");
            string cssClass = DetermineCssClass(cleanContent, p, previousParagraph);
            if (cssClass != null) {
                ApplyCssClass(p, cssClass);
                classified++;
            }
            
            previousParagraph = p;
        }
        
        if (classified > 0) {
            logger.LogInformation("Applied IA CSS classes to {ClassCount} paragraphs", classified);
        }
    }
    
    private static string CleanContent(string content) {
        return content.Replace("<b>", "").Replace("</b>", "");
    }
    
    private static string DetermineCssClass(string cleanContent, XmlNode paragraph, XmlNode previousParagraph) {
        if (cleanContent.StartsWith("Impact Assessment")) {
            return "ia-title";
        }
        
        if (cleanContent.Contains("RPC Reference")) {
            return "ia-head-label";
        }
        
        bool inHeader = IsInPreface(paragraph);
        bool inTable = IsInHeaderTable(paragraph);
        
        if (inHeader || inTable) {
            return DetermineTableCssClass(cleanContent, previousParagraph, inHeader, inTable);
        }
        
        return null;
    }
    
    private static string DetermineTableCssClass(string cleanContent, XmlNode previousParagraph, bool inHeader, bool inTable) {
        if (previousParagraph != null && (inHeader || IsInHeaderTable(previousParagraph))) {
            string prevContent = CleanContent(previousParagraph.InnerText?.Trim() ?? "");
            if ((prevContent.StartsWith("Lead department") || prevContent.StartsWith("Other departments")) && 
                cleanContent.Length < 100 && !cleanContent.Contains(":")) {
                return "ia-header-text";
            }
        }
        if (inTable) {
            return "ia-table-text";
        }
        if (inHeader) {
            string trimmed = cleanContent.Trim();
            if (trimmed.Length > 0 && !trimmed.Contains(":") && trimmed.Length < 200) {
                return "ia-table-text";
            }
        }
        return null;
    }
    
    private static bool IsInPreface(XmlNode paragraph) {
        var parent = paragraph.ParentNode;
        while (parent != null) {
            if (parent.Name == "preface") {
                return true;
            }
            parent = parent.ParentNode;
        }
        return false;
    }
    
    private static void ApplyCssClass(XmlNode paragraph, string cssClass) {
        var classAttr = paragraph.OwnerDocument.CreateAttribute("class");
        classAttr.Value = cssClass;
        paragraph.Attributes.Append(classAttr);
    }
    
    private static void AddDateAttributesToDocDate(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        var docDateNodes = xml.SelectNodes("//akn:docDate[not(@date)]", nsmgr);
        
        foreach (XmlElement docDateNode in docDateNodes) {
            string dateText = docDateNode.InnerText?.Trim() ?? "";
            
            if (string.IsNullOrEmpty(dateText)) {
                var parent = docDateNode.ParentNode as XmlElement;
                if (parent != null) {
                    string parentText = parent.InnerText?.Trim() ?? "";
                    int dateIndex = parentText.IndexOf("Date:", StringComparison.InvariantCultureIgnoreCase);
                    if (dateIndex >= 0) {
                        dateText = parentText.Substring(dateIndex + 5).Trim();
                        if (dateText.StartsWith(":"))
                            dateText = dateText.Substring(1).Trim();
                    }
                }
            }
            
            if (TryParseDateFromValue(dateText, out string normalizedDate)) {
                docDateNode.SetAttribute("date", normalizedDate);
            } else if (string.IsNullOrEmpty(dateText)) {
                docDateNode.ParentNode?.RemoveChild(docDateNode);
            }
        }
    }
    
    private static bool TryParseDateFromValue(string dateValue, out string isoDate) {
        isoDate = null;
        
        if (string.IsNullOrWhiteSpace(dateValue)) {
            return false;
        }
        
        // Clean the date value - remove trailing asterisks, parenthetical notes, etc.
        string cleanedValue = CleanDateValue(dateValue);
        
        var match = System.Text.RegularExpressions.Regex.Match(cleanedValue, @"^(\d{2,3})/(\d{1,2})/(\d{4})$");
        if (match.Success) {
            if (int.TryParse(match.Groups[1].Value, out int day) && 
                int.TryParse(match.Groups[2].Value, out int month) && 
                int.TryParse(match.Groups[3].Value, out int year)) {
                
                if (day > 31) {
                    day = day % 100;
                }
                
                try {
                    var date = new DateTime(year, month, day);
                    isoDate = date.ToString("yyyy-MM-dd");
                    return true;
                }
                catch {
                }
            }
        }
        
        if (DateTime.TryParse(cleanedValue, new System.Globalization.CultureInfo("en-GB"), 
            System.Globalization.DateTimeStyles.None, out DateTime parsedDate)) {
            isoDate = parsedDate.ToString("yyyy-MM-dd");
            return true;
        }
        
        if (DateTime.TryParse(cleanedValue, System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.None, out parsedDate)) {
            isoDate = parsedDate.ToString("yyyy-MM-dd");
            return true;
        }
        
        // Try "Month Year" format (e.g., "May 2017", "December 2014")
        var monthYearMatch = System.Text.RegularExpressions.Regex.Match(cleanedValue, @"^([A-Za-z]+)\s+(\d{4})");
        if (monthYearMatch.Success) {
            string monthYearStr = monthYearMatch.Groups[1].Value + " " + monthYearMatch.Groups[2].Value;
            if (DateTime.TryParseExact(monthYearStr, new[] { "MMMM yyyy", "MMM yyyy" }, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out parsedDate)) {
                isoDate = parsedDate.ToString("yyyy-MM-dd");
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Clean date value by removing trailing asterisks, parenthetical notes, and other noise.
    /// </summary>
    private static string CleanDateValue(string dateValue) {
        if (string.IsNullOrWhiteSpace(dateValue)) {
            return dateValue;
        }
        
        string cleaned = dateValue.Trim();
        
        // Remove trailing asterisks (e.g., "13/12/2012*" -> "13/12/2012")
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\*+$", "");
        
        // Remove parenthetical notes (e.g., "May 2017 (updated June 2017...)" -> "May 2017")
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\(.*\)\s*$", "");
        
        // Remove any remaining trailing punctuation
        cleaned = cleaned.TrimEnd('.', ',', ';', ':');
        
        return cleaned.Trim();
    }
    
    private static bool IsInHeaderTable(XmlNode paragraph) {
        var parent = paragraph.ParentNode;
        while (parent != null) {
            if (parent.Name == "table" || parent.Name == "td" || parent.Name == "tr") {
                return true;
            }
            parent = parent.ParentNode;
        }
        return false;
    }

    /// <summary>
    /// Extract docDate from document and update FRBR dates
    /// </summary>
    private static void UpdateFRBRDatesFromDocDate(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        var docDateNode = xml.SelectSingleNode("//akn:docDate[@date]", nsmgr) as XmlElement;
        if (docDateNode != null) {
            string docDate = docDateNode.GetAttribute("date");
            
            var workDate = xml.SelectSingleNode("//akn:FRBRWork/akn:FRBRdate", nsmgr) as XmlElement;
            if (workDate != null) {
                workDate.SetAttribute("date", docDate);
                workDate.SetAttribute("name", "document");
            }
            
            var expDate = xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRdate", nsmgr) as XmlElement;
            if (expDate != null) {
                expDate.SetAttribute("date", docDate);
                expDate.SetAttribute("name", "document");
            }
        }
    }

    /// <summary>
    /// Transform header structure for Impact Assessments
    /// Wrap header metadata in hcontainer with name="summary" containing a table structure
    /// </summary>
    private static void TransformHeaderStructure(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) return;

        var headerLevels = new List<XmlNode>();
        var levels = mainBody.SelectNodes("akn:level", nsmgr);
        
        foreach (XmlNode level in levels) {
            var content = level.SelectSingleNode("akn:content", nsmgr);
            if (content == null) break;
            
            var paragraphs = content.SelectNodes("akn:p", nsmgr);
            if (paragraphs.Count == 0) break;
            
            var firstPara = paragraphs[0];
            string firstParaText = CleanContent(firstPara.InnerText?.Trim() ?? "");
            
            bool isHeaderRow = firstParaText.StartsWith("Impact Assessment") ||
                              firstParaText.StartsWith("Title:") ||
                              firstParaText.StartsWith("Type of measure:") ||
                              firstParaText.StartsWith("Department or agency:") ||
                              firstParaText.StartsWith("IA number:") ||
                              firstParaText.StartsWith("Type of Impact Assessment") ||
                              firstParaText.StartsWith("RPC reference number:") ||
                              firstParaText.StartsWith("Contact for enquiries:") ||
                              firstParaText.StartsWith("Date:") ||
                              firstParaText.Contains("Lead department") ||
                              firstParaText.Contains("Other departments");
            
            if (isHeaderRow) {
                headerLevels.Add(level);
            } else {
                break;
            }
        }

        if (headerLevels.Count == 0) return;

        var hcontainer = xml.CreateElement("hcontainer", AKN_NAMESPACE);
        hcontainer.SetAttribute("name", "summary");
        
        var hcontainerContent = xml.CreateElement("content", AKN_NAMESPACE);
        hcontainer.AppendChild(hcontainerContent);
        
        var table = xml.CreateElement("table", AKN_NAMESPACE);
        hcontainerContent.AppendChild(table);
        
        foreach (XmlNode level in headerLevels) {
            var tr = xml.CreateElement("tr", AKN_NAMESPACE);
            table.AppendChild(tr);
            
            var levelContent = level.SelectSingleNode("akn:content", nsmgr);
            if (levelContent != null) {
                var td = xml.CreateElement("td", AKN_NAMESPACE);
                tr.AppendChild(td);
                
                var paragraphs = levelContent.SelectNodes("akn:p", nsmgr);
                foreach (XmlNode para in paragraphs) {
                    var importedPara = xml.ImportNode(para, true);
                    td.AppendChild(importedPara);
                }
            }
        }
        
        var firstChild = mainBody.FirstChild;
        if (firstChild != null) {
            mainBody.InsertBefore(hcontainer, firstChild);
        } else {
            mainBody.AppendChild(hcontainer);
        }
        
        foreach (XmlNode level in headerLevels) {
            mainBody.RemoveChild(level);
        }
        
        logger.LogInformation("Transformed {Count} IA header levels into hcontainer.summary with table structure", headerLevels.Count);
    }

    /// <summary>
    /// Transform major IA content areas into proper section elements.
    /// Uses structural patterns (bold numbered paragraphs, bold levels) instead of content matching.
    /// Groups following siblings into each section until the next header (structured model: intro + hierElements + wrapUp).
    /// </summary>
    private static void TransformContentSections(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) return;

        int sectionCounter = 2;
        var elementsToTransform = new List<(XmlNode element, string headingText)>();

        // Find all direct children of mainBody that could be section headers
        foreach (XmlNode child in mainBody.ChildNodes) {
            if (child.NodeType != XmlNodeType.Element) continue;

            string headingText = null;

            if (child.LocalName == "paragraph" && child.NamespaceURI == AKN_NAMESPACE) {
                if (IsSectionHeaderParagraph(child, nsmgr, out headingText)) {
                    elementsToTransform.Add((child, headingText));
                }
            } else if (child.LocalName == "level" && child.NamespaceURI == AKN_NAMESPACE) {
                if (IsSectionHeaderLevel(child, nsmgr, out headingText)) {
                    elementsToTransform.Add((child, headingText));
                }
            }
        }

        var headerSet = new HashSet<XmlNode>(elementsToTransform.Select(t => t.element));

        // Transform identified section headers to section elements, grouping following content into each section
        foreach (var (element, headingText) in elementsToTransform) {
            var following = CollectFollowingSiblings(element, headerSet);
            TransformToSemanticSection(xml, element, sectionCounter, headingText, following);
            logger.LogInformation("Transformed {ElementType} to section_{Number}: {Heading} ({FollowingCount} following nodes)",
                element.LocalName, sectionCounter, headingText, following.Count);
            sectionCounter++;
        }

        if (elementsToTransform.Count > 0) {
            logger.LogInformation("Transformed {Count} section headers using structural patterns",
                elementsToTransform.Count);
        }
    }

    /// <summary>
    /// Collect direct siblings after the given element until the next section header or end of parent.
    /// </summary>
    private static List<XmlNode> CollectFollowingSiblings(XmlNode element, HashSet<XmlNode> sectionHeaderElements) {
        var list = new List<XmlNode>();
        for (var n = element.NextSibling; n != null; n = n.NextSibling) {
            if (n.NodeType != XmlNodeType.Element) continue;
            if (sectionHeaderElements.Contains(n)) break;
            list.Add(n);
        }
        return list;
    }

    /// <summary>
    /// Whether the element is a hierarchical element (allowed as direct section child in structured model).
    /// </summary>
    private static bool IsHierElement(XmlNode node) {
        if (node?.NamespaceURI != AKN_NAMESPACE) return false;
        switch (node.LocalName) {
            case "paragraph":
            case "level":
            case "section":
            case "hcontainer":
            case "clause":
            case "part":
            case "chapter":
            case "title":
            case "article":
            case "book":
            case "tome":
            case "division":
            case "list":
            case "point":
            case "indent":
            case "alinea":
            case "rule":
            case "subrule":
            case "proviso":
            case "subsection":
            case "subpart":
            case "subparagraph":
            case "subchapter":
            case "subtitle":
            case "subdivision":
            case "subclause":
            case "sublist":
            case "transitional":
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether the element is a block element (must go inside intro or wrapUp in structured section).
    /// </summary>
    private static bool IsBlockElement(XmlNode node) {
        if (node?.NamespaceURI != AKN_NAMESPACE) return false;
        switch (node.LocalName) {
            case "p":
            case "table":
            case "blockContainer":
            case "block":
            case "foreign":
            case "figure":
            case "formula":
            case "list":
            case "tblock":
            case "recital":
            case "citation":
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Check if a paragraph element is a section header based on structural patterns.
    /// Pattern: Bold numbered/lettered paragraph with bold content
    /// </summary>
    private static bool IsSectionHeaderParagraph(XmlNode paragraph, XmlNamespaceManager nsmgr, out string headingText) {
        headingText = null;

        // Check if <num> contains <b>
        var num = paragraph.SelectSingleNode("akn:num", nsmgr);
        if (num == null) return false;

        var numBold = num.SelectSingleNode("akn:b", nsmgr);
        if (numBold == null) return false;

        string numText = numBold.InnerText?.Trim() ?? "";
        
        // Check if it's a numbered (1., 2., etc.) or lettered (A., B., etc.) section
        bool isNumberedOrLettered = System.Text.RegularExpressions.Regex.IsMatch(numText, @"^([0-9]+|[A-Z])\.$");
        if (!isNumberedOrLettered) return false;

        // Check if first <p> in <content> is predominantly bold
        var content = paragraph.SelectSingleNode("akn:content", nsmgr);
        if (content == null) return false;

        var firstP = content.SelectSingleNode("akn:p", nsmgr);
        if (firstP == null) return false;

        var firstPBold = firstP.SelectSingleNode("akn:b", nsmgr);
        if (firstPBold == null) return false;

        string boldText = firstPBold.InnerText?.Trim() ?? "";
        
        // Must be substantive but heading-like (not too long)
        if (boldText.Length < 5 || boldText.Length > 200) return false;

        // Extract heading (remove trailing colons)
        headingText = boldText.Replace(":", "").Trim();
        return true;
    }

    /// <summary>
    /// Check if a level element is a section header based on structural patterns.
    /// Pattern: Level with single bold paragraph that looks like a heading
    /// </summary>
    private static bool IsSectionHeaderLevel(XmlNode level, XmlNamespaceManager nsmgr, out string headingText) {
        headingText = null;

        var content = level.SelectSingleNode("akn:content", nsmgr);
        if (content == null) return false;

        // Should have a single paragraph with bold text
        var paragraphs = content.SelectNodes("akn:p", nsmgr);
        if (paragraphs.Count != 1) return false;

        var firstP = paragraphs[0];
        var boldElement = firstP.SelectSingleNode("akn:b", nsmgr);
        if (boldElement == null) return false;

        string boldText = boldElement.InnerText?.Trim() ?? "";
        
        // Must be heading-like: substantive but short
        if (boldText.Length < 5 || boldText.Length > 150) return false;

        // Check if the bold text is the primary content (not just a small part)
        string totalText = firstP.InnerText?.Trim() ?? "";
        if (boldText.Length < totalText.Length * 0.5) return false;

        headingText = boldText.Replace(":", "").Trim();
        return true;
    }

    private static void TransformToSemanticSection(XmlDocument xml, XmlNode element, int sectionNumber, string headingText, List<XmlNode> following) {
        var section = xml.CreateElement("section", AKN_NAMESPACE);
        section.SetAttribute("eId", $"section_{sectionNumber}");

        // Add num from header if present (paragraph has num, level might not)
        var num = element.SelectSingleNode("akn:num", CreateNsMgr(xml));
        if (num != null) {
            section.AppendChild(num);
        }

        // Add heading if we have one
        if (!string.IsNullOrEmpty(headingText)) {
            var heading = xml.CreateElement("heading", AKN_NAMESPACE);
            heading.InnerText = headingText;
            section.AppendChild(heading);
        }

        if (following == null || following.Count == 0) {
            // Simple content branch: section has num?, heading?, content (from header)
            while (element.HasChildNodes) {
                section.AppendChild(element.FirstChild);
            }
            element.ParentNode.ReplaceChild(section, element);
            return;
        }

        // Structured branch: intro (blocks) + hierElements + wrapUp (blocks)
        // 1) Build intro from header's content element (block children) and leading blocks from following
        XmlElement intro = null;
        var contentEl = element.SelectSingleNode("akn:content", CreateNsMgr(xml));
        if (contentEl != null && contentEl.HasChildNodes) {
            intro = xml.CreateElement("intro", AKN_NAMESPACE);
            while (contentEl.HasChildNodes) {
                intro.AppendChild(contentEl.FirstChild);
            }
            section.AppendChild(intro);
        }

        element.ParentNode.ReplaceChild(section, element);

        // 2) Split following into leading blocks, hierElements, trailing blocks
        var leadingBlocks = new List<XmlNode>();
        var hierElements = new List<XmlNode>();
        var trailingBlocks = new List<XmlNode>();
        bool seenHier = false;
        foreach (var n in following) {
            if (IsHierElement(n)) {
                hierElements.Add(n);
                seenHier = true;
            } else if (IsBlockElement(n)) {
                if (!seenHier) leadingBlocks.Add(n);
                else trailingBlocks.Add(n);
            }
            // Skip other types (e.g. toc) or treat as block if needed
        }

        // 3) Add leading blocks to intro (create intro if needed)
        if (leadingBlocks.Count > 0) {
            if (intro == null) {
                intro = xml.CreateElement("intro", AKN_NAMESPACE);
                section.InsertAfter(intro, section.SelectSingleNode("akn:heading", CreateNsMgr(xml)) ?? section.LastChild);
            }
            foreach (var n in leadingBlocks) {
                intro.AppendChild(n);
            }
        }

        // 4) Insert hierElements after intro (or after heading if no intro)
        XmlNode insertAfter = intro ?? section.LastChild;
        foreach (var hier in hierElements) {
            section.InsertAfter(hier, insertAfter);
            insertAfter = hier;
        }

        // 5) wrapUp for trailing blocks
        if (trailingBlocks.Count > 0) {
            var wrapUp = xml.CreateElement("wrapUp", AKN_NAMESPACE);
            foreach (var n in trailingBlocks) {
                wrapUp.AppendChild(n);
            }
            section.AppendChild(wrapUp);
        }
    }

    private static XmlNamespaceManager CreateNsMgr(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        return nsmgr;
    }

    /// <summary>
    /// Remove empty heading elements that violate AKN 3.0 schema
    /// </summary>
    private static void RemoveEmptyHeadings(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        var emptyHeadings = xml.SelectNodes("//akn:heading[not(normalize-space())]", nsmgr);
        
        foreach (XmlNode heading in emptyHeadings) {
            heading.ParentNode.RemoveChild(heading);
        }
    }

    /// <summary>
    /// Replace th with td (th not supported in IA subschema)
    /// </summary>
    private static void ReplaceThWithTd(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        var thElements = xml.SelectNodes("//akn:th", nsmgr);
        
        foreach (XmlElement th in thElements) {
            var td = xml.CreateElement("td", AKN_NAMESPACE);
            
            foreach (XmlAttribute attr in th.Attributes) {
                td.SetAttribute(attr.Name, attr.Value);
            }
            
            while (th.HasChildNodes) {
                td.AppendChild(th.FirstChild);
            }
            
            th.ParentNode.ReplaceChild(td, th);
        }
    }

    /// <summary>
    /// Remove elements not supported in the IA subschema
    /// </summary>
    private static void RemoveUnsupportedElements(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        var tocs = xml.SelectNodes("//akn:toc", nsmgr);
        foreach (XmlElement toc in tocs) {
            toc.ParentNode.RemoveChild(toc);
        }
        
        var markers = xml.SelectNodes("//akn:marker", nsmgr);
        foreach (XmlElement marker in markers) {
            marker.ParentNode.RemoveChild(marker);
        }
    }

    /// <summary>
    /// Move tables from preface to mainBody
    /// The IA schema only allows p elements in preface, not tables.
    /// </summary>
    private static void MovePrefaceTablesToMainBody(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var preface = xml.SelectSingleNode("//akn:preface", nsmgr);
        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);

        if (preface == null || mainBody == null) {
            return;
        }

        var tables = preface.SelectNodes("akn:table", nsmgr);
        if (tables.Count == 0) {
            return;
        }

        logger.LogInformation("Moving {Count} table(s) from preface to mainBody", tables.Count);

        var firstChild = mainBody.FirstChild;
        foreach (XmlNode table in tables) {
            preface.RemoveChild(table);
            if (firstChild != null) {
                mainBody.InsertBefore(table, firstChild);
            } else {
                mainBody.AppendChild(table);
            }
        }

        if (!preface.HasChildNodes) {
            preface.ParentNode.RemoveChild(preface);
            logger.LogInformation("Removed empty preface element");
        }
    }

    /// <summary>
    /// Fix heading issues in sections
    /// In AKN 3.0:
    /// - Only one heading is allowed per section
    /// - Heading must come before content elements (after optional num)
    /// </summary>
    private static void FixSectionHeadingPosition(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        // Find all sections
        var sections = xml.SelectNodes("//akn:section", nsmgr);

        foreach (XmlElement section in sections) {
            var headings = section.SelectNodes("akn:heading", nsmgr);
            
            if (headings.Count == 0) {
                continue;
            }

            if (headings.Count > 1) {
                logger.LogInformation("Section {EId} has {Count} headings, keeping only the first", 
                    section.GetAttribute("eId"), headings.Count);
                
                for (int i = 1; i < headings.Count; i++) {
                    var extraHeading = headings[i] as XmlElement;
                    section.RemoveChild(extraHeading);
                }
            }

            var heading = section.SelectSingleNode("akn:heading", nsmgr);
            if (heading == null) {
                continue;
            }

            var num = section.SelectSingleNode("akn:num", nsmgr);
            
            XmlNode firstElement = section.FirstChild;
            while (firstElement != null && firstElement.NodeType == XmlNodeType.Text) {
                firstElement = firstElement.NextSibling;
            }

            bool needsMove = false;
            if (num != null) {
                var afterNum = num.NextSibling;
                while (afterNum != null && afterNum.NodeType == XmlNodeType.Text) {
                    afterNum = afterNum.NextSibling;
                }
                needsMove = afterNum != heading;
            } else {
                needsMove = firstElement != heading;
            }

            if (needsMove) {
                logger.LogInformation("Moving heading to correct position in section {EId}", section.GetAttribute("eId"));
                section.RemoveChild(heading);
                if (num != null) {
                    section.InsertAfter(heading, num);
                } else {
                    section.InsertBefore(heading, section.FirstChild);
                }
            }
        }
    }

    /// <summary>
    /// Fix nested anchor elements
    /// AKN 3.0 does not allow nested &lt;a&gt; elements.
    /// This flattens nested anchors by unwrapping inner anchors.
    /// </summary>
    private static void FixNestedAnchors(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var nestedAnchors = xml.SelectNodes("//akn:a//akn:a", nsmgr);
        
        if (nestedAnchors.Count == 0) {
            return;
        }

        logger.LogInformation("Found {Count} nested anchor elements to fix", nestedAnchors.Count);

        var anchorsToFix = nestedAnchors.Cast<XmlElement>().ToList();

        foreach (var innerAnchor in anchorsToFix) {
            var parent = innerAnchor.ParentNode;
            bool isStillNested = false;
            while (parent != null) {
                if (parent.LocalName == "a" && parent.NamespaceURI == AKN_NAMESPACE) {
                    isStillNested = true;
                    break;
                }
                parent = parent.ParentNode;
            }

            if (!isStillNested) {
                continue;
            }

            var parentNode = innerAnchor.ParentNode;
            while (innerAnchor.HasChildNodes) {
                parentNode.InsertBefore(innerAnchor.FirstChild, innerAnchor);
            }
            parentNode.RemoveChild(innerAnchor);
        }
    }

    /// <summary>
    /// Generate table of contents from ALL structural elements in the document.
    /// Includes sections, hcontainers, and other major structural elements.
    /// Extracts headings flexibly - from explicit heading elements, bold text, or content.
    /// </summary>
    private static void GenerateTableOfContents(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) {
            return;
        }

        var expressionUri = xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRuri/@value", nsmgr)?.Value;
        if (string.IsNullOrEmpty(expressionUri)) {
            logger.LogWarning("No FRBRExpression URI found, using fragment references for TOC");
        }

        // Get ALL sections (not just those with headings)
        var allSections = xml.SelectNodes("//akn:mainBody/akn:section", nsmgr);
        
        // Also get hcontainers (like summary sections)
        var hcontainers = xml.SelectNodes("//akn:mainBody/akn:hcontainer[@name]", nsmgr);
        
        var toc = xml.CreateElement("toc", AKN_NAMESPACE);
        
        // Add "whole document" link first (always present)
        var wholeDocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        wholeDocItem.SetAttribute("href", expressionUri ?? "#doc");
        wholeDocItem.SetAttribute("level", "1");
        var wholeDocHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        wholeDocHeading.SetAttribute("name", "tocHeading");
        wholeDocHeading.InnerText = "The whole Impact Assessment";
        wholeDocItem.AppendChild(wholeDocHeading);
        toc.AppendChild(wholeDocItem);
        
        int tocNumber = 1;
        
        // Add hcontainer entries first (usually summary at the top)
        if (hcontainers != null && hcontainers.Count > 0) {
            foreach (XmlElement hcontainer in hcontainers) {
                var name = hcontainer.GetAttribute("name");
                var eId = hcontainer.GetAttribute("eId");
                
                // For hcontainers, prioritize the name attribute over content extraction
                string headingText = null;
                if (!string.IsNullOrEmpty(name)) {
                    headingText = char.ToUpper(name[0]) + name.Substring(1);
                }
                
                // Only extract from content if name is missing or generic
                if (string.IsNullOrEmpty(headingText) || headingText == "Container" || headingText == "Section") {
                    headingText = ExtractHeadingForToc(hcontainer, nsmgr);
                }
                
                // Final fallback
                if (string.IsNullOrEmpty(headingText)) {
                    headingText = "Untitled Section";
                }
                
                string href;
                if (!string.IsNullOrEmpty(expressionUri) && !string.IsNullOrEmpty(eId)) {
                    href = $"{expressionUri}#{eId}";
                } else if (!string.IsNullOrEmpty(eId)) {
                    href = "#" + eId;
                } else {
                    href = $"{expressionUri}#{name}";
                }

                AddTocItem(xml, toc, href, tocNumber, headingText);
                tocNumber++;
            }
        }
        
        // Add ALL section entries
        if (allSections != null && allSections.Count > 0) {
            foreach (XmlElement section in allSections) {
                var eId = section.GetAttribute("eId");
                
                // Extract heading using flexible approach
                string headingText = ExtractHeadingForToc(section, nsmgr);
                
                // Generate eId if missing
                if (string.IsNullOrEmpty(eId)) {
                    eId = $"section_{tocNumber + 1}";
                    section.SetAttribute("eId", eId);
                }
                
                string sectionNumber = eId.Replace("section_", "");

                string href;
                if (!string.IsNullOrEmpty(expressionUri)) {
                    href = $"{expressionUri}/section/{sectionNumber}";
                } else {
                    href = "#" + eId;
                }

                AddTocItem(xml, toc, href, tocNumber, headingText);
                tocNumber++;
            }
        }

        // Insert TOC at the beginning of mainBody
        if (toc.HasChildNodes) {
            mainBody.InsertBefore(toc, mainBody.FirstChild);
            logger.LogInformation("Generated TOC with {Count} entries", toc.ChildNodes.Count);
        }
    }

    /// <summary>
    /// Add a TOC item to the TOC element
    /// </summary>
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

    /// <summary>
    /// Extract a heading for TOC using a cascade of strategies:
    /// 1. Explicit heading element
    /// 2. First bold text in content (questions, titles)
    /// 3. First paragraph text (truncated)
    /// 4. Fallback based on element type
    /// </summary>
    private static string ExtractHeadingForToc(XmlElement element, XmlNamespaceManager nsmgr) {
        // Strategy 1: Try explicit heading element
        var heading = element.SelectSingleNode("akn:heading", nsmgr);
        if (heading != null) {
            string text = heading.InnerText?.Trim();
            if (!string.IsNullOrEmpty(text)) {
                return TruncateHeading(text);
            }
        }
        
        // Strategy 2: Try first bold text in content (common for IA questions/titles)
        var boldElements = element.SelectNodes(".//akn:b", nsmgr);
        if (boldElements != null) {
            foreach (XmlNode bold in boldElements) {
                string text = bold.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 5) {
                    // Clean up the text
                    text = text.Replace(":", "").Trim();
                    
                    // Prefer question-style headings (very common in IAs)
                    if (text.EndsWith("?") || 
                        text.StartsWith("What", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Will", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Are", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Is", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("How", StringComparison.OrdinalIgnoreCase)) {
                        return TruncateHeading(text);
                    }
                    
                    // Accept other substantive bold text
                    if (text.Length > 10 && text.Length < 200) {
                        return TruncateHeading(text);
                    }
                }
            }
        }
        
        // Strategy 3: Try first table cell with substantive bold content
        var tableCells = element.SelectNodes(".//akn:td//akn:b", nsmgr);
        if (tableCells != null) {
            foreach (XmlNode cell in tableCells) {
                string text = cell.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 10 && text.Length < 200) {
                    text = text.Replace(":", "").Trim();
                    if (!string.IsNullOrEmpty(text)) {
                        return TruncateHeading(text);
                    }
                }
            }
        }
        
        // Strategy 4: Try first paragraph text
        var firstPara = element.SelectSingleNode(".//akn:p[normalize-space()]", nsmgr);
        if (firstPara != null) {
            string text = firstPara.InnerText?.Trim();
            if (!string.IsNullOrEmpty(text) && text.Length > 5) {
                return TruncateHeading(text);
            }
        }
        
        // Strategy 5: Fallback - use element type and eId
        var eId = element.GetAttribute("eId");
        if (!string.IsNullOrEmpty(eId)) {
            // Extract number from eId like "section_4"
            var match = System.Text.RegularExpressions.Regex.Match(eId, @"_(\d+)$");
            if (match.Success) {
                return $"Section {match.Groups[1].Value}";
            }
            return eId;
        }
        
        var name = element.GetAttribute("name");
        if (!string.IsNullOrEmpty(name)) {
            return char.ToUpper(name[0]) + name.Substring(1);
        }
        
        return "Untitled Section";
    }

    /// <summary>
    /// Truncate heading text to reasonable length for TOC display
    /// </summary>
    private static string TruncateHeading(string text) {
        if (string.IsNullOrEmpty(text)) return text;
        
        // Remove excessive whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        
        if (text.Length > 100) {
            return text.Substring(0, 97) + "...";
        }
        return text;
    }

}

}

