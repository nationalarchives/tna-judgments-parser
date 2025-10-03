
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

enum CellBorderStyle { None, Solid, Dotted, Dashed, Double }

interface IBordered {

    float? BorderTopWidthPt { get; }
    CellBorderStyle? BorderTopStyle { get; }
    string BorderTopColor { get; }

    float? BorderRightWidthPt { get; }
    CellBorderStyle? BorderRightStyle { get; }
    string BorderRightColor { get; }

    float? BorderBottomWidthPt { get; }
    CellBorderStyle? BorderBottomStyle { get; }
    string BorderBottomColor { get; }

    float? BorderLeftWidthPt { get; }
    CellBorderStyle? BorderLeftStyle { get; }
    string BorderLeftColor { get; }

}

interface ITable : IBlock {

    string Style { get; }

    IEnumerable<IRow> Rows { get; }

    List<float> ColumnWidthsIns { get; }

}

interface IRow {

    bool IsHeader { get; }

    bool IsImplicitHeader { get; }
    IEnumerable<ICell> Cells { get; }

}

enum VerticalMerge { Start, Continuation }

enum VerticalAlignment { Top, Middle, Bottom }

interface ICell : IBordered {

    IEnumerable<IBlock> Contents { get; set; }

    int? ColSpan { get; }

    VerticalMerge? VMerge { get; }

    VerticalAlignment? VAlignment { get; }

    string BackgroundColor { get; }

    Dictionary<string, string> GetCSSStyles() {
        return CSS.GetCSSStyles(this);
    }

}

}
