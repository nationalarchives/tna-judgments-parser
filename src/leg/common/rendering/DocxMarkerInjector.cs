using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Common.Rendering {

internal static class DocxMarkerInjector {

    internal const string MarkerPrefix = "LEGRENDERMARK";
    internal const string MarkerSuffix = "ENDMARK";

    internal static string Format(int index) => $"{MarkerPrefix}{index:D5}{MarkerSuffix}";

    // Injects a text marker before every drawing the parser will count via NextDrawingIndex.
    // The parser's counted-set must match this walk or the index→image mapping drifts:
    //  - Drawings inside AlternateContentChoice are SKIPPED (parser's AlternateContent2.Map
    //    never dispatches MapRunChild on Choice children).
    //  - Drawings inside AlternateContentFallback of Requires ∈ {wps,wpg,wpi,wpc,cx1} ARE
    //    counted (parser's MapFallback calls MapRunChild, which fires NextDrawingIndex).
    //  - Drawings in AlternateContent of Requires ∈ {v,w16se} fallbacks are a known
    //    edge case — parser uses WImageRef.Make directly without NextDrawingIndex. No
    //    fixtures in the current corpus trigger this path; documented as tech debt.
    // When a drawing has no ancestor Run we still reserve its ordinal (incrementing the
    // counter) but cannot inject a marker; the extractor's gap-fill heuristic handles it.
    internal static byte[] InjectDrawingMarkers(byte[] docx, out int drawingCount) {
        using var ms = new MemoryStream();
        ms.Write(docx, 0, docx.Length);
        ms.Position = 0;

        int count = 0;
        using (var word = WordprocessingDocument.Open(ms, true)) {
            foreach (var part in EnumerateParts(word)) {
                var body = part.RootElement;
                if (body == null) continue;
                foreach (var drawing in body.Descendants<Drawing>().ToList()) {
                    if (drawing.Ancestors<AlternateContentChoice>().Any())
                        continue;

                    var containingRun = drawing.Ancestors<Run>().FirstOrDefault();
                    if (containingRun != null) {
                        var marker = new Run(
                            new Text(Format(count)) { Space = SpaceProcessingModeValues.Preserve });
                        containingRun.InsertBeforeSelf(marker);
                    }
                    count++;
                }
            }
        }

        drawingCount = count;
        return ms.ToArray();
    }

    private static IEnumerable<OpenXmlPart> EnumerateParts(WordprocessingDocument word) {
        if (word.MainDocumentPart != null) yield return word.MainDocumentPart;
        if (word.MainDocumentPart?.HeaderParts != null)
            foreach (var p in word.MainDocumentPart.HeaderParts) yield return p;
        if (word.MainDocumentPart?.FooterParts != null)
            foreach (var p in word.MainDocumentPart.FooterParts) yield return p;
        if (word.MainDocumentPart?.FootnotesPart != null) yield return word.MainDocumentPart.FootnotesPart;
        if (word.MainDocumentPart?.EndnotesPart != null) yield return word.MainDocumentPart.EndnotesPart;
    }

}

}
