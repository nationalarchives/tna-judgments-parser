using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using DocxStyles = UK.Gov.Legislation.Judgments.DOCX.Styles;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Word-style heading classification used by the leg pipeline for
/// section-structure inference (IA in particular reads
/// <c>uk:headingDepth</c> / <c>uk:headingSignal</c> attributes off
/// each block to recover chapter and sub-section boundaries).
///
/// The logic uses standard OOXML knobs only — <c>outlineLvl</c>,
/// <c>"Heading\d"</c> style names, bold + font-size visual heuristics —
/// but it lives in <c>src/leg/</c> because no non-leg consumer needs
/// this classification. Judgment / press-summary / lawmaker AKN doesn't
/// surface heading depth.
/// </summary>
internal static class LegHeadingClassifier {

    internal enum HeadingSignal {
        Authoritative,
        Visual
    }

    internal readonly record struct HeadingClassification(int Depth, HeadingSignal Signal);

    /// <summary>
    /// Returns depth (1..6) + signal tier, or null for body paragraphs.
    /// Tries: outlineLvl on the style or its basedOn chain, "Heading\d"
    /// name match, then bold + size as a visual fallback.
    /// </summary>
    internal static HeadingClassification? Classify(MainDocumentPart main, string styleId) {
        if (main is null || string.IsNullOrEmpty(styleId)) return null;
        Style style = DocxStyles.GetStyle(main, styleId);
        if (style is null) return null;

        var outlineLvl = DocxStyles.GetInheritedProperty(style, s => s.StyleParagraphProperties?.OutlineLevel);
        if (outlineLvl?.Val?.Value is int level && level >= 0 && level < 6)
            return new HeadingClassification(level + 1, HeadingSignal.Authoritative);

        var m = Regex.Match(styleId, @"^heading\s*(\d)$", RegexOptions.IgnoreCase);
        if (m.Success) {
            int d = int.Parse(m.Groups[1].Value);
            if (d >= 1 && d <= 6) return new HeadingClassification(d, HeadingSignal.Authoritative);
        }

        var bold = DocxStyles.GetInheritedProperty(style, s => s.StyleRunProperties?.Bold);
        var fontSize = DocxStyles.GetInheritedProperty(style, s => s.StyleRunProperties?.FontSize?.Val);
        if (bold is null || fontSize?.Value is null) return null;
        if (!int.TryParse(fontSize.Value, out int sizeHalfPts)) return null;
        if (sizeHalfPts < 26) return null;
        int visualDepth = sizeHalfPts >= 36 ? 1 : (sizeHalfPts >= 30 ? 2 : 3);
        return new HeadingClassification(visualDepth, HeadingSignal.Visual);
    }

    /// <summary>
    /// Strip the <c>uk:headingDepth</c> / <c>uk:headingSignal</c>
    /// attributes leg's Builder subclass emits. Called after the leg
    /// pipeline has consumed them (via SemanticEnricher) so they don't
    /// leak into the final AKN.
    /// </summary>
    internal static void StripHeadingMetadataAttributes(XmlDocument akn) {
        StripHeadingAttrsFrom(akn.DocumentElement);
    }

    private static void StripHeadingAttrsFrom(XmlElement el) {
        if (el is null) return;
        var toRemove = new List<XmlAttribute>();
        foreach (XmlAttribute a in el.Attributes) {
            if (a.LocalName == "headingDepth" || a.LocalName == "headingSignal")
                toRemove.Add(a);
        }
        foreach (var a in toRemove)
            el.RemoveAttributeNode(a);
        foreach (XmlNode child in el.ChildNodes) {
            if (child is XmlElement ce)
                StripHeadingAttrsFrom(ce);
        }
    }

}

}
