using System.IO;
using System.Text.RegularExpressions;
using Xunit;

using UK.Gov.Legislation.Common.Rendering;
using IaHelper = UK.Gov.Legislation.ImpactAssessments.Helper;

namespace UK.Gov.Legislation.Test {

public class TestRenderedChartIntegration {

    private const string SofficePath = @"C:\Program Files\LibreOffice\program\soffice.exe";

    [Fact]
    public void ParseIaWithLocalRenderer_ProducesValidOutput() {
        if (!File.Exists(SofficePath))
            Assert.Skip($"LibreOffice not installed at {SofficePath}");

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
