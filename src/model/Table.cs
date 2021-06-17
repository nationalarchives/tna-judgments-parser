
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {


interface ITable : IBlock {

    IEnumerable<IRow> Rows();

}

interface IRow {

    IEnumerable<ICell> Cells();

}

interface ICell {

    IEnumerable<IBlock> Contents();
}

}
