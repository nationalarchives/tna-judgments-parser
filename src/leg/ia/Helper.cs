
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

    // Constants
    private const string AKN_NAMESPACE = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
    
    // Semantic element mappings
    // Note: docNumber is not included because it's not allowed as a child of <p> in AKN 3.0 schema
    // Tuple structure: (elementName, attributeValue) - attributeValue is used for 'name' attribute on inline elements
    private static readonly Dictionary<string, (string elementName, string attributeValue)> SemanticMappings = new() {
        { "Title:", ("docTitle", null) },
        // { "IA No:", ("docNumber", null) },  // Commented out: not valid in AKN 3.0 schema for <p> elements
        { "Stage:", ("docStage", null) },
        { "Date:", ("docDate", null) },
        { "Lead department or agency:", ("inline", "leadDepartment") },
        { "Other departments or agencies", ("inline", "otherDepartments") }
    };

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
        // Apply IA-specific style mappings
        ApplyIAStyleMappings(xml);
        
        // Phase 2: Header Structure Enhancement
        TransformHeaderStructure(xml);
        
        // Phase 3: Section-Based Organization
        TransformContentSections(xml);
        
        // Phase 4: Clean up empty heading elements
        RemoveEmptyHeadings(xml);
        
        // Phase 5: Replace th with td (th not supported in IA subschema)
        ReplaceThWithTd(xml);
        
        // Phase 6: Remove unsupported elements (img, subFlow/math, TOC, marker/tab)
        RemoveUnsupportedElements(xml);
        
        // Phase 7: Convert blockContainer to p in table cells
        ConvertBlockContainerInTables(xml);
    }

    private static void ApplyIAStyleMappings(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();
    
        var paragraphs = xml.SelectNodes("//akn:p", nsmgr);
        int classified = 0;
        int semanticElements = 0;
        
        XmlNode previousParagraph = null;
        
        foreach (XmlNode p in paragraphs) {
            string cleanContent = CleanContent(p.InnerText?.Trim() ?? "");
            
            // Try to transform to semantic element first
            bool transformed = TryTransformToSemanticElement(xml, p, cleanContent);
            if (transformed) {
                semanticElements++;
            } else {
                // Apply CSS class if not transformed
                string cssClass = DetermineCssClass(cleanContent, p, previousParagraph);
                if (cssClass != null) {
                    ApplyCssClass(p, cssClass);
                    classified++;
                }
            }
            
            previousParagraph = p;
        }
        
        LogProcessingResults(logger, classified, semanticElements);
    }
    
    private static string CleanContent(string content) {
        return content.Replace("<b>", "").Replace("</b>", "");
    }
    
    private static bool TryTransformToSemanticElement(XmlDocument xml, XmlNode paragraph, string cleanContent) {
        foreach (var mapping in SemanticMappings) {
            if (cleanContent.StartsWith(mapping.Key)) {
                return TransformToSemanticElement(xml, paragraph, mapping.Key, mapping.Value.elementName, mapping.Value.attributeValue);
            }
        }
        return false;
    }
    
    private static string DetermineCssClass(string cleanContent, XmlNode paragraph, XmlNode previousParagraph) {
        if (cleanContent.StartsWith("Impact Assessment")) {
            return "ia-title";
        }
        
        if (cleanContent.Contains("RPC Reference")) {
            return "ia-head-label";
        }
        
        if (IsInHeaderTable(paragraph)) {
            return DetermineTableCssClass(cleanContent, previousParagraph);
        }
        
        return null;
    }
    
    private static string DetermineTableCssClass(string cleanContent, XmlNode previousParagraph) {
        if (previousParagraph != null && IsInHeaderTable(previousParagraph)) {
            string prevContent = CleanContent(previousParagraph.InnerText?.Trim() ?? "");
            if ((prevContent.StartsWith("Lead department") || prevContent.StartsWith("Other departments")) && 
                cleanContent.Length < 100 && !cleanContent.Contains(":")) {
                return "ia-header-text";
            }
        }
        return "ia-table-text";
    }
    
    private static void ApplyCssClass(XmlNode paragraph, string cssClass) {
        var classAttr = paragraph.OwnerDocument.CreateAttribute("class");
        classAttr.Value = cssClass;
        paragraph.Attributes.Append(classAttr);
    }
    
    private static void LogProcessingResults(ILogger logger, int classified, int semanticElements) {
        if (classified > 0 || semanticElements > 0) {
            logger.LogInformation("Applied IA CSS classes to {ClassCount} paragraphs and created {SemanticCount} semantic elements", 
                classified, semanticElements);
        }
    }
    
    private static bool TransformToSemanticElement(XmlDocument xml, XmlNode paragraph, string labelText, string semanticElementName, string nameAttribute) {
        try {
            string content = paragraph.InnerText?.Trim() ?? "";
            
            // Extract the value part after the label
            string valueText = ExtractValueText(content, labelText);
            
            // For docDate elements, validate date parsing before creating the element
            // If we can't parse the date, don't create docDate at all (date attribute is required)
            if (semanticElementName == "docDate") {
                if (!TryParseDateFromValue(valueText, out _)) {
                    return false; // Fall back to CSS-only approach
                }
            }
            
            // Clear existing content and rebuild with semantic structure
            paragraph.RemoveAll();
            
            // Create semantic structure: <b>Title:</b> <docTitle>content</docTitle>
            var boldElement = xml.CreateElement("b", AKN_NAMESPACE);
            boldElement.InnerText = labelText;
            paragraph.AppendChild(boldElement);
            
            // Add space
            paragraph.AppendChild(xml.CreateTextNode(" "));
            
            // Add the semantic element with the value content
            if (!string.IsNullOrEmpty(valueText)) {
                var semanticElement = xml.CreateElement(semanticElementName, AKN_NAMESPACE);
                
                // For docDate elements, add the required 'date' attribute
                if (semanticElementName == "docDate") {
                    if (TryParseDateFromValue(valueText, out string normalizedDate)) {
                        semanticElement.SetAttribute("date", normalizedDate);
                    }
                }
                
                // For inline elements with a name attribute, add it
                if (semanticElementName == "inline" && !string.IsNullOrEmpty(nameAttribute)) {
                    semanticElement.SetAttribute("name", nameAttribute);
                }
                
                semanticElement.InnerText = valueText;
                paragraph.AppendChild(semanticElement);
            }
            
            return true;
        }
        catch (Exception) {
            // If transformation fails, return false to fall back to CSS-only approach
            return false;
        }
    }
    
    private static string ExtractValueText(string content, string labelText) {
        string valueText = content.Substring(labelText.Length).Trim();
        if (valueText.StartsWith(":")) {
            valueText = valueText.Substring(1).Trim();
        }
        return valueText;
    }
    
    private static bool TryParseDateFromValue(string dateValue, out string isoDate) {
        isoDate = null;
        
        if (string.IsNullOrWhiteSpace(dateValue)) {
            return false;
        }
        
        // Handle malformed dates with extra leading zeros like "030/9/2015"
        var match = System.Text.RegularExpressions.Regex.Match(dateValue, @"^(\d{2,3})/(\d{1,2})/(\d{4})$");
        if (match.Success) {
            if (int.TryParse(match.Groups[1].Value, out int day) && 
                int.TryParse(match.Groups[2].Value, out int month) && 
                int.TryParse(match.Groups[3].Value, out int year)) {
                
                // Handle extra leading zero (e.g., "030" → 30)
                if (day > 31) {
                    day = day % 100;
                }
                
                try {
                    var date = new DateTime(year, month, day);
                    isoDate = date.ToString("yyyy-MM-dd");
                    return true;
                }
                catch {
                    // Fall through to other parsers
                }
            }
        }
        
        // Try UK date format culture first (DD/MM/YYYY) as this is the expected format for UK IA documents
        if (DateTime.TryParse(dateValue, new System.Globalization.CultureInfo("en-GB"), 
            System.Globalization.DateTimeStyles.None, out DateTime parsedDate)) {
            isoDate = parsedDate.ToString("yyyy-MM-dd");
            return true;
        }
        
        // Try to parse using DateTime.TryParse which handles many formats
        if (DateTime.TryParse(dateValue, System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.None, out parsedDate)) {
            isoDate = parsedDate.ToString("yyyy-MM-dd");
            return true;
        }
        
        // Try parsing month-year formats like "Aug 2022" or "July 2022"
        // Default to first day of the month
        if (System.Text.RegularExpressions.Regex.IsMatch(dateValue, @"^[A-Za-z]+\s+\d{4}$")) {
            if (DateTime.TryParseExact(dateValue, new[] { "MMMM yyyy", "MMM yyyy" }, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out parsedDate)) {
                isoDate = parsedDate.ToString("yyyy-MM-dd");
                return true;
            }
        }
        
        return false;
    }
    
    private static bool IsInHeaderTable(XmlNode paragraph) {
        // Check if this paragraph is inside a table (likely the IA header table)
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
    /// Phase 2: Transform header structure for Impact Assessments
    /// Replace generic level elements with semantic hcontainer elements
    /// </summary>
    private static void TransformHeaderStructure(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        // Find the first level element (IA header table)
        var firstLevel = xml.SelectSingleNode("//akn:mainBody/akn:level[1]", nsmgr);
        if (firstLevel == null) return;

        // Check if this level contains IA header metadata (docTitle, docStage, docDate, etc.)
        var hasHeaderMetadata = firstLevel.SelectNodes(".//akn:docTitle | .//akn:docStage | .//akn:docDate | .//akn:inline[@name='leadDepartment'] | .//akn:inline[@name='otherDepartments']", nsmgr).Count > 0;
        
        if (hasHeaderMetadata) {
            // Transform to section element
            var section = xml.CreateElement("section", AKN_NAMESPACE);
            
            // Add eId attribute for proper identification
            section.SetAttribute("eId", "section_1");
            
            // Copy all child nodes from level to section
            while (firstLevel.HasChildNodes) {
                section.AppendChild(firstLevel.FirstChild);
            }
            
            // Replace the level element with section
            firstLevel.ParentNode.ReplaceChild(section, firstLevel);
            
            logger.LogInformation("Transformed IA header from <level> to <section>");
        }
    }

    /// <summary>
    /// Phase 3: Transform major IA content areas into proper section elements
    /// </summary>
    private static void TransformContentSections(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        // Define major IA sections with their identifying content patterns
        var sectionMappings = new Dictionary<string, string> {
            { "Cost of Preferred", "cost-analysis" },
            { "What are the policy objectives", "policy-objectives" },
            { "What policy options have been considered", "policy-options" },
            { "Will the policy be reviewed", "policy-review" },
            { "FULL ECONOMIC ASSESSMENT", "economic-assessment" },
            { "BUSINESS ASSESSMENT", "business-assessment" }
        };

        int sectionCounter = 2; // Start from 2 since header is section 1

        // Find level elements that contain major section content
        var levels = xml.SelectNodes("//akn:level", nsmgr);
        
        foreach (XmlNode level in levels) {
            foreach (var mapping in sectionMappings) {
                if (ContainsSectionContent(level, mapping.Key)) {
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
        // Create new section element
        var section = xml.CreateElement("section", AKN_NAMESPACE);
        section.SetAttribute("eId", $"section_{sectionNumber}");

        // Add heading if we can identify one
        var headingText = ExtractSectionHeading(level);
        if (!string.IsNullOrEmpty(headingText)) {
            var heading = xml.CreateElement("heading", AKN_NAMESPACE);
            heading.InnerText = headingText;
            section.AppendChild(heading);
        }

        // Copy all child nodes from level to section
        while (level.HasChildNodes) {
            section.AppendChild(level.FirstChild);
        }

        // Replace the level element with section
        level.ParentNode.ReplaceChild(section, level);
    }

    private static string ExtractSectionHeading(XmlNode level) {
        // Look for bold text that could serve as a heading
        var nsmgr = new XmlNamespaceManager(level.OwnerDocument.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        var boldElements = level.SelectNodes(".//akn:b", nsmgr);
        foreach (XmlNode bold in boldElements) {
            var text = bold.InnerText?.Trim();
            if (!string.IsNullOrEmpty(text) && text.Length > 10 && text.Length < 100) {
                // Clean up common patterns
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
    /// Phase 4: Remove empty heading elements that violate AKN 3.0 schema
    /// </summary>
    private static void RemoveEmptyHeadings(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        // Find all empty heading elements
        var emptyHeadings = xml.SelectNodes("//akn:heading[not(normalize-space())]", nsmgr);
        
        foreach (XmlNode heading in emptyHeadings) {
            heading.ParentNode.RemoveChild(heading);
        }
    }

    /// <summary>
    /// Phase 5: Replace th with td (th not supported in IA subschema)
    /// </summary>
    private static void ReplaceThWithTd(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        // Find all th elements
        var thElements = xml.SelectNodes("//akn:th", nsmgr);
        
        foreach (XmlElement th in thElements) {
            // Create new td element
            var td = xml.CreateElement("td", AKN_NAMESPACE);
            
            // Copy all attributes
            foreach (XmlAttribute attr in th.Attributes) {
                td.SetAttribute(attr.Name, attr.Value);
            }
            
            // Copy all child nodes
            while (th.HasChildNodes) {
                td.AppendChild(th.FirstChild);
            }
            
            // Replace th with td
            th.ParentNode.ReplaceChild(td, th);
        }
    }

    /// <summary>
    /// Phase 6: Remove elements not supported in strict AKN 3.0 for doc elements
    /// </summary>
    private static void RemoveUnsupportedElements(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        // Remove img elements
        var imgs = xml.SelectNodes("//akn:img", nsmgr);
        foreach (XmlElement img in imgs) {
            img.ParentNode.RemoveChild(img);
        }
        
        // Remove subFlow elements (used for math)
        var subFlows = xml.SelectNodes("//akn:subFlow", nsmgr);
        foreach (XmlElement subFlow in subFlows) {
            subFlow.ParentNode.RemoveChild(subFlow);
        }
        
        // Remove toc elements
        var tocs = xml.SelectNodes("//akn:toc", nsmgr);
        foreach (XmlElement toc in tocs) {
            toc.ParentNode.RemoveChild(toc);
        }
        
        // Remove marker elements (used for tabs)
        var markers = xml.SelectNodes("//akn:marker", nsmgr);
        foreach (XmlElement marker in markers) {
            marker.ParentNode.RemoveChild(marker);
        }
    }

    /// <summary>
    /// Phase 7: Convert blockContainer in table cells to inline p elements
    /// </summary>
    private static void ConvertBlockContainerInTables(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        
        // Find all blockContainer elements within td elements
        var blockContainers = xml.SelectNodes("//akn:td/akn:blockContainer", nsmgr);
        
        foreach (XmlElement container in blockContainers) {
            var parent = container.ParentNode;
            
            // Create a new p element
            var p = xml.CreateElement("p", AKN_NAMESPACE);
            
            // Extract the num element if present
            var num = container.SelectSingleNode("akn:num", nsmgr);
            if (num != null) {
                // Add the number as text
                p.AppendChild(xml.CreateTextNode(num.InnerText + " "));
            }
            
            // Extract content from the inner p element
            var innerP = container.SelectSingleNode("akn:p", nsmgr);
            if (innerP != null) {
                // Copy all child nodes from inner p
                while (innerP.HasChildNodes) {
                    p.AppendChild(innerP.FirstChild);
                }
            }
            
            // Replace blockContainer with the new p
            parent.ReplaceChild(p, container);
        }
    }

}

}
