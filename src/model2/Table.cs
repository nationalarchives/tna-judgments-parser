
using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WTable : ITable {

    internal MainDocumentPart Main { get; private init; }

    internal TableProperties Properties { get; private init; }
    internal TableGrid Grid { get; private init; }

    public IEnumerable<WRow> TypedRows { get; private init; }
    public IEnumerable<IRow> Rows { get => TypedRows; }

    public List<float> ColumnWidthsIns {
        get => DOCX.Tables.GetColumnWidthsIns(Grid);
    }

    public WTable(MainDocumentPart main, Table table) {
        Main = main;
        Properties = table.ChildElements.OfType<TableProperties>().FirstOrDefault();
        Grid = table.ChildElements.OfType<TableGrid>().FirstOrDefault();
        TypedRows = ParseTableContents(this, table.ChildElements);
    }

    public WTable(MainDocumentPart main, TableProperties props, TableGrid grid, IEnumerable<IRow> rows) {
        Main = main;
        Properties = props;
        Grid = grid;
        TypedRows = rows.Select(row => new WRow(this, row.Cells));
    }

    internal static IEnumerable<WRow> ParseTableContents(WTable table, IEnumerable<OpenXmlElement> elements) {
        return elements.Select(e => ParseTableChild(table, e)).Where(e => e is not null);
    }

    internal static WRow ParseTableChild(WTable table, OpenXmlElement e) {
        if (e is TableProperties)
            return null;
        if (e is TableGrid) // TODO
            return null;
        if (e is TableRow row)
            return new WRow(table, row);
        if (e is BookmarkStart || e is BookmarkEnd)
            return null;
        throw new Exception();
    }

}

class WRow : IRow {

    internal MainDocumentPart Main { get; private init; }

    internal WTable Table { get; private init; }

    public IEnumerable<WCell> TypedCells { get; private init; }
    public IEnumerable<ICell> Cells { get => TypedCells; }

    internal WRow(WTable table, TableRow row) {
        Main = table.Main;
        Table = table;
        TypedCells = ParseRowContents(this, row.ChildElements);
    }
    internal WRow(WTable table, IEnumerable<ICell> cells) {
        Main = table.Main;
        Table = table;
        TypedCells = cells.Select(c => new WCell(this, c.Contents));

    }

    internal static IEnumerable<WCell> ParseRowContents(WRow row, IEnumerable<OpenXmlElement> elements) {
        return elements.Select(e => ParseRowChild(row, e)).Where(e => e is not null);
    }

    internal static WCell ParseRowChild(WRow row, OpenXmlElement e) {
        if (e is TableRowProperties)
            return null;
        if (e is TablePropertyExceptions)   // [2021] EWHC 3360 (Ch)
            return null;
        if (e is TableCell cell)
            return new WCell(row, cell);
        if (e is SdtCell sdt)
            return ParseStdCell(row, sdt);
        if (e is BookmarkStart || e is BookmarkEnd)
            return null;
        throw new Exception();
    }

    internal static WCell ParseStdCell(WRow row, SdtCell sdt) {
        var content = sdt.SdtContentCell;
        OpenXmlElementList children = content.ChildElements;
        if (children.Count != 1)
            throw new Exception();
        OpenXmlElement child = children.First();
        if (child is TableCell cell)
            return new WCell(row, cell);
        if (child is SdtCell std2)
            return ParseStdCell(row, std2);
        throw new Exception();
    }

}

class WCell : ICell {

    internal MainDocumentPart Main { get; private init; }

    internal WTable Table { get => Row.Table; }

    internal WRow Row { get; private init; }

    public IEnumerable<IBlock> Contents { get; private init; }

    internal WCell(WRow row, TableCell cell) {
        Main = row.Main;
        Row = row;
        Contents = ParseCellContents(this, cell.ChildElements);
    }
    internal WCell(WRow row, IEnumerable<IBlock> contents) {
        Main = row.Main;
        Row = row;
        Contents = contents;
    }

    public float? BorderTopWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(Table.Properties?.TableBorders?.InsideHorizontalBorder);
    }
    public CellBorderStyle BorderTopStyle {
        get => DOCX.Tables.ExtractBorderStyle(Table.Properties?.TableBorders?.InsideHorizontalBorder);
    }
    public string BorderTopColor {
        get => DOCX.Tables.ExtractBorderColor(Table.Properties?.TableBorders?.InsideHorizontalBorder);
    }

    public float? BorderRightWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(Table.Properties?.TableBorders?.InsideVerticalBorder);
    }
    public CellBorderStyle BorderRightStyle {
        get => DOCX.Tables.ExtractBorderStyle(Table.Properties?.TableBorders?.InsideVerticalBorder);
    }
    public string BorderRightColor {
        get => DOCX.Tables.ExtractBorderColor(Table.Properties?.TableBorders?.InsideVerticalBorder);
    }

    public float? BorderBottomWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(Table.Properties?.TableBorders?.InsideHorizontalBorder);
    }
    public CellBorderStyle BorderBottomStyle {
        get => DOCX.Tables.ExtractBorderStyle(Table.Properties?.TableBorders?.InsideHorizontalBorder);
    }
    public string BorderBottomColor {
        get => DOCX.Tables.ExtractBorderColor(Table.Properties?.TableBorders?.InsideHorizontalBorder);
    }

    public float? BorderLeftWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(Table.Properties?.TableBorders?.InsideVerticalBorder);
    }
    public CellBorderStyle BorderLeftStyle {
        get => DOCX.Tables.ExtractBorderStyle(Table.Properties?.TableBorders?.InsideVerticalBorder);
    }
    public string BorderLeftColor {
        get => DOCX.Tables.ExtractBorderColor(Table.Properties?.TableBorders?.InsideVerticalBorder);
    }

    internal static IEnumerable<IBlock> ParseCellContents(WCell cell, IEnumerable<OpenXmlElement> elements) {
        return elements.SelectMany(e => ParseCellChild(cell, e));
    }

    internal static IEnumerable<IBlock> ParseCellChild(WCell cell, OpenXmlElement e) {
        if (e is TableCellProperties)
            return Enumerable.Empty<IBlock>();
        if (e is BookmarkStart || e is BookmarkEnd)
            return Enumerable.Empty<IBlock>();
        if (e is SdtBlock sdt)
            return Blocks.ParseStdBlock(cell.Main, sdt);
        return new List<IBlock>(1) { Blocks.ParseBlock(cell.Main, e) };
    }

}

}
