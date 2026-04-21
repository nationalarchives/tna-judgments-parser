using System;

namespace UK.Gov.Legislation.Common.Rendering {

public sealed class UnrenderableDrawingException : Exception {

    public string DocumentName { get; }
    public int DrawingIndex { get; }
    public string GraphicType { get; }
    public string Caption { get; }
    public string RenderError { get; }

    public UnrenderableDrawingException(
        string documentName, int drawingIndex, string graphicType,
        string caption, string renderError)
        : base(BuildMessage(documentName, drawingIndex, graphicType, caption, renderError)) {
        DocumentName = documentName;
        DrawingIndex = drawingIndex;
        GraphicType = graphicType;
        Caption = caption;
        RenderError = renderError;
    }

    private static string BuildMessage(
        string documentName, int drawingIndex, string graphicType,
        string caption, string renderError) {
        string captionPart = string.IsNullOrEmpty(caption) ? "" : $" \"{caption}\"";
        string docPart = string.IsNullOrEmpty(documentName) ? "" : $" in {documentName}";
        return $"Drawing {drawingIndex} ({graphicType}){captionPart}{docPart} could not be rendered: {renderError}. "
             + "Re-run with --allow-unrendered-charts to accept a text placeholder instead.";
    }

}

}
