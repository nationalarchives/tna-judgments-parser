
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
    
    [ThreadStatic]
    private static string _currentFilename;

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
        _currentFilename = filename;
        try {
            return ((BaseHelper)Instance).Parse(docx, simplify);
        } finally {
            _currentFilename = null;
        }
    }

    /// <summary>
    /// Parse an Impact Assessment document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document bytes</param>
    /// <param name="filename">The filename (e.g., ukia_20250001_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true) {
        _currentFilename = filename;
        try {
            return ((BaseHelper)Instance).Parse(docx, simplify);
        } finally {
            _currentFilename = null;
        }
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx) {
        return ImpactAssessments.Parser.Parse(docx, _currentFilename);
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
    /// Transform major IA content areas into proper section elements
    /// </summary>
    private static void TransformContentSections(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var sectionPatterns = new[] {
            "Cost of Preferred",
            "What are the policy objectives",
            "What policy options have been considered",
            "Will the policy be reviewed",
            "FULL ECONOMIC ASSESSMENT",
            "BUSINESS ASSESSMENT"
        };

        int sectionCounter = 2;

        var levels = xml.SelectNodes("//akn:level", nsmgr);
        
        foreach (XmlNode level in levels) {
            foreach (var pattern in sectionPatterns) {
                if (ContainsSectionContent(level, pattern)) {
                    TransformToSemanticSection(xml, level, sectionCounter);
                    logger.LogInformation($"Transformed level to section with eId: section_{sectionCounter}");
                    sectionCounter++;
                    break;
                }
            }
        }
    }

    private static bool ContainsSectionContent(XmlNode level, string contentPattern) {
        var textContent = level.InnerText ?? "";
        return textContent.Contains(contentPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static void TransformToSemanticSection(XmlDocument xml, XmlNode level, int sectionNumber) {
        var section = xml.CreateElement("section", AKN_NAMESPACE);
        section.SetAttribute("eId", $"section_{sectionNumber}");

        var headingText = ExtractSectionHeading(level);
        if (!string.IsNullOrEmpty(headingText)) {
            var heading = xml.CreateElement("heading", AKN_NAMESPACE);
            heading.InnerText = headingText;
            section.AppendChild(heading);
        }

        while (level.HasChildNodes) {
            section.AppendChild(level.FirstChild);
        }

        level.ParentNode.ReplaceChild(section, level);
    }

    private static string ExtractSectionHeading(XmlNode level) {
        var nsmgr = new XmlNamespaceManager(level.OwnerDocument.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        var boldElements = level.SelectNodes(".//akn:b", nsmgr);
        foreach (XmlNode bold in boldElements) {
            var text = bold.InnerText?.Trim();
            if (!string.IsNullOrEmpty(text) && text.Length > 10 && text.Length < 100) {
                text = text.Replace(":", "").Trim();
                if (text.EndsWith("?")) return text;
                if (text.Contains("ASSESSMENT")) return text;
                if (text.Contains("Cost of")) return text;
                if (text.Contains("policy")) return text;
            }
        }
        return "";
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
    /// Generate table of contents from sections with headings
    /// Creates a toc element with tocItem entries linking to each section using full URLs
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

        var sectionsWithHeadings = xml.SelectNodes("//akn:mainBody/akn:section[akn:heading and @eId!='section_1']", nsmgr);
        
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
        
        // Add section entries if they exist
        if (sectionsWithHeadings != null && sectionsWithHeadings.Count > 0) {
            foreach (XmlElement section in sectionsWithHeadings) {
            var eId = section.GetAttribute("eId");
            var heading = section.SelectSingleNode("akn:heading", nsmgr);
            
            if (string.IsNullOrEmpty(eId) || heading == null) {
                continue;
            }

            string headingText = heading.InnerText?.Trim();
            if (string.IsNullOrEmpty(headingText)) {
                continue;
            }

            if (headingText.Length > 100) {
                headingText = headingText.Substring(0, 97) + "...";
            }

            string sectionNumber = eId.Replace("section_", "");

            string href;
            if (!string.IsNullOrEmpty(expressionUri)) {
                href = $"{expressionUri}/section/{sectionNumber}";
            } else {
                href = "#" + eId;
            }

            var tocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
            tocItem.SetAttribute("href", href);
            tocItem.SetAttribute("level", "2");
            
            var inlineHeading = xml.CreateElement("inline", AKN_NAMESPACE);
            inlineHeading.SetAttribute("name", "tocHeading");
            // Include section number in heading text
            inlineHeading.InnerText = $"{sectionNumber}. {headingText}";
            tocItem.AppendChild(inlineHeading);
            
            toc.AppendChild(tocItem);
            }
        }

        if (toc.HasChildNodes) {
            var firstSection = mainBody.SelectSingleNode("akn:section[1]", nsmgr);
            if (firstSection != null && firstSection.NextSibling != null) {
                mainBody.InsertBefore(toc, firstSection.NextSibling);
            } else {
                mainBody.InsertBefore(toc, mainBody.FirstChild);
            }
            
            logger.LogInformation("Generated TOC with {Count} entries", toc.ChildNodes.Count);
        }
    }

}

}

