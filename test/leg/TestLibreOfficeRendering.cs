using System;
using System.IO;
using System.Threading;
using Xunit;

using UK.Gov.Legislation.Common.Rendering;

namespace UK.Gov.Legislation.Test {

public class TestLibreOfficeRendering {

    private const string SofficePath = @"C:\Program Files\LibreOffice\program\soffice.exe";

    [Fact]
    public void LocalSubprocessRenderer_ProducesImageForKnownDrawing() {
        if (!File.Exists(SofficePath))
            Assert.Skip($"LibreOffice not installed at {SofficePath}");

        string projectRoot = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        string docxPath = Path.Combine(projectRoot, "test", "leg", "ia", "original filenames",
                                       "ukia_20250012_en.docx");
        byte[] docx = File.ReadAllBytes(docxPath);

        var renderer = new LocalSubprocessRenderer(SofficePath);

        // Ask for an image that LibreOffice reliably produces for this doc.
        byte[] bytes = null;
        for (int i = 0; i < 30; i++) {
            bytes = renderer.TryRenderDrawing(docx, i, CancellationToken.None);
            if (bytes != null && bytes.Length > 1000) break;
        }

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, $"image unexpectedly small: {bytes.Length} bytes");
        Assert.True(IsPng(bytes) || IsGif(bytes) || IsJpeg(bytes),
            $"unrecognised image magic: {bytes[0]:X2} {bytes[1]:X2} {bytes[2]:X2} {bytes[3]:X2}");
    }

    private static bool IsPng(byte[] b) =>
        b.Length >= 4 && b[0] == 0x89 && b[1] == (byte) 'P' && b[2] == (byte) 'N' && b[3] == (byte) 'G';
    private static bool IsGif(byte[] b) =>
        b.Length >= 3 && b[0] == (byte) 'G' && b[1] == (byte) 'I' && b[2] == (byte) 'F';
    private static bool IsJpeg(byte[] b) =>
        b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    [Fact]
    public void LocalSubprocessRenderer_ReturnsNullForOutOfRangeIndex() {
        if (!File.Exists(SofficePath))
            Assert.Skip($"LibreOffice not installed at {SofficePath}");

        string projectRoot = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", ".."));
        byte[] docx = File.ReadAllBytes(Path.Combine(
            projectRoot, "test", "leg", "ia", "original filenames", "ukia_20250012_en.docx"));

        var renderer = new LocalSubprocessRenderer(SofficePath);
        byte[] png = renderer.TryRenderDrawing(docx, drawingIndex: 9999, CancellationToken.None);

        Assert.Null(png);
    }

    [Fact]
    public void LocalSubprocessRenderer_ReturnsNullWhenSofficeMissing() {
        var renderer = new LocalSubprocessRenderer(@"C:\does\not\exist\soffice.exe");
        byte[] png = renderer.TryRenderDrawing(new byte[] { 1, 2, 3 }, 0, CancellationToken.None);
        Assert.Null(png);
    }

    [Fact]
    public void NullRenderer_AlwaysReturnsNull() {
        var renderer = new NullRenderer();
        Assert.Null(renderer.TryRenderDrawing(new byte[] { 1 }, 0, CancellationToken.None));
    }

}

}
