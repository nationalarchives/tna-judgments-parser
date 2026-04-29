using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

using Office10Wps = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Lifts <c>&lt;w:txbxContent&gt;</c> paragraphs out of <c>&lt;mc:AlternateContent&gt;</c>
/// shape drawings and inserts them as siblings of the anchor paragraph, then removes
/// the consumed AlternateContent block.
///
/// Word stores values that visually overlay a label paragraph (e.g. an Impact
/// Assessment summary form's "Title:" / value pairs) inside a wps:wsp shape's
/// w:txbxContent. The general parser pipeline drops shape choices in favour of
/// VML fallbacks (which it then renders as images), so the text inside the box
/// is silently lost. For leg documents this loses real content authored by the
/// user. Promoting the box's paragraphs to siblings lets the rest of the
/// pipeline see them as ordinary content.
///
/// Only invoked from BaseHelper, so judgments/lawmaker behaviour is unchanged.
/// </summary>
internal static class TextBoxLifter {

    private static readonly ILogger Logger = Logging.Factory.CreateLogger("TextBoxLifter");

    public static void Lift(WordprocessingDocument docx) {
        var main = docx?.MainDocumentPart;
        if (main?.Document?.Body == null) return;

        // Snapshot: we mutate the tree as we go.
        var anchors = main.Document.Body.Descendants<AlternateContent>().ToList();

        int liftedParagraphs = 0;
        foreach (var alternate in anchors) {
            if (alternate.Parent == null) continue; // already removed by an outer lift

            var choice = alternate.Elements<AlternateContentChoice>().FirstOrDefault();
            if (choice?.Requires?.Value != "wps") continue;

            var wsp = choice.Descendants<Office10Wps.WordprocessingShape>().FirstOrDefault();
            if (wsp == null) continue;

            var txbxContent = wsp.Descendants<TextBoxContent>().FirstOrDefault();
            if (txbxContent == null) continue;

            // TextBoxContent may have w:sdt wrappers around w:p — collect from
            // any depth so SDT-wrapped content is included.
            var paragraphs = txbxContent.Descendants<Paragraph>().ToList();
            if (paragraphs.Count == 0) continue;

            var anchorParagraph = alternate.Ancestors<Paragraph>().FirstOrDefault();
            if (anchorParagraph?.Parent == null) continue;

            // Word's wp:anchor uses absolute positioning, so the anchor paragraph
            // is typically a positioning point that visually overlays a *later*
            // paragraph (e.g. the "Title:" cell in an IA summary form). Merge
            // the text-box's runs INTO that label paragraph so the result is a
            // single "Title: <value>" line that the SemanticEnricher already
            // knows how to split. If we can't find a label, fall back to
            // inserting the box's paragraphs as siblings of the anchor.
            var labelParagraph = FindLabelParagraphAfter(anchorParagraph);
            if (labelParagraph != null) {
                EnsureTrailingSpace(labelParagraph);
                foreach (var p in paragraphs) {
                    foreach (var run in p.Elements<Run>()) {
                        labelParagraph.AppendChild((Run) run.CloneNode(deep: true));
                    }
                }
                liftedParagraphs += paragraphs.Count;
            } else {
                OpenXmlElement insertAfter = anchorParagraph;
                foreach (var p in paragraphs) {
                    var clone = (Paragraph) p.CloneNode(deep: true);
                    anchorParagraph.Parent.InsertAfter(clone, insertAfter);
                    insertAfter = clone;
                    liftedParagraphs++;
                }
            }

            // Remove the consumed alternate content block. We leave the wrapping
            // run/paragraph in place; if it had no other content the anchor
            // paragraph is now empty, which the existing pipeline tolerates.
            alternate.Remove();
        }

        if (liftedParagraphs > 0)
            Logger.LogInformation("Lifted {Count} paragraph(s) from text boxes", liftedParagraphs);
    }

    private static Paragraph FindLabelParagraphAfter(Paragraph anchor) {
        for (var n = anchor.NextSibling(); n != null; n = n.NextSibling()) {
            if (n is not Paragraph p) continue;
            // Skip another anchor-only paragraph (empty save for shape drawings)
            // so a run of consecutive text-box anchors all attach to the next
            // real label paragraph after them.
            if (HasSubstantiveText(p)) return p;
        }
        return null;
    }

    private static void EnsureTrailingSpace(Paragraph p) {
        // If the last <w:t> in this paragraph already ends with whitespace, the
        // template has its own separator and we don't need another. Otherwise
        // append a single-space run so the merged value isn't run-on with the
        // label.
        var lastText = p.Descendants<Text>().LastOrDefault(t => !t.Ancestors<TextBoxContent>().Any());
        if (lastText != null && lastText.Text.Length > 0 && char.IsWhiteSpace(lastText.Text[lastText.Text.Length - 1]))
            return;
        var spaceRun = new Run(new Text(" ") { Space = SpaceProcessingModeValues.Preserve });
        p.AppendChild(spaceRun);
    }

    private static bool HasSubstantiveText(Paragraph p) {
        foreach (var t in p.Descendants<Text>()) {
            // Skip Text elements inside w:txbxContent — that's text-box content
            // we're trying to lift OUT, not the surrounding label.
            if (t.Ancestors<TextBoxContent>().Any()) continue;
            if (!string.IsNullOrWhiteSpace(t.Text)) return true;
        }
        return false;
    }
}

}
