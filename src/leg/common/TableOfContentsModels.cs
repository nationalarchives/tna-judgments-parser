using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Minimal interface for TOC-specific content
/// </summary>
public interface ITocContent {
    string Text { get; }
}

/// <summary>
/// Minimal interface for TOC-specific lines
/// </summary>
public interface ITocLine {
    string Style { get; }
    IEnumerable<ITocContent> Contents { get; }
}

/// <summary>
/// Simple TOC line implementation that adapts to ILine interface
/// </summary>
internal class TocLine : ILine {
    private readonly string _text;
    private readonly List<IInline> _contents;

    public TocLine(string text) {
        _text = text ?? string.Empty;
        _contents = new List<IInline> { new TocText(_text) };
    }

    // Required ILine properties
    public string Style => TableOfContentsConstants.TocEntryStyle;
    public AlignmentValues? Alignment => null;
    public string LeftIndent => null;
    public string RightIndent => null;
    public string FirstLineIndent => null;
    public IEnumerable<IInline> Contents => _contents;

    // Border properties - implement required interface members
    public string BorderTop => null;
    public string BorderRight => null;
    public string BorderBottom => null;
    public string BorderLeft => null;
    public float? BorderTopWidthPt => null;
    public CellBorderStyle? BorderTopStyle => null;
    public string BorderTopColor => null;
    public float? BorderRightWidthPt => null;
    public CellBorderStyle? BorderRightStyle => null;
    public string BorderRightColor => null;
    public float? BorderBottomWidthPt => null;
    public CellBorderStyle? BorderBottomStyle => null;
    public string BorderBottomColor => null;
    public float? BorderLeftWidthPt => null;
    public CellBorderStyle? BorderLeftStyle => null;
    public string BorderLeftColor => null;
}

/// <summary>
/// Simple TOC text implementation that adapts to IFormattedText interface
/// </summary>
internal class TocText : IFormattedText, ITocContent {
    public string Text { get; }

    public TocText(string text) {
        Text = text ?? string.Empty;
    }

    // Required IFormattedText properties
    public string Style => null;
    public bool? Bold => null;
    public bool? Italic => null;
    public UnderlineValues2? Underline => null;
    public bool? SmallCaps => null;
    public StrikethroughValue? Strikethrough => null;
    public SuperSubValues? SuperSub => null;
    public string BackgroundColor => null;
    public string FontColor => null;
    public string FontName => null;
    public float? FontSizePt => null;
    public bool? Uppercase => null;
    public bool IsHidden => false;

    public Dictionary<string, string> GetCSSStyles(string defaultFontFamily) {
        return new Dictionary<string, string>();
    }
}

/// <summary>
/// Configuration options for table of contents generation
/// </summary>
public class TableOfContentsOptions {
    
    /// <summary>
    /// Maximum depth of sections to include in TOC
    /// </summary>
    public int MaxDepth { get; init; } = 10;
    
    /// <summary>
    /// Whether to include section numbers in TOC entries
    /// </summary>
    public bool IncludeSectionNumbers { get; init; } = true;
    
    /// <summary>
    /// Custom title for the document (overrides auto-detection)
    /// </summary>
    public string CustomTitle { get; init; }
    
    /// <summary>
    /// Whether to generate href links for TOC items
    /// </summary>
    public bool GenerateHrefLinks { get; init; } = true;
}

}
