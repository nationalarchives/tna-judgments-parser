
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.AkomaNtoso;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.ImpactAssessments {

class Helper {

    public static IXmlDocument Parse(Stream docx, bool simplify = true) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify);
    }



    public static IXmlDocument Parse(byte[] docx, bool simplify = true) {
        WordprocessingDocument word = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.Read(docx);
        return Parse(word, simplify);
    }

    private static IXmlDocument Parse(WordprocessingDocument docx, bool simplify) {
        IDocument doc = ImpactAssessments.Parser.Parse(docx);
        
        // Style analysis can be enabled for debugging by setting an environment variable
        var logger = Logging.Factory.CreateLogger<Helper>();
        bool debugStyles = Environment.GetEnvironmentVariable("IA_DEBUG_STYLES") == "true";
        if (debugStyles) {
            var divided = doc as DividedDocument;
            if (divided != null) {
                Console.WriteLine("=== IA Document Style Analysis ===");
                var uniqueStyles = new HashSet<string>();
                foreach (var division in divided.Body.Take(10)) {
                    if (division is ILeaf leaf) {
                        foreach (var block in leaf.Contents.Take(3)) {
                            if (block is ILine line && line.Style != null) {
                                uniqueStyles.Add(line.Style);
                            }
                        }
                    }
                }
                Console.WriteLine($"Unique styles found: {string.Join(", ", uniqueStyles.OrderBy(s => s))}");
            }
        }
        
        XmlDocument xml = Builder.Build(doc);
        docx.Dispose();
        if (simplify)
            Simplifier.Simplify(xml);
        
        // Apply IA-specific style mappings and log what we find
        ApplyIAStyleMappings(xml, debugStyles);
        
        // Re-enable validation but don't fail parsing
        try {
            var validator = new Validator();
            var errors = validator.Validate(xml);
            if (errors.Count > 0) {
                logger.LogWarning("IA validation found {Count} schema errors", errors.Count);
                foreach (var error in errors) {
                    logger.LogWarning("Schema error: {Message}", error.Message);
                }
            }
        } catch (System.Exception ex) {
            logger.LogError("Schema validation failed: {Message}", ex.Message);
        }
        
        return new XmlDocument_ { Document = xml };
    }

    private static void ApplyIAStyleMappings(XmlDocument xml, bool debugMode = false) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        var logger = Logging.Factory.CreateLogger<Helper>();
        
        // Since Word styles are not being preserved in the pipeline, 
        // we'll use content-based detection to apply IA-specific CSS classes
        
        var paragraphs = xml.SelectNodes("//akn:p", nsmgr);
        int classified = 0;
        
        foreach (XmlNode p in paragraphs) {
            string content = p.InnerText?.Trim() ?? "";
            string cssClass = null;
            
            // Detect IA-specific content patterns and assign appropriate classes
            if (content.StartsWith("Title:") || content.Contains("<b>Title:</b>")) {
                cssClass = "ia-head-label";
            }
            else if (content.StartsWith("IA No:") || content.Contains("<b>IA No:</b>")) {
                cssClass = "ia-number";
            }
            else if (content.StartsWith("Stage:") || content.Contains("<b>Stage:</b>")) {
                cssClass = "ia-stage";
            }
            else if (content.StartsWith("Date:") || content.Contains("<b>Date</b>:")) {
                cssClass = "ia-head-label";
            }
            else if (content.StartsWith("Other departments") || content.Contains("<b>Other departments")) {
                cssClass = "ia-head-label";
            }
            else if (content.StartsWith("Impact Assessment") || content == "Impact Assessment, The Home Office") {
                cssClass = "ia-title";
            }
            else if (content.StartsWith("RPC Reference") || content.Contains("RPC Reference")) {
                cssClass = "ia-head-label";
            }
            else if (IsInHeaderTable(p)) {
                cssClass = "ia-table-text";
            }
            
            if (cssClass != null) {
                var classAttr = xml.CreateAttribute("class");
                classAttr.Value = cssClass;
                p.Attributes.Append(classAttr);
                classified++;
                if (debugMode) {
                    Console.WriteLine($"Applied IA class '{cssClass}' to: {content.Substring(0, Math.Min(50, content.Length))}...");
                }
            }
        }
        
        if (debugMode || classified > 0) {
            logger.LogInformation("Applied IA CSS classes to {Count} paragraphs", classified);
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
