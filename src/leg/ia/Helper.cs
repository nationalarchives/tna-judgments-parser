
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
    private static readonly Dictionary<string, string> SemanticMappings = new() {
        { "Title:", "docTitle" },
        { "IA No:", "docNumber" },
        { "Stage:", "docStage" },
        { "Date:", "docDate" },
        { "Lead department or agency:", "docDepartment" },
        { "Other departments or agencies", "docDepartment" }
    };

    private static readonly Helper Instance = new Helper();

    private Helper() : base(LegislativeDocumentConfig.ForImpactAssessments()) { }

    public static new IXmlDocument Parse(Stream docx, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify);
    }

    public static new IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx) {
        return ImpactAssessments.Parser.Parse(docx);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        // Apply IA-specific style mappings
        ApplyIAStyleMappings(xml);
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
                return TransformToSemanticElement(xml, paragraph, mapping.Key, mapping.Value);
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
    
    private static bool TransformToSemanticElement(XmlDocument xml, XmlNode paragraph, string labelText, string semanticElementName) {
        try {
            string content = paragraph.InnerText?.Trim() ?? "";
            
            // Extract the value part after the label
            string valueText = ExtractValueText(content, labelText);
            
            // Clear existing content and rebuild with semantic structure
            paragraph.RemoveAll();
            
            // Create semantic structure: <b><docTitle>Title</docTitle></b>: <content>
            var boldElement = CreateBoldSemanticElement(xml, labelText, semanticElementName);
            paragraph.AppendChild(boldElement);
            
            // Add colon and space
            paragraph.AppendChild(xml.CreateTextNode(": "));
            
            // Add the value content
            if (!string.IsNullOrEmpty(valueText)) {
                paragraph.AppendChild(xml.CreateTextNode(valueText));
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
    
    private static XmlElement CreateBoldSemanticElement(XmlDocument xml, string labelText, string semanticElementName) {
        var boldElement = xml.CreateElement("b", AKN_NAMESPACE);
        var semanticElement = xml.CreateElement(semanticElementName, AKN_NAMESPACE);
        semanticElement.InnerText = labelText.TrimEnd(':');
        boldElement.AppendChild(semanticElement);
        return boldElement;
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

}

}
