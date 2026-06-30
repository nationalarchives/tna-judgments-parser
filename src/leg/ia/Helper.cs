
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

    public static IXmlDocument Parse(Stream docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    public static IXmlDocument Parse(byte[] docx, bool simplify = true) {
        return Parse(docx, null, simplify);
    }

    /// <summary>
    /// Parse an Impact Assessment document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document stream</param>
    /// <param name="filename">The filename (e.g., ukia_20250001_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(Stream docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName, bool allowUnrenderedCharts = true, UK.Gov.Legislation.Common.Rendering.IDrawingRenderer renderer = null) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName, allowUnrenderedCharts, renderer);
    }

    /// <summary>
    /// Parse an Impact Assessment document with filename for metadata lookup.
    /// </summary>
    /// <param name="docx">The document bytes</param>
    /// <param name="filename">The filename (e.g., ukia_20250001_en.docx) used for URI and legislation lookup</param>
    /// <param name="simplify">Whether to simplify the output XML</param>
    public static IXmlDocument Parse(byte[] docx, string filename, bool simplify = true, string manifestationName = Builder.DefaultManifestationName, bool allowUnrenderedCharts = true, UK.Gov.Legislation.Common.Rendering.IDrawingRenderer renderer = null) {
        return ((BaseHelper)Instance).Parse(docx, simplify, filename, manifestationName, allowUnrenderedCharts, renderer);
    }

    protected override IDocument ParseDocument(WordprocessingDocument docx, string filename = null) {
        return ImpactAssessments.Parser.Parse(docx, filename);
    }

    protected override void ApplyDocumentSpecificProcessing(XmlDocument xml) {
        MovePrefaceTablesToMainBody(xml);
        AddDateAttributesToDocDate(xml);
        UpdateFRBRDatesFromDocDate(xml);
        TransformHeaderStructure(xml);
        TransformContentSections(xml);
        PromoteCoverSheetToSections(xml);
        BuildSubSectionsFromHeadingDepth(xml);
        BuildAnnexTier(xml);
        RenumberTopLevelMainBodySections(xml);
        RemoveEmptyHeadings(xml);
        ReplaceThWithTd(xml);
        RemoveUnsupportedElements(xml);
        FixSectionHeadingPosition(xml);
        FixNestedAnchors(xml);
        GenerateTableOfContents(xml);
    }

    private const string UKNS = "https://legislation.gov.uk/akn";

    private static string CleanContent(string content) {
        return content.Replace("<b>", "").Replace("</b>", "");
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

        // Strip ordinal suffixes from day numbers (e.g., "14th January 2025" -> "14 January 2025").
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"\b(\d{1,2})(st|nd|rd|th)\b", "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove any remaining trailing punctuation
        cleaned = cleaned.TrimEnd('.', ',', ';', ':');
        
        return cleaned.Trim();
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

    private static void TransformHeaderStructure(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr);
        if (mainBody == null) return;

        var headerLevels = new List<XmlNode>();
        var levels = mainBody.SelectNodes("akn:level", nsmgr);

        var preambleCandidates = new List<XmlNode>();
        bool sawCoverLabel = false;

        foreach (XmlNode level in levels) {
            if (LevelLooksLikeIaCover(level, nsmgr)) {
                if (!sawCoverLabel && preambleCandidates.Count > 0) {
                    headerLevels.AddRange(preambleCandidates);
                    preambleCandidates.Clear();
                }
                headerLevels.Add(level);
                sawCoverLabel = true;
            } else if (!sawCoverLabel && LevelLooksLikeCoverPreamble(level, nsmgr)
                       && preambleCandidates.Count < 2) {
                preambleCandidates.Add(level);
            } else {
                break;
            }
        }

        if (headerLevels.Count == 0) return;

        bool anyHasTable = false;
        foreach (XmlNode level in headerLevels) {
            if (level.SelectSingleNode("akn:content/akn:table", nsmgr) != null) {
                anyHasTable = true;
                break;
            }
        }

        var hcontainer = xml.CreateElement("hcontainer", AKN_NAMESPACE);
        hcontainer.SetAttribute("name", "summary");
        // eId is what the TOC's "#summary" href and the rendered HTML's
        // `id="summary"` resolve against; without it the first TOC link is dead.
        hcontainer.SetAttribute("eId", "summary");

        if (anyHasTable) {
            foreach (XmlNode level in headerLevels) {
                var levelContent = level.SelectSingleNode("akn:content", nsmgr);
                if (levelContent != null)
                    hcontainer.AppendChild(xml.ImportNode(levelContent, true));
            }
        } else {
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
                        td.AppendChild(xml.ImportNode(para, true));
                    }
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

        logger.LogInformation("Wrapped {Count} IA cover level(s) in hcontainer.summary",
            headerLevels.Count);
    }

    private static bool LevelLooksLikeIaCover(XmlNode level, XmlNamespaceManager nsmgr) {
        var content = level.SelectSingleNode("akn:content", nsmgr);
        if (content == null) return false;
        var paragraphs = content.SelectNodes(".//akn:p", nsmgr);
        if (paragraphs == null || paragraphs.Count == 0) return false;

        int limit = System.Math.Min(10, paragraphs.Count);
        for (int i = 0; i < limit; i++) {
            string text = CleanContent(paragraphs[i].InnerText?.Trim() ?? "");
            if (string.IsNullOrEmpty(text)) continue;
            if (IsIaCoverHeading(text)) return true;
        }
        return false;
    }

    private static readonly string[] IaCoverLabelPrefixes = new[] {
        "Title:", "Title of proposal:",
        "IA No:", "IA number:",
        "RPC Reference No:", "RPC reference number:",
        "Lead department or agency:", "Lead department:",
        "Other departments or agencies:", "Other departments:",
        "Department or agency:",
        "Date:", "Stage:",
        "Source of intervention:",
        "Type of measure:", "Type of Impact Assessment:", "Type of Impact Assessment",
        "Contact for enquiries:", "Contact:"
    };

    internal static bool IsIaCoverHeading(string text) {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.StartsWith("Impact Assessment", System.StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (var prefix in IaCoverLabelPrefixes) {
            if (text.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool LevelLooksLikeCoverPreamble(XmlNode level, XmlNamespaceManager nsmgr) {
        var content = level.SelectSingleNode("akn:content", nsmgr);
        if (content == null) return false;
        var paragraphs = content.SelectNodes(".//akn:p", nsmgr);
        if (paragraphs == null || paragraphs.Count == 0 || paragraphs.Count > 2)
            return false;
        string text = CleanContent(paragraphs[0].InnerText?.Trim() ?? "");
        if (string.IsNullOrEmpty(text) || text.Length > 80) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(
            text, @"\bimpact\s+assessment\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
    internal static bool IsSectionHeaderParagraph(XmlNode paragraph, XmlNamespaceManager nsmgr, out string headingText) {
        headingText = null;

        // Check if <num> contains <b>. Use descendant-or-self because LEG-150's
        // colour-preservation work wraps inline runs in <span style="color:...">,
        // so <b> may be a grandchild rather than a direct child of <num>/<p>.
        // A direct-child query silently degrades section detection — see
        // src/leg/spec/LEG-151-ia-toc-refactor.md for the impact measurement.
        var num = paragraph.SelectSingleNode("akn:num", nsmgr);
        if (num == null) return false;

        var numBold = num.SelectSingleNode(".//akn:b", nsmgr);
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

        var firstPBold = firstP.SelectSingleNode(".//akn:b", nsmgr);
        if (firstPBold == null) return false;

        string boldText = firstPBold.InnerText?.Trim() ?? "";

        // Must be substantive but heading-like (not too long)
        if (boldText.Length < 5 || boldText.Length > 200) return false;

        if (IsNonHeadingLabel(boldText)) return false;

        // Extract heading (remove trailing colons)
        headingText = boldText.Replace(":", "").Trim();
        return true;
    }

    /// <summary>
    /// Check if a level element is a section header based on structural patterns.
    /// Pattern: Level with single bold paragraph that looks like a heading
    /// </summary>
    internal static bool IsSectionHeaderLevel(XmlNode level, XmlNamespaceManager nsmgr, out string headingText) {
        headingText = null;

        var content = level.SelectSingleNode("akn:content", nsmgr);
        if (content == null) return false;

        // Should have a single paragraph with bold text
        var paragraphs = content.SelectNodes("akn:p", nsmgr);
        if (paragraphs.Count != 1) return false;

        var firstP = paragraphs[0];
        // Descendant-or-self: see comment on IsSectionHeaderParagraph above.
        var boldElement = firstP.SelectSingleNode(".//akn:b", nsmgr);
        if (boldElement == null) return false;

        string boldText = boldElement.InnerText?.Trim() ?? "";

        // Must be heading-like: substantive but short
        if (boldText.Length < 5 || boldText.Length > 150) return false;

        // Check if the bold text is the primary content (not just a small part)
        string totalText = firstP.InnerText?.Trim() ?? "";
        if (boldText.Length < totalText.Length * 0.5) return false;

        if (IsNonHeadingLabel(boldText)) return false;

        headingText = boldText.Replace(":", "").Trim();
        return true;
    }

    internal static bool IsFigureOrTableCaption(string text) {
        if (string.IsNullOrEmpty(text)) return false;
        // Caption word followed by a number ("Table 40") or a single letter label
        // ("Table A:", "Figure B"). The \b stops it matching real headings whose
        // next word merely starts with a capital ("Table of contents").
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"^(Figure|Fig\.?|Table|Chart|Box|Diagram|Map|Exhibit|Graph)\s*([0-9]+|[A-Z])\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    // Table/figure notes and cross-references that are sometimes bolded like a
    // heading but are not section titles ("Note ...", "See Annex A", "Source: ...").
    // "Sources:" with a colon is a caption; "Sources of ..." without one is a real
    // heading, so the colon is required for the Source(s) case.
    internal static bool IsTableNoteOrReference(string text) {
        if (string.IsNullOrEmpty(text)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(
                text, @"^(Notes?|See)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(
                text, @"^Sources?\s*:", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    // A bold line that reads as a non-heading label (figure/table caption, or a
    // note/source/cross-reference). The promotion heuristics keep these as body
    // content instead of turning them into sections (and so into TOC entries).
    internal static bool IsNonHeadingLabel(string text) =>
        IsFigureOrTableCaption(text) || IsTableNoteOrReference(text);

    /// Promote chapter headings detected from Word style metadata
    /// (uk:headingDepth set by Builder; see DOCX.Styles.ClassifyHeading)
    /// into top-level sibling sections under mainBody. Each chapter gets
    /// its own /section/N URL — the publisher only resolves hierarchical
    /// /section/N paths, not eId fragments, so nesting would orphan the
    /// chapters from the TOC.
    private static void BuildSubSectionsFromHeadingDepth(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        nsmgr.AddNamespace("uk", UKNS);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr) as XmlElement;
        if (mainBody is null) return;

        var sections = mainBody.SelectNodes("akn:section", nsmgr).Cast<XmlElement>().ToList();

        int totalPromoted = 0;
        foreach (var hostSection in sections) {
            var orderedNodes = new List<XmlElement>();
            CollectFlatBlockSequence(hostSection, orderedNodes);

            // Demote Visual-tier headings that lack enough body in their
            // subtree — drops signature blocks/address lines that share a
            // Word style with real chapter headings (Ofcom RIA template).
            var demoted = ComputeDemotedHeadings(orderedNodes, nsmgr);

            // Body collected before the first promoted heading stays in
            // the host section (e.g. tables introducing "Regulatory
            // scorecard" before "Part A"/"Part B" sub-headings).
            var hostKeep = new List<XmlElement>();
            var groups = new List<(string heading, List<XmlElement> body)>();
            string currentHeading = null;
            var currentBody = new List<XmlElement>();

            foreach (var el in orderedNodes) {
                int? depth = ReadHeadingDepth(el, nsmgr);
                if (depth is int && !demoted.Contains(el)) {
                    string headingText = ReadHeadingText(el, nsmgr);
                    if (string.IsNullOrWhiteSpace(headingText) || IsNonHeadingLabel(headingText)) {
                        (currentHeading == null ? hostKeep : currentBody).Add(el);
                        continue;
                    }
                    if (currentHeading != null)
                        groups.Add((currentHeading, currentBody));
                    currentHeading = headingText;
                    currentBody = new List<XmlElement>();
                } else {
                    (currentHeading == null ? hostKeep : currentBody).Add(el);
                }
            }
            if (currentHeading != null)
                groups.Add((currentHeading, currentBody));

            if (groups.Count < 2) continue;

            var siblings = new List<XmlElement>();
            foreach (var (heading, body) in groups) {
                var sib = xml.CreateElement("section", AKN_NAMESPACE);
                var h = xml.CreateElement("heading", AKN_NAMESPACE);
                h.InnerText = heading;
                sib.AppendChild(h);
                AppendBodyToSection(xml, sib, body);
                siblings.Add(sib);
            }

            // Only elements that ended up in the new siblings should be
            // removed from the host; hostKeep stays put.
            var consumed = new HashSet<XmlElement>(orderedNodes.Except(hostKeep));
            RemoveConsumedFromHost(hostSection, consumed);

            // Insert chapters as siblings immediately after the wrapper,
            // preserving document order.
            XmlNode insertAfter = hostSection;
            foreach (var sib in siblings) {
                mainBody.InsertAfter(sib, insertAfter);
                insertAfter = sib;
            }

            totalPromoted += siblings.Count;
            logger.LogInformation(
                "Promoted {Count} chapters from {EId} to top-level siblings",
                siblings.Count, hostSection.GetAttribute("eId"));
        }

        if (totalPromoted > 0)
            logger.LogInformation("Promoted {Count} chapters in total", totalPromoted);
    }

    /// Convert the cover-sheet hcontainer (name="summary") and any orphan
    /// level elements into top-level sections so the TOC can address them
    /// via /section/N paths (the publisher doesn't resolve fragments).
    private static void PromoteCoverSheetToSections(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr) as XmlElement;
        if (mainBody is null) return;

        int promoted = 0;
        var summary = mainBody.SelectSingleNode("akn:hcontainer[@name='summary']", nsmgr) as XmlElement;
        if (summary is not null) {
            var section = WrapAsSection(xml, summary, "Summary");
            // Preserve the "summary" marker for CSS targeting the
            // cover-sheet table layout. AKN allows @class on hierarchy.
            section.SetAttribute("class", "summary");
            mainBody.ReplaceChild(section, summary);
            promoted++;
        }

        foreach (var level in mainBody.SelectNodes("akn:level", nsmgr).Cast<XmlElement>().ToList()) {
            string heading = ExtractCoverSheetHeading(level, nsmgr);
            if (string.IsNullOrEmpty(heading)) continue;
            var section = WrapAsSection(xml, level, heading);
            // Mark as cover-sheet so the XSL hides the synthesized heading
            // (the styled question is preserved as the visible header in the
            // body content). Other num-less sections like "Declaration"
            // don't carry this class and render normally.
            section.SetAttribute("class", "summary");
            mainBody.ReplaceChild(section, level);
            promoted++;
        }

        if (promoted > 0)
            logger.LogInformation("Promoted {Count} cover-sheet elements to sections", promoted);
    }

    private static XmlElement WrapAsSection(XmlDocument xml, XmlElement source, string headingText) {
        var section = xml.CreateElement("section", AKN_NAMESPACE);
        var heading = xml.CreateElement("heading", AKN_NAMESPACE);
        heading.InnerText = headingText;
        section.AppendChild(heading);
        foreach (XmlNode c in source.ChildNodes.Cast<XmlNode>().ToList())
            section.AppendChild(c);
        return section;
    }

    // Cover-sheet levels typically start with a question paragraph; the
    // text up to the first "?" is the heading, the answer follows in body.
    private static string ExtractCoverSheetHeading(XmlElement level, XmlNamespaceManager nsmgr) {
        var firstP = level.SelectSingleNode(".//akn:p", nsmgr) as XmlElement;
        string text = firstP?.InnerText?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return null;
        int q = text.IndexOf('?');
        if (q >= 0 && q + 1 < text.Length) return text.Substring(0, q + 1);
        return text.Length > 200 ? text.Substring(0, 200) : text;
    }

    /// Promote each annex (an attached <doc name="annex"> of flat bold-heading
    /// paragraphs) into ordinary <section>s appended to the main body, so the
    /// appendix content continues as /section/N like any other section. The
    /// appendix's own title (e.g. "Appendix A: ...") becomes the heading of a
    /// leading section. RenumberTopLevelMainBodySections then renumbers every
    /// top-level section contiguously.
    private static void BuildAnnexTier(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var logger = Logging.Factory.CreateLogger<Helper>();

        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr) as XmlElement;
        if (mainBody is null) return;
        var attachments = xml.SelectSingleNode("//akn:attachments", nsmgr) as XmlElement;
        if (attachments is null) return;

        var annexDocs = attachments.SelectNodes("akn:attachment/akn:doc[@name='annex']", nsmgr)
            .Cast<XmlElement>().ToList();
        if (annexDocs.Count == 0) return;

        int annexIndex = 0;
        foreach (var annexDoc in annexDocs) {
            annexIndex++;
            var annexBody = annexDoc.SelectSingleNode("akn:mainBody", nsmgr) as XmlElement;
            if (annexBody is null) continue;

            var blocks = annexBody.ChildNodes.Cast<XmlNode>().OfType<XmlElement>().ToList();

            // Group flat blocks by bold-heading paragraph (excluding figure/table
            // captions, which are content). Content before the first heading is
            // "leading" (typically just the annex's own title).
            var leading = new List<XmlElement>();
            var groups = new List<(string heading, List<XmlElement> body)>();
            string currentHeading = null;
            var currentBody = new List<XmlElement>();
            foreach (var b in blocks) {
                if (IsFlatBoldHeadingParagraph(b, nsmgr, out string h) && !IsNonHeadingLabel(h)) {
                    if (currentHeading != null) groups.Add((currentHeading, currentBody));
                    currentHeading = h;
                    currentBody = new List<XmlElement>();
                } else {
                    (currentHeading == null ? leading : currentBody).Add(b);
                }
            }
            if (currentHeading != null) groups.Add((currentHeading, currentBody));

            // The annex's own title is the first leading bold paragraph (e.g.
            // "Appendix A: ..."), too long to be a section heading; when present it
            // becomes the heading of the leading section. We do not fabricate an
            // "Annex" heading when the source has none.
            string annexTitle = null;
            var leadingTitle = leading.FirstOrDefault(e =>
                e.LocalName == "p" && e.SelectSingleNode("akn:b", nsmgr) != null
                && !IsNonHeadingLabel(e.InnerText?.Trim()));
            if (leadingTitle != null) {
                annexTitle = leadingTitle.InnerText?.Trim();
                leading.Remove(leadingTitle);
            }

            int sectionIndex = 0;
            void AppendSection(string heading, List<XmlElement> body) {
                sectionIndex++;
                var sec = xml.CreateElement("section", AKN_NAMESPACE);
                if (!string.IsNullOrWhiteSpace(heading)) {
                    var hEl = xml.CreateElement("heading", AKN_NAMESPACE);
                    hEl.InnerText = heading;
                    sec.AppendChild(hEl);
                }
                // Wrap loose blocks in paragraph/content (a section cannot hold p/
                // table/blockContainer directly); same builder the body sections use.
                AppendBodyToSection(xml, sec, body);
                // A section must have a content or structural child; if it carried
                // only a heading (e.g. a title-only leading section), give it an
                // empty <content/> so it stays schema-valid.
                bool hasBody = false;
                foreach (XmlNode ch in sec.ChildNodes) {
                    if (ch is not XmlElement ce) continue;
                    if (ce.LocalName == "heading" || ce.LocalName == "num") continue;
                    hasBody = true;
                    break;
                }
                if (!hasBody) sec.AppendChild(xml.CreateElement("content", AKN_NAMESPACE));
                mainBody.AppendChild(sec);
            }

            // A genuine appendix title opens its own section (so it shows in the
            // TOC), carrying any remaining leading content as its body. With no
            // title, any leading content opens an untitled section; otherwise the
            // content sections follow directly.
            if (annexTitle != null) {
                AppendSection(annexTitle, leading);
            } else if (leading.Count > 0) {
                AppendSection(null, leading);
            }
            foreach (var (heading, body) in groups) AppendSection(heading, body);

            logger.LogInformation("Appended annex {Index} ({Title}) as {Count} body sections",
                annexIndex, annexTitle ?? "untitled", sectionIndex);
        }

        attachments.ParentNode?.RemoveChild(attachments);
    }

    // Renumber top-level sections sequentially so /section/N URLs stay
    // contiguous after promotions. Starts from whatever number the first
    // existing section has (typically 2 — section_1 is reserved for the
    // preface in this pipeline).
    private static void RenumberTopLevelMainBodySections(XmlDocument xml) {
        var nsmgr = new XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        var mainBody = xml.SelectSingleNode("//akn:mainBody", nsmgr) as XmlElement;
        if (mainBody is null) return;
        var sections = mainBody.SelectNodes("akn:section", nsmgr).Cast<XmlElement>().ToList();
        if (sections.Count == 0) return;

        int startNum = 2;
        var firstEId = sections[0].GetAttribute("eId");
        var match = System.Text.RegularExpressions.Regex.Match(firstEId, @"section_(\d+)$");
        if (match.Success) startNum = int.Parse(match.Groups[1].Value);

        for (int i = 0; i < sections.Count; i++) {
            sections[i].SetAttribute("eId", $"section_{startNum + i}");
        }
    }

    /// Read uk:headingDepth from the element directly, or from its inner
    /// content/p (Builder may have placed the attribute on the <p>).
    private static int? ReadHeadingDepth(XmlElement el, XmlNamespaceManager nsmgr) {
        string raw = el.GetAttribute("headingDepth", UKNS);
        if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int d)) return d;
        var firstP = el.SelectSingleNode("akn:content/akn:p", nsmgr) as XmlElement
                  ?? el.SelectSingleNode("akn:p", nsmgr) as XmlElement;
        if (firstP is null) return null;
        raw = firstP.GetAttribute("headingDepth", UKNS);
        if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int d2)) return d2;
        return null;
    }

    // Prefer inner content/p text so paragraph nums (e.g. "404.") don't
    // bleed into the heading.
    private static string ReadHeadingText(XmlElement el, XmlNamespaceManager nsmgr) {
        var p = el.SelectSingleNode("akn:content/akn:p", nsmgr) as XmlElement
             ?? el.SelectSingleNode("akn:p", nsmgr) as XmlElement;
        if (p is not null) return p.InnerText?.Trim() ?? "";
        return el.InnerText?.Trim() ?? "";
    }

    private static string ReadHeadingSignal(XmlElement el, XmlNamespaceManager nsmgr) {
        string raw = el.GetAttribute("headingSignal", UKNS);
        if (!string.IsNullOrEmpty(raw)) return raw;
        var firstP = el.SelectSingleNode("akn:content/akn:p", nsmgr) as XmlElement
                  ?? el.SelectSingleNode("akn:p", nsmgr) as XmlElement;
        if (firstP is null) return null;
        raw = firstP.GetAttribute("headingSignal", UKNS);
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    // Visual-tier heading must accumulate at least this much body text in
    // its subtree (up to the next heading of equal or shallower depth) to
    // be kept; otherwise it's demoted to body. Filters out signature
    // blocks and address lines styled the same as chapter headings.
    private const int VisualHeadingMinSubtreeBodyChars = 100;

    private static HashSet<XmlElement> ComputeDemotedHeadings(
            List<XmlElement> orderedNodes, XmlNamespaceManager nsmgr) {
        var demoted = new HashSet<XmlElement>();

        var headings = new List<(int idx, int depth, string signal)>();
        for (int i = 0; i < orderedNodes.Count; i++) {
            int? d = ReadHeadingDepth(orderedNodes[i], nsmgr);
            if (d is int dd) {
                string sig = ReadHeadingSignal(orderedNodes[i], nsmgr) ?? "Authoritative";
                headings.Add((i, dd, sig));
            }
        }
        if (headings.Count == 0) return demoted;

        var headingIdxs = new HashSet<int>(headings.Select(h => h.idx));
        for (int h = 0; h < headings.Count; h++) {
            var (idx, depth, signal) = headings[h];
            if (signal != "Visual") continue;

            int endIdx = orderedNodes.Count;
            for (int n = h + 1; n < headings.Count; n++) {
                if (headings[n].depth <= depth) { endIdx = headings[n].idx; break; }
            }

            int bodyChars = 0;
            for (int j = idx + 1; j < endIdx; j++) {
                if (headingIdxs.Contains(j)) continue;
                bodyChars += orderedNodes[j].InnerText?.Length ?? 0;
                if (bodyChars >= VisualHeadingMinSubtreeBodyChars) break;
            }

            if (bodyChars < VisualHeadingMinSubtreeBodyChars)
                demoted.Add(orderedNodes[idx]);
        }
        return demoted;
    }

    private static void CollectFlatBlockSequence(XmlElement host, List<XmlElement> sink) {
        var nsmgr = new XmlNamespaceManager(host.OwnerDocument.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);
        foreach (XmlNode child in host.ChildNodes) {
            if (child is not XmlElement el) continue;
            if (el.LocalName == "section") continue;
            if (el.LocalName == "heading" || el.LocalName == "num"
                    || el.LocalName == "intro" || el.LocalName == "wrapUp") continue;
            if (el.LocalName == "paragraph") {
                // Numless paragraph: a wrapper the parser uses to bag
                // headings + body together under one section. Flatten its
                // <content>'s children so internal headings separate from
                // body during grouping.
                bool hasNum = el.SelectSingleNode("akn:num", nsmgr) is not null;
                if (!hasNum) {
                    var contentEl = el.SelectSingleNode("akn:content", nsmgr) as XmlElement;
                    if (contentEl is not null) {
                        foreach (XmlNode ck in contentEl.ChildNodes)
                            if (ck is XmlElement cce) sink.Add(cce);
                        continue;
                    }
                }
                // Numbered paragraph with structural children — recurse
                // to find heading-marked subparagraphs.
                if (HasStructuralChildren(el)) {
                    CollectFlatBlockSequence(el, sink);
                    continue;
                }
                // Numbered atomic: stays as one body element.
            }
            sink.Add(el);
        }
    }

    // AKN section's content model is exclusive: either bare <content>, or
    // a sequence of structural children (paragraph/level/etc.). When body
    // mixes raw blocks (p, table) with structural elements, wrap each
    // raw-block run in <paragraph><content>...</content></paragraph> so
    // everything is structural.
    private static void AppendBodyToSection(XmlDocument xml, XmlElement section, List<XmlElement> body) {
        XmlElement openContent = null;
        foreach (var b in body) {
            var clone = (XmlElement)b.CloneNode(true);
            bool isStructural = b.LocalName == "paragraph" || b.LocalName == "subparagraph"
                || b.LocalName == "level" || b.LocalName == "section"
                || b.LocalName == "hcontainer";
            if (isStructural) {
                section.AppendChild(clone);
                openContent = null;
            } else {
                if (openContent is null) {
                    var wrapper = xml.CreateElement("paragraph", AKN_NAMESPACE);
                    openContent = xml.CreateElement("content", AKN_NAMESPACE);
                    wrapper.AppendChild(openContent);
                    section.AppendChild(wrapper);
                }
                openContent.AppendChild(clone);
            }
        }
    }

    private static bool HasStructuralChildren(XmlElement el) {
        foreach (XmlNode child in el.ChildNodes) {
            if (child is not XmlElement ce) continue;
            if (ce.LocalName == "subparagraph" || ce.LocalName == "level"
                    || ce.LocalName == "section" || ce.LocalName == "paragraph")
                return true;
        }
        return false;
    }

    private static void RemoveConsumedFromHost(XmlElement host, HashSet<XmlElement> consumed) {
        var nsmgr = new XmlNamespaceManager(host.OwnerDocument.NameTable);
        nsmgr.AddNamespace("akn", AKN_NAMESPACE);

        var wrappers = host.SelectNodes("akn:paragraph", nsmgr).Cast<XmlElement>().ToList();

        foreach (var c in consumed) {
            c.ParentNode?.RemoveChild(c);
        }

        // Drop wrappers left with no body — only num/empty after consumption.
        foreach (var w in wrappers) {
            if (!string.IsNullOrWhiteSpace(w.InnerText)) continue;
            if (w.SelectNodes("akn:subparagraph", nsmgr).Count > 0) continue;
            if (w.SelectNodes("akn:paragraph", nsmgr).Count > 0) continue;
            host.RemoveChild(w);
        }
    }

    private static void TransformToSemanticSection(XmlDocument xml, XmlNode element, int sectionNumber, string headingText, List<XmlNode> following) {
        var section = xml.CreateElement("section", AKN_NAMESPACE);
        section.SetAttribute("eId", $"section_{sectionNumber}");

        // Add num from header if present (paragraph has num, level might not)
        var num = element.SelectSingleNode("akn:num", CreateNsMgr(xml));
        if (num != null) {
            section.AppendChild(num);
        }

        // Add heading if we have one. Where the source's first <p> contains
        // the heading text exactly (typical for level-promoted sections like
        // "Declaration" and numbered headers like "1. Summary of proposal"),
        // move its inline children into <heading> so DOCX styling (b, span
        // colour) survives. Fall back to plain text otherwise.
        if (!string.IsNullOrEmpty(headingText)) {
            var heading = xml.CreateElement("heading", AKN_NAMESPACE);
            var nsm = CreateNsMgr(xml);
            var sourceP = element.SelectSingleNode("akn:content/akn:p", nsm) as XmlElement;
            string srcText = (sourceP?.InnerText ?? "").Trim().TrimEnd(':', ';', '.').Trim();
            string hdrText = headingText.Trim().TrimEnd(':', ';', '.').Trim();
            if (sourceP is not null && srcText.Equals(hdrText, System.StringComparison.OrdinalIgnoreCase)) {
                foreach (XmlNode c in sourceP.ChildNodes.Cast<XmlNode>().ToList())
                    heading.AppendChild(c);
                sourceP.ParentNode?.RemoveChild(sourceP);
            } else {
                heading.InnerText = headingText;
            }
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
        // 1) Build intro from header's content. Numbered sections render
        //    their <heading> inline with <num>; the parser also leaves the
        //    heading text as a leading <p> in the source content. Skip
        //    that <p> so the title isn't rendered twice.
        XmlElement intro = null;
        var contentEl = element.SelectSingleNode("akn:content", CreateNsMgr(xml));
        if (contentEl != null && contentEl.HasChildNodes) {
            intro = xml.CreateElement("intro", AKN_NAMESPACE);
            string normHeading = (headingText ?? "").Trim();
            while (contentEl.HasChildNodes) {
                var child = contentEl.FirstChild;
                contentEl.RemoveChild(child);
                if (child is XmlElement ce && ce.LocalName == "p"
                        && string.Equals((ce.InnerText ?? "").Trim(), normHeading,
                            System.StringComparison.OrdinalIgnoreCase))
                    continue;
                intro.AppendChild(child);
            }
            if (intro.HasChildNodes)
                section.AppendChild(intro);
            else
                intro = null;
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
        
        // The IA subschema permits <marker name="tab"> but no other marker types.
        // Strip anything else; keep tab markers so paragraph tab stops survive to HTML.
        var markers = xml.SelectNodes("//akn:marker[@name != 'tab']", nsmgr);
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

                AddTocItem(xml, toc, href, null, headingText);
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

                AddTocItem(xml, toc, href, SectionDisplayNum(section, nsmgr), headingText);
                tocNumber++;
            }
        }

        // Insert TOC at the beginning of mainBody
        if (toc.HasChildNodes) {
            mainBody.InsertBefore(toc, mainBody.FirstChild);
            logger.LogInformation("Generated TOC with {Count} entries", toc.ChildNodes.Count);
        }
    }

    private static bool IsFlatBoldHeadingParagraph(XmlElement paragraph, XmlNamespaceManager nsmgr, out string headingText) {
        headingText = null;
        // A paragraph carrying an image is content (e.g. a chart with a bold caption),
        // not a heading; treating it as one discards the image, since the annex tier
        // builds headings as text only (LEG-162).
        if (paragraph.SelectSingleNode(".//akn:img", nsmgr) != null) return false;
        string fullText = paragraph.InnerText?.Trim() ?? "";
        if (fullText.Length < 3 || fullText.Length > 200) return false;
        var bold = paragraph.SelectSingleNode("akn:b", nsmgr);
        if (bold == null) return false;
        string boldText = bold.InnerText?.Trim() ?? "";
        if (boldText.Length < 3) return false;
        if (boldText.Length < fullText.Length - 3) return false;
        string trimmed = boldText.TrimEnd(':', '.', ' ');
        if (string.IsNullOrEmpty(trimmed)) return false;
        headingText = trimmed;
        return true;
    }

    /// <summary>
    /// Add a TOC item to the TOC element. Prefixes the document's own number
    /// (<paramref name="num"/>, e.g. "1." or "A.") when the section has one;
    /// otherwise shows the heading text alone. legislation.gov.uk numbers a TOC
    /// entry only where the content is numbered, so we never synthesise one.
    /// </summary>
    private static void AddTocItem(XmlDocument xml, XmlElement toc, string href, string num, string headingText) {
        var tocItem = xml.CreateElement("tocItem", AKN_NAMESPACE);
        tocItem.SetAttribute("href", href);
        tocItem.SetAttribute("level", "2");

        var inlineHeading = xml.CreateElement("inline", AKN_NAMESPACE);
        inlineHeading.SetAttribute("name", "tocHeading");
        inlineHeading.InnerText = string.IsNullOrEmpty(num) ? headingText : $"{num} {headingText}";
        tocItem.AppendChild(inlineHeading);

        toc.AppendChild(tocItem);
    }

    /// The document's own number for this section (e.g. "1.", "A."), whitespace-
    /// collapsed, or null when the section carries no number.
    private static string SectionDisplayNum(XmlElement section, XmlNamespaceManager nsmgr) {
        var num = section.SelectSingleNode("akn:num", nsmgr);
        string text = num?.InnerText?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
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

                    // Skip captions/notes/references — they are not headings.
                    if (IsNonHeadingLabel(text)) continue;

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
                    if (!string.IsNullOrEmpty(text) && !IsNonHeadingLabel(text)) {
                        return TruncateHeading(text);
                    }
                }
            }
        }
        
        // Strategy 4: Try first paragraph text
        var firstPara = element.SelectSingleNode(".//akn:p[normalize-space()]", nsmgr);
        if (firstPara != null) {
            string text = firstPara.InnerText?.Trim();
            if (!string.IsNullOrEmpty(text) && text.Length > 5 && !IsNonHeadingLabel(text)) {
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
        // Collapse whitespace; no length cap.
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }

}

}

