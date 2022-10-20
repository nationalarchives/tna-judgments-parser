
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
        Style = Properties?.TableStyle?.Val?.Value;
        TypedRows = ParseTableContents(this, table.ChildElements);
    }

    public WTable(MainDocumentPart main, TableProperties props, TableGrid grid, IEnumerable<WRow> rows) {
        Main = main;
        Properties = props;
        Grid = grid;
        Style = Properties?.TableStyle?.Val?.Value;
        TypedRows = rows.Select(row => new WRow(this, row.Properties, row.TypedCells));
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

    public string Style { get; init; }

}

class WRow : IRow {

    internal MainDocumentPart Main { get; private init; }

    internal WTable Table { get; private init; }

    internal TablePropertyExceptions Properties { get; private init; }

    public IEnumerable<WCell> TypedCells { get; private init; }
    public IEnumerable<ICell> Cells { get => TypedCells; }

    internal WRow(WTable table, TableRow row) {
        Main = table.Main;
        Table = table;
        Properties = row.TablePropertyExceptions;
        TypedCells = ParseRowContents(this, row.ChildElements);
    }
    internal WRow(WTable table, TablePropertyExceptions properties, IEnumerable<WCell> cells) {
        Main = table.Main;
        Table = table;
        Properties = properties;
        TypedCells = cells.Select(c => new WCell(this, c.Props, c.Contents));
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
        SdtContentCell content = sdt.SdtContentCell;
        IEnumerable<OpenXmlElement> children = content.ChildElements.Where(e => e is not BookmarkStart && e is not BookmarkEnd);    // [2022] EWHC 205 (QB)
        if (children.Count() != 1)
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

    internal TableCellProperties Props { get; init; }

    public IEnumerable<IBlock> Contents { get; private init; }

    internal WCell(WRow row, TableCell cell) {
        Main = row.Main;
        Row = row;
        Props = cell.TableCellProperties;
        Contents = ParseCellContents(this, cell.ChildElements);
    }
    internal WCell(WRow row, TableCellProperties props, IEnumerable<IBlock> contents) {
        Main = row.Main;
        Row = row;
        Props = props;
        Contents = contents;
    }

    public VerticalMerge? VMerge { get {
        var vMerge = Props?.VerticalMerge;
        if (vMerge is null)
            return null;
        if (vMerge.Val is null)
            return VerticalMerge.Continuation;
        if (vMerge.Val == MergedCellValues.Restart)
            return VerticalMerge.Start;
        if (vMerge.Val == MergedCellValues.Continue)
            return VerticalMerge.Continuation;
        throw new Exception();
    } }

    public int? ColSpan { get  => Props?.GridSpan?.Val?.Value; }

    private BorderType BorderTop {
        get  {
            BorderType border = Props?.TableCellBorders?.TopBorder;
            if (border is null)
                border = Row.Properties?.TableBorders?.InsideHorizontalBorder;
            if (border is null)
                border = Table.Properties?.TableBorders?.InsideHorizontalBorder;
            return border;
        }
    }
    public float? BorderTopWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(BorderTop);
    }
    public CellBorderStyle? BorderTopStyle {
        get => DOCX.Tables.ExtractBorderStyle(BorderTop);
    }
    public string BorderTopColor {
        get => DOCX.Tables.ExtractBorderColor(BorderTop);
    }

    private BorderType BorderRight {
        get  {
            BorderType border = Props?.TableCellBorders?.RightBorder;
            if (border is null)
                border = Row.Properties?.TableBorders?.InsideVerticalBorder;
            if (border is null)
                border = Table.Properties?.TableBorders?.InsideVerticalBorder;
            return border;
        }
    }
    public float? BorderRightWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(BorderRight);
    }
    public CellBorderStyle? BorderRightStyle {
        get => DOCX.Tables.ExtractBorderStyle(BorderRight);
    }
    public string BorderRightColor {
        get => DOCX.Tables.ExtractBorderColor(BorderRight);
    }

    private BorderType BorderBottom {
        get  {
            BorderType border = Props?.TableCellBorders?.BottomBorder;
            if (border is null)
                border = Row.Properties?.TableBorders?.InsideHorizontalBorder;
            if (border is null)
                border = Table.Properties?.TableBorders?.InsideHorizontalBorder;
            return border;
        }
    }
    public float? BorderBottomWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(BorderBottom);
    }
    public CellBorderStyle? BorderBottomStyle {
        get => DOCX.Tables.ExtractBorderStyle(BorderBottom);
    }
    public string BorderBottomColor {
        get => DOCX.Tables.ExtractBorderColor(BorderBottom);
    }

    private BorderType BorderLeft {
        get  {
            BorderType border = Props?.TableCellBorders?.LeftBorder;
            if (border is null)
                border = Row.Properties?.TableBorders?.InsideVerticalBorder;
            if (border is null)
                border = Table.Properties?.TableBorders?.InsideVerticalBorder;
            return border;
        }
    }
    public float? BorderLeftWidthPt {
        get => DOCX.Tables.ExtractBorderWidthPt(BorderLeft);
    }
    public CellBorderStyle? BorderLeftStyle {
        get => DOCX.Tables.ExtractBorderStyle(BorderLeft);
    }
    public string BorderLeftColor {
        get => DOCX.Tables.ExtractBorderColor(BorderLeft);
    }

    public string BackgroundColor { get {
        return Props?.Shading?.Fill?.Value;
    } }

    public VerticalAlignment? VAlignment { get {
        var valign = Props?.TableCellVerticalAlignment?.Val;
        if (valign is null)
            return null;
        if (valign == TableVerticalAlignmentValues.Top)
            return VerticalAlignment.Top;
        if (valign == TableVerticalAlignmentValues.Center)
            return VerticalAlignment.Middle;
        if (valign == TableVerticalAlignmentValues.Bottom)
            return VerticalAlignment.Bottom;
        throw new Exception(valign.ToString());
    } }

    internal static IEnumerable<IBlock> ParseCellContents(WCell cell, IEnumerable<OpenXmlElement> elements) {
        return elements.SelectMany(e => ParseCellChild(cell, e));
    }

    internal static IEnumerable<IBlock> ParseCellChild(WCell cell, OpenXmlElement e) {
        if (e is TableCellProperties)
            return Enumerable.Empty<IBlock>();
        if (e is BookmarkStart || e is BookmarkEnd)
            return Enumerable.Empty<IBlock>();
        return Blocks.ParseBlock(cell.Main, e)
            .Where(b => b is not WOldNumberedParagraph np || np.Contents.Any());
    }

}

}
