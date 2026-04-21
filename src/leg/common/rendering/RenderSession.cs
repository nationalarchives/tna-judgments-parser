using System;
using System.Collections.Generic;
using System.Threading;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common.Rendering {

public sealed class RenderSession {

    private static readonly AsyncLocal<RenderSession> _current = new();

    public static RenderSession Current => _current.Value;

    public IDrawingRenderer Renderer { get; }
    public byte[] DocxBytes { get; }
    public string DocumentName { get; }
    public bool AllowUnrenderedCharts { get; }

    private int drawingCounter;
    public int NextDrawingIndex() => Interlocked.Increment(ref drawingCounter) - 1;

    private readonly List<IImage> renderedImages = new();
    internal void AddRenderedImage(IImage image) {
        lock (renderedImages) { renderedImages.Add(image); }
    }
    public IEnumerable<IImage> RenderedImages {
        get {
            lock (renderedImages) { return renderedImages.ToArray(); }
        }
    }

    private RenderSession(
        IDrawingRenderer renderer, byte[] docx, string documentName, bool allowUnrenderedCharts) {
        Renderer = renderer;
        DocxBytes = docx;
        DocumentName = documentName;
        AllowUnrenderedCharts = allowUnrenderedCharts;
    }

    public static IDisposable Begin(
        IDrawingRenderer renderer, byte[] docx, string documentName, bool allowUnrenderedCharts) {
        var prev = _current.Value;
        _current.Value = new RenderSession(
            renderer ?? new NullRenderer(), docx, documentName, allowUnrenderedCharts);
        return new Scope(prev);
    }

    private sealed class Scope : IDisposable {
        private readonly RenderSession prev;
        public Scope(RenderSession prev) { this.prev = prev; }
        public void Dispose() => _current.Value = prev;
    }

}

}
