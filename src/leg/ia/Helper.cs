
using System;
using System.IO;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Common;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.ImpactAssessments {

class Helper : BaseHelper {

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
        nsmgr.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        var logger = Logging.Factory.CreateLogger<Helper>();
    
        var paragraphs = xml.SelectNodes("//akn:p", nsmgr);
        int classified = 0;
        int semanticElements = 0;
        
        XmlNode previousParagraph = null;
        
        foreach (XmlNode p in paragraphs) {
            string content = p.InnerText?.Trim() ?? "";
            string cssClass = null;
            bool transformed = false;
            
            // Remove HTML tags for cleaner pattern matching
            string cleanContent = content.Replace("<b>", "").Replace("</b>", "");
            
            // Transform IA-specific content patterns into semantic elements
            if (cleanContent.StartsWith("Title:")) {
                transformed = TransformToSemanticElement(xml, p, "Title:", "docTitle", "ia-metadata");
                semanticElements++;
            }
            else if (cleanContent.StartsWith("IA No:")) {
                transformed = TransformToSemanticElement(xml, p, "IA No:", "docNumber", "ia-metadata");
                semanticElements++;
            }
            else if (cleanContent.StartsWith("Stage:")) {
                transformed = TransformToSemanticElement(xml, p, "Stage:", "docStage", "ia-metadata");
                semanticElements++;
            }
            else if (cleanContent.StartsWith("Date:")) {
                transformed = TransformToSemanticElement(xml, p, "Date:", "docDate", "ia-metadata");
                semanticElements++;
            }
            else if (cleanContent.StartsWith("Lead department or agency:")) {
                transformed = TransformToSemanticElement(xml, p, "Lead department or agency:", "docDepartment", "ia-metadata");
                semanticElements++;
            }
            else if (cleanContent.StartsWith("Other departments or agencies")) {
                transformed = TransformToSemanticElement(xml, p, "Other departments or agencies", "docDepartment", "ia-metadata");
                semanticElements++;
            }
            
            // If not transformed to semantic element, apply CSS class as before
            if (!transformed) {
                if (cleanContent.StartsWith("Impact Assessment")) {
                    cssClass = "ia-title";
                }
                else if (cleanContent.Contains("RPC Reference")) {
                    cssClass = "ia-head-label";
                }
                else if (IsInHeaderTable(p)) {
                    // Check if this follows a header-text question and is a short answer
                    if (previousParagraph != null && IsInHeaderTable(previousParagraph)) {
                        string prevContent = previousParagraph.InnerText?.Trim().Replace("<b>", "").Replace("</b>", "") ?? "";
                        if ((prevContent.StartsWith("Lead department") || prevContent.StartsWith("Other departments")) && 
                            cleanContent.Length < 100 && !cleanContent.Contains(":")) {
                            cssClass = "ia-header-text";
                        } else {
                            cssClass = "ia-table-text";
                        }
                    } else {
                        cssClass = "ia-table-text";
                    }
                }
                
                if (cssClass != null) {
                    var classAttr = xml.CreateAttribute("class");
                    classAttr.Value = cssClass;
                    p.Attributes.Append(classAttr);
                    classified++;
                }
            }
            
            previousParagraph = p;
        }
        
        if (classified > 0 || semanticElements > 0) {
            logger.LogInformation("Applied IA CSS classes to {ClassCount} paragraphs and created {SemanticCount} semantic elements", 
                classified, semanticElements);
        }
    }
    
    private static bool TransformToSemanticElement(XmlDocument xml, XmlNode paragraph, string labelText, string semanticElementName, string cssClass) {
        try {
            string content = paragraph.InnerText?.Trim() ?? "";
            
            // Extract the value part after the label
            string valueText = content.Substring(labelText.Length).Trim();
            if (valueText.StartsWith(":")) {
                valueText = valueText.Substring(1).Trim();
            }
            
            // Create the semantic structure: <docTitle>Title</docTitle>: <content>
            var akn = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
            
            // Clear existing content
            paragraph.RemoveAll();
            
            // Add semantic element for the label
            var semanticElement = xml.CreateElement(semanticElementName, akn);
            semanticElement.InnerText = labelText.TrimEnd(':');
            paragraph.AppendChild(semanticElement);
            
            // Add colon and space
            paragraph.AppendChild(xml.CreateTextNode(": "));
            
            // Add the value content (preserve any formatting)
            if (!string.IsNullOrEmpty(valueText)) {
                paragraph.AppendChild(xml.CreateTextNode(valueText));
            }
            
            // Add CSS class to the paragraph
            var classAttr = xml.CreateAttribute("class");
            classAttr.Value = cssClass;
            paragraph.Attributes.Append(classAttr);
            
            return true;
        }
        catch (Exception) {
            // If transformation fails, return false to fall back to CSS-only approach
            return false;
        }
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
