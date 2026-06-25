using Xunit;

using test;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

/// <summary>
/// Regression guard for the embedded EMF/WMF metafile crash (LEG-159).
///
/// sdsifia_9780111020593 carries a cropped vector WMF (image1.wmf) that previously
/// crashed the parse with SixLabors.ImageSharp.UnknownImageFormatException: our
/// extractor can't rasterise a vector metafile and ImageSharp can't decode it, so the
/// raw bytes reached the cropper. It must now parse to valid AKN, with a renderer the
/// metafile is rasterised; without one it degrades, never throwing.
///
/// This is an integration-branch test: it relies on the Scottish IA support (LEG-158)
/// to give the document a URI so it reaches image conversion. On main alone the
/// document early-returns before that point, so the crash path is never exercised.
/// </summary>
public class TestIaMetafileImage {

    [Fact]
    public void EmbeddedWmfMetafileParsesWithoutCrashing() {
        var docx = DocumentHelpers.ReadDocx("test.leg.ia.sdsifia_9780111020593_en.docx");
        var parsed = Helper.Parse(docx, "sdsifia_9780111020593_en.docx",
            renderer: UK.Gov.Legislation.Test.LocalRendererHelper.GetOrNull());
        DocumentHelpers.AssertValidMainAkn(parsed.Document);
    }

}

}
