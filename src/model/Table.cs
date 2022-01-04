
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface ITable : IBlock {

    IEnumerable<IRow> Rows { get; }

    List<float> ColumnWidthsIns { get; }

}

interface IRow {

    IEnumerable<ICell> Cells { get; }

}

enum CellBorderStyle { None, Solid, Dotted, Dashed, Double }

interface ICell {

    IEnumerable<IBlock> Contents { get; }

    float? BorderTopWidthPt { get; }
    CellBorderStyle BorderTopStyle { get; }
    string BorderTopColor { get; }

    float? BorderRightWidthPt { get; }
    CellBorderStyle BorderRightStyle { get; }
    string BorderRightColor { get; }

    float? BorderBottomWidthPt { get; }
    CellBorderStyle BorderBottomStyle { get; }
    string BorderBottomColor { get; }

    float? BorderLeftWidthPt { get; }
    CellBorderStyle BorderLeftStyle { get; }
    string BorderLeftColor { get; }

    Dictionary<string, string> GetCSSStyles() {
        return CSS.GetCSSStyles(this);
    }

}

}
