using System.Threading;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

/// <summary>
/// Optional hook implemented by callers that want to provide rendered
/// content for OOXML drawings the parser can't extract itself (charts,
/// SmartArt, diagrams). Shared parsing code consults
/// <see cref="DrawingResolver.Current"/>; if nothing has registered a
/// resolver, the parser falls back to its default constructor-based
/// behaviour.
/// </summary>
internal interface IDrawingResolver {

    /// <summary>
    /// Increments and returns the resolver's drawing counter. Returns
    /// a stable per-document index that <see cref="TryResolveUnrenderedDrawing"/>
    /// can use as a lookup key into a pre-rendered image set.
    /// </summary>
    int NextDrawingIndex();

    /// <summary>
    /// Attempts to resolve a drawing that couldn't be parsed as a normal
    /// embedded image. Implementations typically look up pre-rendered
    /// bytes by <paramref name="drawingIndex"/> and emit a reference to
    /// the rendered asset, or fall back to a placeholder.
    /// </summary>
    /// <returns>Inline to substitute, or <c>null</c> if the resolver
    /// declines to handle the drawing (caller falls back).</returns>
    IInline TryResolveUnrenderedDrawing(
        Drawing draw,
        RunProperties rProps,
        int drawingIndex);

    /// <summary>
    /// Returns a reference to the pre-rendered image for the given drawing index when
    /// one is available, or <c>null</c> when nothing was rendered (no placeholder, no
    /// throw). Lets callers prefer a rendered rasterisation for embedded images the
    /// parser can't decode itself (e.g. vector EMF/WMF metafiles) while falling back to
    /// direct extraction when no render exists.
    /// </summary>
    IInline TryGetRenderedDrawing(int drawingIndex);

}

/// <summary>
/// AsyncLocal accessor for the current <see cref="IDrawingResolver"/>.
/// Pipelines that want to provide custom drawing resolution set this
/// at the top of their parse; shared parser code reads it.
/// </summary>
internal static class DrawingResolver {

    private static readonly AsyncLocal<IDrawingResolver> _current = new();

    internal static IDrawingResolver Current {
        get => _current.Value;
        set => _current.Value = value;
    }

}

}
