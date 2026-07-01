using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.Common.Rendering {

public sealed class RenderSession : IDrawingResolver {

    private static readonly AsyncLocal<RenderSession> _current = new();
    private static readonly ILogger logger = Logging.Factory.CreateLogger<RenderSession>();

    public static RenderSession Current => _current.Value;

    public IDrawingRenderer Renderer { get; }
    public byte[] DocxBytes { get; }
    public string DocumentName { get; }
    public bool AllowUnrenderedCharts { get; }

    // Per-session lazy. First access triggers a single docx → all-images conversion;
    // subsequent accesses share the result. Scoped to this parse so nothing outlives
    // the Session.Dispose — no cached CT, no cached docx bytes, no cached failures.
    private readonly Lazy<IReadOnlyList<byte[]>> renderedDrawings;

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
        renderedDrawings = new Lazy<IReadOnlyList<byte[]>>(
            () => Renderer.RenderAllDrawings(DocxBytes, CancellationToken.None)
                  ?? Array.Empty<byte[]>(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public byte[] GetRenderedDrawing(int drawingIndex) {
        if (drawingIndex < 0 || DocxBytes == null || Renderer is NullRenderer) return null;
        var images = renderedDrawings.Value;
        if (drawingIndex >= images.Count) return null;
        return images[drawingIndex];
    }

    /// <summary>
    /// <see cref="IDrawingResolver"/> implementation. If the drawing was
    /// pre-rendered, emit a reference to the rendered image. Otherwise
    /// either throw (strict mode) or emit a placeholder describing the
    /// drawing — leg pipelines prefer never failing a whole batch over
    /// one unrenderable chart.
    /// </summary>
    IInline IDrawingResolver.TryResolveUnrenderedDrawing(
            DocumentFormat.OpenXml.Wordprocessing.Drawing draw,
            RunProperties rProps,
            int drawingIndex) {
        IInline rendered = TryGetRenderedRef(drawingIndex);
        if (rendered is not null)
            return rendered;
        IInline isolated = TryIsolatedRenderRef(draw, drawingIndex);
        if (isolated is not null)
            return isolated;
        if (!AllowUnrenderedCharts) {
            var (graphicType, caption) = DescribeDrawing(draw);
            throw new UnrenderableDrawingException(
                DocumentName, drawingIndex, graphicType, caption,
                "renderer unavailable or returned no image");
        }
        return MakeDrawingPlaceholder(draw, rProps);
    }

    IInline IDrawingResolver.TryGetRenderedDrawing(int drawingIndex) => TryGetRenderedRef(drawingIndex);

    /// <summary>
    /// Emit a reference to the pre-rendered image for this drawing index, adding it to
    /// the session's rendered images, or null when nothing was rendered for it.
    /// </summary>
    private IInline TryGetRenderedRef(int drawingIndex) {
        if (drawingIndex < 0 || DocxBytes == null)
            return null;
        byte[] bytes = GetRenderedDrawing(drawingIndex);
        if (bytes == null || bytes.Length == 0)
            return null;
        var (ext, mime) = ImageFormat.Detect(bytes);
        string name = $"rendered_drawing_{drawingIndex:D3}.{ext}";
        AddRenderedImage(new WRenderedImage(name, mime, bytes));
        return new WRenderedImageRef(name);
    }

    /// <summary>
    /// Fallback when the whole-document render didn't yield an image for this drawing
    /// (e.g. SmartArt, which the marker/index mapping can miss): render the drawing on
    /// its own docx, one drawing, one image, no mapping to get wrong. Null if there is
    /// no renderer or it produces nothing.
    /// </summary>
    private IInline TryIsolatedRenderRef(DocumentFormat.OpenXml.Wordprocessing.Drawing draw, int drawingIndex) {
        if (DocxBytes == null || Renderer is NullRenderer)
            return null;
        byte[] image;
        try {
            byte[] isolatedDocx = IsolatedDrawingDocx.Build(DocxBytes, draw);
            if (isolatedDocx == null)
                return null;
            image = Renderer.RenderAllDrawings(isolatedDocx, CancellationToken.None)?
                .FirstOrDefault(b => b != null && b.Length > 0);
        } catch (Exception e) {
            logger.LogWarning(e, "isolated drawing render failed");
            return null;
        }
        if (image == null || image.Length == 0)
            return null;
        var (ext, mime) = ImageFormat.Detect(image);
        string name = $"rendered_drawing_{drawingIndex:D3}.{ext}";
        AddRenderedImage(new WRenderedImage(name, mime, image));
        logger.LogInformation("recovered drawing {Index} via isolated render", drawingIndex);
        return new WRenderedImageRef(name);
    }

    private static (string graphicType, string caption) DescribeDrawing(DocumentFormat.OpenXml.Wordprocessing.Drawing draw) {
        var graphicData = draw.Descendants<DocumentFormat.OpenXml.Drawing.GraphicData>().FirstOrDefault();
        string graphicType = graphicData?.Uri?.Value ?? "unknown";
        var props = draw.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().FirstOrDefault();
        string caption = props?.Description?.Value?.Trim()
                       ?? props?.Name?.Value?.Trim()
                       ?? "";
        return (graphicType, caption);
    }

    private static IInline MakeDrawingPlaceholder(DocumentFormat.OpenXml.Wordprocessing.Drawing draw, RunProperties rProps) {
        var props = draw.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().FirstOrDefault();
        string name = props?.Name?.Value?.Trim() ?? "";
        string descr = props?.Description?.Value?.Trim() ?? "";

        var graphicData = draw.Descendants<DocumentFormat.OpenXml.Drawing.GraphicData>().FirstOrDefault();
        string graphicUri = graphicData?.Uri?.Value ?? "";
        string kind = graphicUri switch {
            string u when u.Contains("chart") => "Chart",
            string u when u.Contains("diagram") => "Diagram",
            string u when u.Contains("smartArt") => "SmartArt",
            string u when u.Contains("wordprocessingShape") => "Shape",
            _ => "Visual"
        };

        string caption = !string.IsNullOrEmpty(descr) ? descr
            : !string.IsNullOrEmpty(name) ? name
            : kind;
        return new UK.Gov.Legislation.Judgments.Parse.WText($"[{kind}: {caption}]", rProps);
    }

    public static IDisposable Begin(
        IDrawingRenderer renderer, byte[] docx, string documentName, bool allowUnrenderedCharts) {
        var prev = _current.Value;
        _current.Value = new RenderSession(
            renderer ?? new NullRenderer(), docx, documentName, allowUnrenderedCharts);
        DrawingResolver.Current = _current.Value;
        return new Scope(prev);
    }

    private sealed class Scope : IDisposable {
        private readonly RenderSession prev;
        public Scope(RenderSession prev) { this.prev = prev; }
        public void Dispose() {
            _current.Value = prev;
            DrawingResolver.Current = prev;
        }
    }

}

}
