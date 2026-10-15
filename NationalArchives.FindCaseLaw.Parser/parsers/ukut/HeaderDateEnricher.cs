using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

internal class HeaderDateEnricher : Enricher
{
    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        var enumerator = blocks.GetEnumerator();
        var contents = new List<IBlock>(blocks.Count());
        var i = 0;
        while (enumerator.MoveNext())
        {
            var block = enumerator.Current;
            var enrichecd = Enrich(block);
            contents.Add(enrichecd);
            if (!ReferenceEquals(enrichecd, block))
            {
                var date = Util.Descendants<WDocDate>(enrichecd).First();
                if (date.Name == "decision")
                {
                    break;
                }
            }

            i += 1;
        }

        while (enumerator.MoveNext())
        {
            contents.Add(enumerator.Current);
        }

        return contents;
    }

    protected override IBlock Enrich(IBlock block)
    {
        if (block is WLine line)
        {
            return EnrichLineOutsideTable(line);
        }

        if (block is WTable table)
        {
            return EnrichTable(table);
        }

        return block;
    }

    private WLine EnrichLineOutsideTable(WLine line)
    {
        var text = line.NormalizedContent;
        if (text.StartsWith("Date of Decision ") || text.StartsWith("Date of Decision: "))
        {
            return LineDateEnricher.Enrich(line, "decision", 1);
        }

        if (text.StartsWith("Decision given on ") || text.StartsWith("Decision given on: "))
        {
            return LineDateEnricher.Enrich(line, "decision", 1);
        }

        if (text.StartsWith("Hearing date ") || text.StartsWith("Hearing date: "))
        {
            return LineDateEnricher.Enrich(line, "hearing", 0);
        }

        if (text.StartsWith("Heard on ") || text.StartsWith("Heard on: "))
        {
            return LineDateEnricher.Enrich(line, "hearing", 0);
        }

        return line;
    }

    private WTable EnrichTable(WTable table)
    {
        if (table.TypedRows.Count < 2)
        {
            return table;
        }

        var row1 = table.TypedRows.ElementAt(0);
        var row2 = table.TypedRows.ElementAt(1);
        if (IsRowBeforeHearingDate(row1))
        {
            var enriched2 = EnrichRowWithHearingDate(row2);
            if (!ReferenceEquals(enriched2, row2))
            {
                return ReplaceSecondRow(table, enriched2);
            }

            var enriched1 = EnrichSecondLineOfFirstCellWithHearingDate(row1);
            if (!ReferenceEquals(enriched1, row1))
            {
                return ReplaceFirstRow(table, enriched1);
            }
        }

        return table;
    }

    private WTable ReplaceFirstRow(WTable table, WRow row1)
    {
        return new WTable(table.Main, table.Properties, table.Grid, table.TypedRows.Skip(1).Prepend(row1));
    }

    private WTable ReplaceSecondRow(WTable table, WRow row2)
    {
        return new WTable(table.Main, table.Properties, table.Grid,
            new List<WRow>(2) { table.TypedRows.First(), row2 }.Concat(table.TypedRows.Skip(2)
            ));
    }

    private bool IsRowBeforeHearingDate(WRow row)
    {
        if (!row.TypedCells.Any())
        {
            return false;
        }

        var cell = row.TypedCells.First();
        if (!cell.Contents.Any())
        {
            return false;
        }

        var block = cell.Contents.First();
        if (block is not WLine line)
        {
            return false;
        }

        var normalized = line.NormalizedContent;
        if (normalized.StartsWith("Heard at "))
        {
            return true;
        }

        if (normalized.StartsWith("At: "))
        {
            return true;
        }

        if (normalized.StartsWith("Heard by "))
        {
            return true;
        }

        return false;
    }

    private WRow EnrichRowWithHearingDate(WRow row)
    {
        if (!row.Cells.Any())
        {
            return row;
        }

        var cell = row.TypedCells.First();
        var enriched = EnrichCellWithDate(cell, "hearing", 0);
        if (ReferenceEquals(enriched, cell))
        {
            return row;
        }

        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            row.TypedCells.Skip(1).Prepend(enriched));
    }

    private WRow EnrichSecondLineOfFirstCellWithHearingDate(WRow row)
    {
        if (!row.Cells.Any())
        {
            return row;
        }

        var cell = row.TypedCells.First();
        var enriched = EnrichSecondLineOfCellWithDate(cell, "hearing", 0);
        if (ReferenceEquals(enriched, cell))
        {
            return row;
        }

        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties,
            row.TypedCells.Skip(1).Prepend(enriched));
    }

    private WCell EnrichCellWithDate(WCell cell, string name, int priority)
    {
        if (!cell.Contents.Any())
        {
            return cell;
        }

        var block = cell.Contents.First();
        if (block is not WLine line)
        {
            return cell;
        }

        var enriched = EnrichLineInTableCell(line, name, priority);
        if (ReferenceEquals(enriched, line))
        {
            return cell;
        }

        return new WCell(cell.Row, cell.Props, cell.Contents.Skip(1).Prepend(enriched));
    }

    private WCell EnrichSecondLineOfCellWithDate(WCell cell, string name, int priority)
    {
        if (cell.Contents.Count() < 2)
        {
            return cell;
        }

        var block = cell.Contents.ElementAt(1);
        if (block is not WLine line)
        {
            return cell;
        }

        var enriched = EnrichLineInTableCell(line, name, priority);
        if (ReferenceEquals(enriched, line))
        {
            return cell;
        }

        return new WCell(cell.Row, cell.Props, new List<IBlock>(2) { cell.Contents.First(), enriched }.Concat(
            cell.Contents.Skip(2)
        ));
    }

    private WLine EnrichLineInTableCell(WLine line, string name, int priority)
    {
        return LineDateEnricher.Enrich(line, name, priority);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        throw new NotImplementedException();
    }
}
