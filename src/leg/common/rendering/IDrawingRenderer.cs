using System.Collections.Generic;
using System.Threading;

namespace UK.Gov.Legislation.Common.Rendering {

public interface IDrawingRenderer {
    // Converts a docx and returns raster bytes for every drawing it finds, in
    // document-walk order. Callers match rendered bytes to drawings by index.
    IReadOnlyList<byte[]> RenderAllDrawings(byte[] docx, CancellationToken ct);

    // Optional capability: rasterise a single embedded image (e.g. a metafile the
    // in-process converters can't read) to PNG bytes. Returns null when the renderer
    // can't or won't do it, callers fall back accordingly.
    byte[] RenderImage(byte[] image, string sourceExtension, CancellationToken ct) => null;
}

public sealed class NullRenderer : IDrawingRenderer {
    public IReadOnlyList<byte[]> RenderAllDrawings(byte[] docx, CancellationToken ct)
        => System.Array.Empty<byte[]>();
}

}
