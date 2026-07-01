using System.IO;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Common.Rendering {

/// <summary>
/// Builds a minimal docx containing a single drawing so it can be rendered in
/// isolation, one drawing in, one image out. This sidesteps the whole-document
/// marker/index mapping (which can't reliably associate a rendered image with a
/// drawing like SmartArt); with only one drawing there is nothing to disambiguate.
/// </summary>
internal static class IsolatedDrawingDocx {

    /// <summary>
    /// Returns a docx containing only <paramref name="drawing"/> (plus page setup),
    /// keeping every part and relationship of the source so the drawing's r:ids still
    /// resolve. Returns null if the source has no body to rewrite.
    /// </summary>
    public static byte[] Build(byte[] sourceDocx, Drawing drawing) {
        if (sourceDocx == null || sourceDocx.Length == 0 || drawing == null)
            return null;

        var ms = new MemoryStream();
        ms.Write(sourceDocx, 0, sourceDocx.Length);
        ms.Position = 0;
        using (var doc = WordprocessingDocument.Open(ms, true)) {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
                return null;

            // Keep page setup, but drop header/footer references so their content isn't
            // rendered into extra images alongside the one drawing we care about.
            var sectPr = body.Elements<SectionProperties>().LastOrDefault()?.CloneNode(true) as SectionProperties;
            sectPr?.RemoveAllChildren<HeaderReference>();
            sectPr?.RemoveAllChildren<FooterReference>();

            body.RemoveAllChildren();
            body.AppendChild(new Paragraph(new Run((Drawing) drawing.CloneNode(true))));
            if (sectPr != null)
                body.AppendChild(sectPr);
            doc.MainDocumentPart.Document.Save();
        }
        return ms.ToArray();
    }
}

}
