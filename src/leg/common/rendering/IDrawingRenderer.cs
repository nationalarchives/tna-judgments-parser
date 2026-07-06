using System.Collections.Generic;
using System.Threading;

namespace UK.Gov.Legislation.Common.Rendering {

public interface IDrawingRenderer {
    // Converts a docx and returns raster bytes for every drawing it finds, in
    // document-walk order. Callers match rendered bytes to drawings by index.
    IReadOnlyList<byte[]> RenderAllDrawings(byte[] docx, CancellationToken ct);
}

public sealed class NullRenderer : IDrawingRenderer {
    public IReadOnlyList<byte[]> RenderAllDrawings(byte[] docx, CancellationToken ct)
        => System.Array.Empty<byte[]>();
}

}
