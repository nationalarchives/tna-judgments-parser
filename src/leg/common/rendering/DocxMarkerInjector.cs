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
                    var containingRun = drawing.Ancestors<Run>().FirstOrDefault();
                    if (containingRun == null) continue;
                    var marker = new Run(
                        new Text(Format(count)) { Space = SpaceProcessingModeValues.Preserve });
                    containingRun.InsertBeforeSelf(marker);
                    count++;
                }
            }
        }

        drawingCount = count;
        return ms.ToArray();
    }

    private static System.Collections.Generic.IEnumerable<OpenXmlPart> EnumerateParts(WordprocessingDocument word) {
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
