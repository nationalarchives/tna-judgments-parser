
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface ITable : IBlock {

    IEnumerable<IRow> Rows { get; }

}

interface IRow {

    IEnumerable<ICell> Cells { get; }

}

interface ICell {

    IEnumerable<IBlock> Contents { get; }
}

}
