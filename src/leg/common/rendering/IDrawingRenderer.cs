using System.Threading;

namespace UK.Gov.Legislation.Common.Rendering {

public interface IDrawingRenderer {
    byte[] TryRenderDrawing(byte[] docx, int drawingIndex, CancellationToken ct);
}

public sealed class NullRenderer : IDrawingRenderer {
    public byte[] TryRenderDrawing(byte[] docx, int drawingIndex, CancellationToken ct) => null;
}

}
