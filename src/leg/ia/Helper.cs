
using System.IO;
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
        XmlDocument xml = Builder.Build(doc);
        docx.Dispose();
        if (simplify)
            Simplifier.Simplify(xml);
        
        // Apply IA-specific style mappings
        ApplyIAStyleMappings(xml);
        
        return new XmlDocument_ { Document = xml };
    }

    private static void ApplyIAStyleMappings(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        var logger = Logging.Factory.CreateLogger<Helper>();
    
        
        var paragraphs = xml.SelectNodes("//akn:p", nsmgr);
        int classified = 0;
        
        foreach (XmlNode p in paragraphs) {
            string content = p.InnerText?.Trim() ?? "";
            string cssClass = null;
            
            // Detect IA-specific content patterns and assign appropriate classes
            // Remove HTML tags for cleaner pattern matching
            string cleanContent = content.Replace("<b>", "").Replace("</b>", "");
            
            if (cleanContent.StartsWith("Title:")) {
                cssClass = "ia-head-label";
            }
            else if (cleanContent.StartsWith("IA No:")) {
                cssClass = "ia-number";
            }
            else if (cleanContent.StartsWith("Stage:")) {
                cssClass = "ia-stage";
            }
            else if (cleanContent.StartsWith("Date:")) {
                cssClass = "ia-head-label";
            }
            else if (cleanContent.StartsWith("Lead department or agency:")) {
                cssClass = "ia-head-label";
            }
            else if (cleanContent.StartsWith("Other departments or agencies")) {
                cssClass = "ia-head-label";
            }
            else if (cleanContent.StartsWith("Impact Assessment")) {
                cssClass = "ia-title";
            }
            else if (cleanContent.Contains("RPC Reference")) {
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

            }
        }
        
        if (classified > 0) {
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
