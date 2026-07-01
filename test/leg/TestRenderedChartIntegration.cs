using System.IO;
using System.Text.RegularExpressions;
using Xunit;

using UK.Gov.Legislation.Common.Rendering;
using IaHelper = UK.Gov.Legislation.ImpactAssessments.Helper;

namespace UK.Gov.Legislation.Test {

public class TestRenderedChartIntegration {

    // Opt-in: set LEG_SOFFICE_PATH to a real LibreOffice to exercise rendering.
    private static readonly string SofficePath = System.Environment.GetEnvironmentVariable("LEG_SOFFICE_PATH");

    [Fact]
    public void ParseIaWithLocalRenderer_ProducesValidOutput() {
        if (string.IsNullOrEmpty(SofficePath) || !File.Exists(SofficePath))
            Assert.Skip("LibreOffice not available; set LEG_SOFFICE_PATH to run this test.");

        string projectRoot = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        string docxPath = Path.Combine(projectRoot, "test", "leg", "ia", "original filenames",
                                       "ukia_20250012_en.docx");
        byte[] docx = File.ReadAllBytes(docxPath);

        var renderer = new LocalSubprocessRenderer(SofficePath);

        var result = IaHelper.Parse(
            docx, "ukia_20250012_en.docx",
            simplify: true,
            allowUnrenderedCharts: true,
            renderer: renderer);

        string akn = result.Serialize();
        Assert.Contains("<img ", akn);
    }

    [Fact]
    public void ParseIaWithSmartArt_RecoversViaIsolatedRender() {
        if (string.IsNullOrEmpty(SofficePath) || !File.Exists(SofficePath))
            Assert.Skip("LibreOffice not available; set LEG_SOFFICE_PATH to run this test.");

        string projectRoot = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        byte[] docx = File.ReadAllBytes(Path.Combine(
            projectRoot, "test", "leg", "ia", "ukia_20140346_en.docx"));

        var renderer = new LocalSubprocessRenderer(SofficePath);

        // The SmartArt diagram has no blip and the whole-document marker render can't map
        // it; strict mode throws unless the isolated-render fallback recovers it. Reaching
        // an image (not a placeholder) proves the recovery.
        var result = IaHelper.Parse(
            docx, "ukia_20140346_en.docx",
            simplify: true,
            allowUnrenderedCharts: false,
            renderer: renderer);

        string akn = result.Serialize();
        Assert.Contains("<img ", akn);
        Assert.DoesNotContain("[Diagram", akn);
    }

    [Fact]
    public void ParseIaWithoutRenderer_KeepsTextPlaceholderInLenientMode() {
        string projectRoot = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        byte[] docx = File.ReadAllBytes(Path.Combine(
            projectRoot, "test", "leg", "ia", "original filenames", "ukia_20250012_en.docx"));

        var result = IaHelper.Parse(
            docx, "ukia_20250012_en.docx",
            simplify: true,
            allowUnrenderedCharts: true,
            renderer: null);

        string akn = result.Serialize();
        Assert.Contains("[Chart:", akn);
    }

    [Fact]
    public void ParseIaWithoutRenderer_StrictModeThrows() {
        string projectRoot = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        byte[] docx = File.ReadAllBytes(Path.Combine(
            projectRoot, "test", "leg", "ia", "original filenames", "ukia_20250012_en.docx"));

        var ex = Assert.Throws<UnrenderableDrawingException>(() =>
            IaHelper.Parse(
                docx, "ukia_20250012_en.docx",
                simplify: true,
                allowUnrenderedCharts: false,
                renderer: null));

        Assert.Equal("ukia_20250012_en.docx", ex.DocumentName);
        Assert.True(ex.DrawingIndex >= 0);
    }

}

}
