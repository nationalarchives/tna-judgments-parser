
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WTable : ITable {

    private readonly MainDocumentPart main;

    public WTable(MainDocumentPart main, Table table) {
        this.main = main;
        this.TypedRows = table.ChildElements.Where(e => e is TableRow).Cast<TableRow>().Select(r => new WRow(main, r));
    }
    public WTable(MainDocumentPart main, IEnumerable<WRow> rows) {
        this.main = main;
        this.TypedRows = rows;
    }

    public MainDocumentPart Main { get => main; }

    public IEnumerable<IRow> Rows { get => TypedRows; }

    public IEnumerable<WRow> TypedRows { get; private init; }

    // public IEnumerable<WRow> Rows2() {
    //     return (IEnumerable<WRow>) Rows;
    // }

    // public IEnumerable<WRow> TypedRows { get => (IEnumerable<WRow>) Rows; }

    // public T Jim<T>() where T : struct {
    //     return null;
    // }

}

class WRow : IRow {

    private readonly MainDocumentPart main;

    internal WRow(MainDocumentPart main, TableRow row) {
        this.main = main;
        this.TypedCells = row.ChildElements.Where(e => e is TableCell).Cast<TableCell>().Select(c => new WCell(main, c));
    }
    internal WRow(MainDocumentPart main, IEnumerable<WCell> cells) {
        this.main = main;
        this.TypedCells = cells;
    }

    public MainDocumentPart Main { get => main; }

    public IEnumerable<WCell> TypedCells { get; private init; }

    public IEnumerable<ICell> Cells { get => TypedCells; }

}

class WCell : ICell {

    private readonly MainDocumentPart main;

    internal WCell(MainDocumentPart main, TableCell cell) {
        this.main = main;
        this.Contents = Blocks.ParseBlocks(main, cell.ChildElements.Where(e => !(e is TableCellProperties)));
    }
    internal WCell(MainDocumentPart main, IEnumerable<IBlock> contents) {
        this.main = main;
        this.Contents = contents;
    }

    public MainDocumentPart Main { get => main; }

    public IEnumerable<IBlock> Contents { get; private init; }

}

}
