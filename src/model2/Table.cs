
using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WTable : ITable {

    private readonly MainDocumentPart main;

    public WTable(MainDocumentPart main, Table table) {
        this.main = main;
        this.TypedRows = ParseTableContents(main, table.ChildElements); // table.ChildElements.Where(e => e is TableRow).Cast<TableRow>().Select(r => new WRow(main, r));
    }
    public WTable(MainDocumentPart main, IEnumerable<WRow> rows) {
        this.main = main;
        this.TypedRows = rows;
    }

    public MainDocumentPart Main { get => main; }

    public IEnumerable<IRow> Rows { get => TypedRows; }

    public IEnumerable<WRow> TypedRows { get; private init; }

    internal static IEnumerable<WRow> ParseTableContents(MainDocumentPart main, IEnumerable<OpenXmlElement> elements) {
        return elements.Select(e => ParseTableChild(main, e)).Where(e => e is not null);
    }

    internal static WRow ParseTableChild(MainDocumentPart main, OpenXmlElement e) {
        if (e is TableProperties)
            return null;
        if (e is TableGrid) // TODO
            return null;
        if (e is TableRow row)
            return new WRow(main, row);
        throw new Exception();
    }

}

class WRow : IRow {

    private readonly MainDocumentPart main;

    internal WRow(MainDocumentPart main, TableRow row) {
        this.main = main;
        this.TypedCells = ParseRowContents(main, row.ChildElements); // row.ChildElements.Where(e => e is TableCell).Cast<TableCell>().Select(c => new WCell(main, c));
    }
    internal WRow(MainDocumentPart main, IEnumerable<WCell> cells) {
        this.main = main;
        this.TypedCells = cells;
    }

    public MainDocumentPart Main { get => main; }

    public IEnumerable<WCell> TypedCells { get; private init; }

    public IEnumerable<ICell> Cells { get => TypedCells; }

    internal static IEnumerable<WCell> ParseRowContents(MainDocumentPart main, IEnumerable<OpenXmlElement> elements) {
        return elements.Select(e => ParseRowChild(main, e)).Where(e => e is not null);
    }

    internal static WCell ParseRowChild(MainDocumentPart main, OpenXmlElement e) {
        if (e is TableRowProperties)
            return null;
        if (e is TablePropertyExceptions)   // [2021] EWHC 3360 (Ch)
            return null;
        if (e is TableCell cell)
            return new WCell(main, cell);
        if (e is SdtCell sdt)
            return ParseStdCell(main, sdt);
        if (e is BookmarkStart || e is BookmarkEnd)
            return null;
        throw new Exception();
    }

    internal static WCell ParseStdCell(MainDocumentPart main, SdtCell sdt) {
        var content = sdt.SdtContentCell;
        OpenXmlElementList children = content.ChildElements;
        if (children.Count != 1)
            throw new Exception();
        OpenXmlElement child = children.First();
        if (child is TableCell cell)
            return new WCell(main, cell);
        if (child is SdtCell std2)
            return ParseStdCell(main, std2);
        throw new Exception();
    }

}

class WCell : ICell {

    private readonly MainDocumentPart main;

    internal WCell(MainDocumentPart main, TableCell cell) {
        this.main = main;
        this.Contents = ParseCellContents(main, cell.ChildElements);
    }
    internal WCell(MainDocumentPart main, IEnumerable<IBlock> contents) {
        this.main = main;
        this.Contents = contents;
    }

    public MainDocumentPart Main { get => main; }

    public IEnumerable<IBlock> Contents { get; private init; }

    internal static IEnumerable<IBlock> ParseCellContents(MainDocumentPart main, IEnumerable<OpenXmlElement> elements) {
        return elements.SelectMany(e => ParseCellChild(main, e));
    }

    internal static IEnumerable<IBlock> ParseCellChild(MainDocumentPart main, OpenXmlElement e) {
        if (e is TableCellProperties)
            return Enumerable.Empty<IBlock>();
        if (e is BookmarkStart || e is BookmarkEnd)
            return Enumerable.Empty<IBlock>();
        if (e is SdtBlock sdt)
            return Blocks.ParseStdBlock(main, sdt);
        return new List<IBlock>(1) { Blocks.ParseBlock(main, e) };
    }

}

}
