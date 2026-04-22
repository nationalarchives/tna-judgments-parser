using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        IReadOnlyList<byte[]> all = renderer.RenderAllDrawings(docx, CancellationToken.None);

        byte[] bytes = all.FirstOrDefault(b => b != null && b.Length > 1000);
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
    public void LocalSubprocessRenderer_ReturnsEmptyWhenSofficeMissing() {
        var renderer = new LocalSubprocessRenderer(@"C:\does\not\exist\soffice.exe");
        var result = renderer.RenderAllDrawings(new byte[] { 1, 2, 3 }, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void NullRenderer_AlwaysReturnsEmpty() {
        var renderer = new NullRenderer();
        var result = renderer.RenderAllDrawings(new byte[] { 1 }, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Session_ReturnsNullForOutOfRangeIndex() {
        var fake = new FakeRenderer(new byte[][] { new byte[] { 1 }, new byte[] { 2 } });
        using var _ = RenderSession.Begin(fake, new byte[] { 0x50, 0x4B }, "fake.docx", allowUnrenderedCharts: true);
        var session = RenderSession.Current;

        Assert.NotNull(session.GetRenderedDrawing(0));
        Assert.NotNull(session.GetRenderedDrawing(1));
        Assert.Null(session.GetRenderedDrawing(2));
        Assert.Null(session.GetRenderedDrawing(-1));
        Assert.Null(session.GetRenderedDrawing(9999));
    }

    [Fact]
    public void Session_CachesRenderAcrossCalls() {
        var fake = new FakeRenderer(new byte[][] { new byte[] { 1 } });
        using var _ = RenderSession.Begin(fake, new byte[] { 0x50, 0x4B }, "fake.docx", allowUnrenderedCharts: true);
        var session = RenderSession.Current;

        for (int i = 0; i < 5; i++)
            Assert.NotNull(session.GetRenderedDrawing(0));
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public void Session_NoRendererReturnsNull() {
        using var _ = RenderSession.Begin(renderer: null, new byte[] { 0x50, 0x4B }, "doc.docx", allowUnrenderedCharts: true);
        Assert.Null(RenderSession.Current.GetRenderedDrawing(0));
    }

    private sealed class FakeRenderer : IDrawingRenderer {
        private readonly byte[][] images;
        public int CallCount { get; private set; }
        public FakeRenderer(byte[][] images) { this.images = images; }
        public IReadOnlyList<byte[]> RenderAllDrawings(byte[] docx, CancellationToken ct) {
            CallCount++;
            return images;
        }
    }

}

}
