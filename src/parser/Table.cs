
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WTable : ITable {

    private readonly MainDocumentPart main;
    private readonly Table table;

    public WTable(MainDocumentPart main, Table table) {
        this.main = main;
        this.table = table;
    }

    public IEnumerable<IRow> Rows() {
        return table.ChildElements.Where(e => e is TableRow).Cast<TableRow>().Select(r => new WRow(main, r));
    }

}

class WRow : IRow {

    private readonly MainDocumentPart main;
    private readonly TableRow row;

    internal WRow(MainDocumentPart main, TableRow row) {
        this.main = main;
        this.row = row;
    }

    public IEnumerable<ICell> Cells() {
        return row.ChildElements.Where(e => e is TableCell).Cast<TableCell>().Select(c => new WCell(main, c));
    }

}

class WCell : ICell {

    private readonly MainDocumentPart main;
    private readonly TableCell cell;

    internal WCell(MainDocumentPart main, TableCell cell) {
        this.main = main;
        this.cell = cell;
    }

    public IEnumerable<IBlock> Contents() {
        return Blocks.ParseBlocks(main, cell.ChildElements.Where(e => !(e is TableCellProperties)));
    }

}

}
