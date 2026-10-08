
using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class Date1 : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        var enumerator = blocks.GetEnumerator();
        List<IBlock> contents = new List<IBlock>(blocks.Count());
        int i = 0;
        while (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            IBlock enrichecd = Enrich(block);
            contents.Add(enrichecd);
            if (!object.ReferenceEquals(enrichecd, block)) {
                WDocDate date = Util.Descendants<WDocDate>(enrichecd).First();
                if (date.Name == "decision")
                    break;
            }
            i += 1;
        }
        while (enumerator.MoveNext())
            contents.Add(enumerator.Current);
        return contents;
    }

    protected override IBlock Enrich(IBlock block) {
        if (block is WLine line)
            return EnrichLineOutsideTable(line);
        if (block is WTable table)
            return EnrichTable(table);
        return block;
    }

    private WLine EnrichLineOutsideTable(WLine line) {
        string text = line.NormalizedContent;
        if (text.StartsWith("Date of Decision ") || text.StartsWith("Date of Decision: "))
            return Date0.Enrich(line, "decision", 1);
        if (text.StartsWith("Decision given on ") || text.StartsWith("Decision given on: "))
            return Date0.Enrich(line, "decision", 1);
        if (text.StartsWith("Hearing date ") || text.StartsWith("Hearing date: "))
            return Date0.Enrich(line, "hearing", 0);
        if (text.StartsWith("Heard on ") || text.StartsWith("Heard on: "))
            return Date0.Enrich(line, "hearing", 0);
        return line;
    }

    protected IEnumerable<IInline> EnrichOutsideTable(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

    /* */

    private WTable EnrichTable(WTable table) {
        if (table.TypedRows.Count() < 2)
            return table;
        WRow row1 = table.TypedRows.ElementAt(0);
        WRow row2 = table.TypedRows.ElementAt(1);
        if (IsRowBeforeHearingDate(row1)) {
            WRow enriched2 = EnrichRowWithHearingDate(row2);
            if (!object.ReferenceEquals(enriched2, row2))
                return ReplaceSecondRow(table, enriched2);
            WRow enriched1 = EnrichSecondLineOfFirstCellWithHearingDate(row1);
            if (!object.ReferenceEquals(enriched1, row1))
                return ReplaceFirstRow(table, enriched1);
        }
        return table;
    }

    private WTable ReplaceFirstRow(WTable table, WRow row1) {
        return new WTable(table.Main, table.Properties, table.Grid, table.TypedRows.Skip(1).Prepend(row1));
    }
    private WTable ReplaceSecondRow(WTable table, WRow row2) {
        return new WTable(table.Main, table.Properties, table.Grid, Enumerable.Concat<WRow>(
            new List<WRow>(2) { table.TypedRows.First(), row2 },
            table.TypedRows.Skip(2)
        ));
    }

    private bool IsRowBeforeHearingDate(WRow row) {
        if (!row.TypedCells.Any())
            return false;
        WCell cell = row.TypedCells.First();
        if (!cell.Contents.Any())
            return false;
        IBlock block = cell.Contents.First();
        if (block is not WLine line)
            return false;
        string normalized = line.NormalizedContent;
        if (normalized.StartsWith("Heard at "))
            return true;
        if (normalized.StartsWith("At: "))
            return true;
        if (normalized.StartsWith("Heard by "))
            return true;
        return false;
    }

    private bool IsRowBeforeDecisionDate(WRow row) {
        if (!row.TypedCells.Any())
            return false;
        WCell cell = row.TypedCells.Last();
        if (!cell.Contents.Any())
            return false;
        IBlock block = cell.Contents.First();
        if (block is not WLine line)
            return false;
        string normalized = line.NormalizedContent;
        if (normalized == "Decision & Reasons Promulgated")
            return true;
        return false;
    }

    private WRow EnrichRowWithHearingDate(WRow row) {
        if (!row.Cells.Any())
            return row;
        WCell cell = row.TypedCells.First();
        WCell enriched = EnrichCellWithDate(cell, "hearing", 0);
        if (object.ReferenceEquals(enriched, cell))
            return row;
        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, row.TypedCells.Skip(1).Prepend(enriched));
    }
    private WRow EnrichSecondLineOfFirstCellWithHearingDate(WRow row) {
        if (!row.Cells.Any())
            return row;
        WCell cell = row.TypedCells.First();
        WCell enriched = EnrichSecondLineOfCellWithDate(cell, "hearing", 0);
        if (object.ReferenceEquals(enriched, cell))
            return row;
        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, row.TypedCells.Skip(1).Prepend(enriched));
    }

    private WRow EnrichRowWithDecisionDate(WRow row) {
        if (!row.Cells.Any())
            return row;
        WCell cell = row.TypedCells.First();
        WCell enriched = EnrichCellWithDate(cell, "decision", 1);
        if (object.ReferenceEquals(enriched, cell))
            return row;
        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, row.TypedCells.Skip(1).Prepend(enriched));
    }

    private WCell EnrichCellWithDate(WCell cell, string name, int priority) {
        if (!cell.Contents.Any())
            return cell;
        IBlock block = cell.Contents.First();
        if (block is not WLine line)
            return cell;
        WLine enriched = EnrichLineInTableCell(line, name, priority);
        if (object.ReferenceEquals(enriched, line))
            return cell;
        return new WCell(cell.Row, cell.Props, cell.Contents.Skip(1).Prepend(enriched));
    }
    private WCell EnrichSecondLineOfCellWithDate(WCell cell, string name, int priority) {
        if (cell.Contents.Count() < 2)
            return cell;
        IBlock block = cell.Contents.ElementAt(1);
        if (block is not WLine line)
            return cell;
        WLine enriched = EnrichLineInTableCell(line, name, priority);
        if (object.ReferenceEquals(enriched, line))
            return cell;
        return new WCell(cell.Row, cell.Props, Enumerable.Concat(
            new List<IBlock>(2) { cell.Contents.First(), enriched },
            cell.Contents.Skip(2)
        ));
    }

    private WLine EnrichLineInTableCell(WLine line, string name, int priority) {
        return Date0.Enrich(line, name, priority);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

}

}
