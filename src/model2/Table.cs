
using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UK.Gov.Legislation.Lawmaker;

namespace UK.Gov.Legislation.Judgments.Parse;

class WTable : ITable, ILineable {

    internal MainDocumentPart Main { get; private init; }

    internal TableProperties Properties { get; private init; }
    internal TableGrid Grid { get; private init; }

    public List<WRow> TypedRows { get; private init; }
    public IEnumerable<IRow> Rows { get => TypedRows; }

    public IEnumerable<WLine> Lines => Rows
        .SelectMany(row => row.Cells)
        .SelectMany(cell => cell.Contents)
        .OfType<ILineable>() // this may falsely filter some things,
        .SelectMany(lineable => lineable.Lines);


    public List<float> ColumnWidthsIns {
        get => DOCX.Tables.GetColumnWidthsIns(Grid);
    }

    public WTable(MainDocumentPart main, Table table) {
        Main = main;
        Properties = table.ChildElements.OfType<TableProperties>().FirstOrDefault();
        Grid = table.ChildElements.OfType<TableGrid>().FirstOrDefault();
        Style = Properties?.TableStyle?.Val?.Value;
        TypedRows = ParseTableContents(this, table.ChildElements).ToList();
    }

    public WTable(MainDocumentPart main, TableProperties props, TableGrid grid, IEnumerable<WRow> rows) {
        Main = main;
        Properties = props;
        Grid = grid;
        Style = Properties?.TableStyle?.Val?.Value;
        TypedRows = rows.Select(row => new WRow(this, row.TablePropertyExceptions, row.Properties, row.TypedCells)).ToList();
    }

    private static IEnumerable<WRow> ParseTableContents(WTable table, IEnumerable<OpenXmlElement> elements) {
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

    internal static WTable Enrich(WTable table, Func<WCell, WCell> operation)
    {
        IEnumerator<WRow> rows = table.TypedRows.GetEnumerator();
        List<WRow> enriched = new List<WRow>();
        while (rows.MoveNext())
        {
            WRow before = rows.Current;
            WRow after = WRow.Enrich(before, operation);
            enriched.Add(after);
        }
        return new WTable(table.Main, table.Properties, table.Grid, enriched);
    }

}

class WRow : IRow {

    internal MainDocumentPart Main { get; private init; }

    internal WTable Table { get; private init; }
    internal TablePropertyExceptions TablePropertyExceptions { get; private init; }

    internal TableRowProperties Properties { get; private init; }
    public bool IsHeader { get {
        var tblHeader = Properties?.ChildElements.OfType<TableHeader>().FirstOrDefault();
        if (tblHeader is null)
            return false;
        return DOCX.Util.OnOffToBool(tblHeader.Val) ?? true;
    } }

    public bool IsImplicitHeader { get {
        IEnumerable<WLine> rowLines = TypedCells
            .SelectMany(cell => cell.Contents)
            .OfType<WLine>();
        IEnumerable<WText> rowTexts = rowLines
            .SelectMany(line => line.Contents)
            .OfType<WText>();

        bool isFirstRow = Table.Rows.First() == this;
        bool isAllItalic = rowTexts.All(text => text.Italic ?? false) || rowLines.All(line => line.IsAllItalicized());
        bool isAllBold = rowTexts.All(text => text.Bold ?? false);

        return IsHeader || (isFirstRow && (isAllItalic || isAllBold));
    }}

    public List<WCell> TypedCells { get; private init; }
    public IEnumerable<ICell> Cells { get => TypedCells; }

    internal WRow(WTable table, TableRow row) {
        Main = table.Main;
        Table = table;
        TablePropertyExceptions = row.TablePropertyExceptions;
        Properties = row.TableRowProperties;
        TypedCells = ParseRowContents(this, row.ChildElements).ToList();
    }
    internal WRow(WTable table, TablePropertyExceptions exceptions, TableRowProperties props, IEnumerable<WCell> cells) {
        Main = table.Main;
        Table = table;
        TablePropertyExceptions = exceptions;
        Properties = props;
        TypedCells = cells.Select(c => new WCell(this, c.Props, c.Contents)).ToList();
    }

    private static IEnumerable<WCell> ParseRowContents(WRow row, IEnumerable<OpenXmlElement> elements) {
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

    internal static WRow Enrich(WRow row, Func<WCell, WCell> operation)
    {
        IEnumerator<WCell> cells = row.TypedCells.GetEnumerator();
        List<WCell> enriched = new List<WCell>();
        while (cells.MoveNext())
        {
            WCell before = cells.Current;
            WCell after = WCell.Enrich(before, operation);
            enriched.Add(after);
        }
        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, enriched);
    }

}

class WCell : ICell {

    internal MainDocumentPart Main { get; private init; }

    internal WTable Table { get => Row.Table; }

    internal WRow Row { get; private init; }

    internal TableCellProperties Props { get; init; }

    public IEnumerable<IBlock> Contents { get; set; }

    internal WCell(WRow row, TableCell cell) {
        Main = row.Main;
        Row = row;
        Props = cell.TableCellProperties;
        Contents = ParseCellContents(this, cell.ChildElements).ToList();
    }
    internal WCell(WRow row, TableCellProperties props, IEnumerable<IBlock> contents) {
        Main = row.Main;
        Row = row;
        Props = props;
        Contents = contents.ToList();
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
                border = Row.TablePropertyExceptions?.TableBorders?.InsideHorizontalBorder;
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
                border = Row.TablePropertyExceptions?.TableBorders?.InsideVerticalBorder;
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
                border = Row.TablePropertyExceptions?.TableBorders?.InsideHorizontalBorder;
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
                border = Row.TablePropertyExceptions?.TableBorders?.InsideVerticalBorder;
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

    private static IEnumerable<IBlock> ParseCellContents(WCell cell, IEnumerable<OpenXmlElement> elements) {
        return elements.SelectMany(e => ParseCellChild(cell, e));
    }

    private static IEnumerable<IBlock> ParseCellChild(WCell cell, OpenXmlElement e) {
        if (e is TableCellProperties)
            return [];
        if (e is BookmarkStart || e is BookmarkEnd)
            return [];
        // this is especially important for paragraphs with numbers, see [2024] EWHC 3163 (Comm)
        if (cell.VMerge == VerticalMerge.Continuation && e is Paragraph p && string.IsNullOrWhiteSpace(e.InnerText))
            return [];
        return Blocks.ParseBlock(cell.Main, e);
    }

    /*
    internal static WCell Enrich(WCell cell, Func<IBlock, IBlock> operation)
    {
        IEnumerator<IBlock> contents = cell.Contents.GetEnumerator();
        List<IBlock> enriched = new List<IBlock>();
        while (contents.MoveNext())
        {
            IBlock before = contents.Current;
            IBlock after = operation(before);
            enriched.Add(after);
        }
        return new WCell(cell.Row, cell.Props, enriched);
    }*/

    internal static WCell Enrich(WCell cell, Func<WCell, WCell> operation)
    {
        return operation(cell);
    }

}