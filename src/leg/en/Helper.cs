
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

    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
    }

    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename);
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
        nsmgr.AddNamespace("uk", "https://legislation.gov.uk/akn");
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

        int tocNumber = 1;

        // Add paragraph entries (paragraphs with intro)
        var paragraphsWithIntro = xml.SelectNodes("//akn:mainBody/akn:paragraph[akn:intro]", nsmgr);
        if (paragraphsWithIntro != null && paragraphsWithIntro.Count > 0) {
            foreach (XmlElement paragraph in paragraphsWithIntro) {
                AddTocItemFromParagraph(xml, toc, paragraph, expressionUri, nsmgr, tocNumber);
                tocNumber++;
            }
        }

        // Add section entries (sections with headings)
        var sectionsWithHeadings = xml.SelectNodes("//akn:mainBody/akn:section[akn:heading]", nsmgr);
        if (sectionsWithHeadings != null && sectionsWithHeadings.Count > 0) {
            foreach (XmlElement section in sectionsWithHeadings) {
                AddTocItemFromSection(xml, toc, section, expressionUri, nsmgr, tocNumber);
                tocNumber++;
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

    private static void AddTocItemFromParagraph(XmlDocument xml, XmlElement toc, XmlElement paragraph, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
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

        var tocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        tocItem.SetAttribute("href", href);
        tocItem.SetAttribute("level", "2");
        var inlineHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        inlineHeading.SetAttribute("name", "tocHeading");
        inlineHeading.InnerText = $"{tocNumber}. {headingText}";
        tocItem.AppendChild(inlineHeading);
        toc.AppendChild(tocItem);
    }

    private static void AddTocItemFromSection(XmlDocument xml, XmlElement toc, XmlElement section, string expressionUri, XmlNamespaceManager nsmgr, int tocNumber) {
        string eId = section.GetAttribute("eId");
        var numElement = section.SelectSingleNode("akn:num", nsmgr);
        if (numElement == null) return;

        string numText = numElement.InnerText?.Trim();
        if (string.IsNullOrEmpty(numText)) return;

        string sectionNumber = Regex.Replace(numText, @"[^\d]", "");
        if (string.IsNullOrEmpty(sectionNumber)) return;

        var heading = section.SelectSingleNode("akn:heading", nsmgr);
        if (heading == null) return;

        string headingText = heading.InnerText?.Trim();
        if (string.IsNullOrEmpty(headingText)) return;

        if (headingText.Length > 100)
            headingText = headingText.Substring(0, 97) + "...";

        if (string.IsNullOrEmpty(eId)) {
            eId = $"section_{sectionNumber}";
            section.SetAttribute("eId", eId);
        }

        string href = !string.IsNullOrEmpty(expressionUri)
            ? $"{expressionUri}/section/{sectionNumber}"
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

}

}
