
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface ITable : IBlock {

    IEnumerable<IRow> Rows { get; }

}

interface IRow {

    IEnumerable<ICell> Cells { get; }

}

enum CellBorderStyle { None, Solid, Dotted, Dashed, Double }

interface ICell {

    IEnumerable<IBlock> Contents { get; }

   // string BorderTopColor { get; }
    CellBorderStyle BorderTopStyle { get; }
    float? BorderTopWidthPt { get; }
    // string BorderRightColor { get; }
    CellBorderStyle BorderRightStyle { get; }
    float? BorderRightWidthPt { get; }
    // string BorderBottomColor { get; }
    CellBorderStyle BorderBottomStyle { get; }
    float? BorderBottomWidthPt { get; }
    // string BorderLeftColor { get; }
    CellBorderStyle BorderLeftStyle { get; }
    float? BorderLeftWidthPt { get; }

    Dictionary<string, string> GetCSSStyles() {
        return CSS.GetCSSStyles(this);
    }

}

}
