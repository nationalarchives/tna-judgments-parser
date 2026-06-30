using System;
using System.Collections.Generic;
using System.Threading;

namespace UK.Gov.Legislation.Common.Rendering {

// Thin IDrawingRenderer adapter for parser-side use on dev PCs. Delegates to
// DocxToImageRenderer, which is shared with the EC2-hosted RenderService so
// dev fixtures and remote rendering run the exact same algorithm.
public sealed class LocalSubprocessRenderer : IDrawingRenderer {

    private readonly DocxToImageRenderer inner;

    public LocalSubprocessRenderer(
        string sofficePath, TimeSpan? conversionTimeout = null, int? maxConcurrency = null) {
        this.inner = new DocxToImageRenderer(sofficePath, conversionTimeout, maxConcurrency);
    }

    public IReadOnlyList<byte[]> RenderAllDrawings(byte[] docx, CancellationToken ct)
        => inner.RenderAllDrawings(docx, ct);

    public byte[] RenderImage(byte[] image, string sourceExtension, CancellationToken ct)
        => inner.RenderImage(image, sourceExtension, ct);

}

}
