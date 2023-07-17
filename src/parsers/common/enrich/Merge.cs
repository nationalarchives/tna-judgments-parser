
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse {

class Merger : Enricher {

    public static IEnumerable<IInline> Merge(IEnumerable<IInline> unmerged) {
        if (unmerged.Count() <= 1)
            return unmerged;
        List<IInline> merged = new List<IInline>(unmerged.Count());
        IInline last = unmerged.First();
        foreach (IInline next in unmerged.Skip(1)) {
            if (last is WHyperlink1 || next is WHyperlink1) {
                merged.Add(last);
                last = next;
            } else if (last is SpecialCharacter || next is SpecialCharacter) {
                merged.Add(last);
                last = next;
            } else if (last.GetType().IsSubclassOf(typeof(WText)) || next.GetType().IsSubclassOf(typeof(WText))) {
                merged.Add(last);
                last = next;
            } else if (last is WText fText1 && next is WText fText2 && IFormattedText.HaveSameFormatting(fText1, fText2)) {
                last = new WText(fText1.Text + fText2.Text, fText1.properties);
            } else {
                merged.Add(last);
                last = next;
            }
        }
        merged.Add(last);
        return merged;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> unmerged) {
        return Merge(unmerged);
    }

    /* tables */

    private WTable EnrichTable(WTable table) {
        IEnumerable<WRow> rows = EnrichRows(table.TypedRows);
        return new WTable(table.Main, table.Properties, table.Grid, rows);
    }

    private IEnumerable<WRow> EnrichRows(IEnumerable<WRow> rows) {
        return rows.Select(row => EnrichRow(row));
    }

    private WRow EnrichRow(WRow row) {
        IEnumerable<WCell> cells = EnrichCells((IEnumerable<WCell>) row.Cells);
        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, cells);
    }

    private IEnumerable<WCell> EnrichCells(IEnumerable<WCell> cells) {
        return cells.Select(cell => EnrichCell(cell));
    }

    private WCell EnrichCell(WCell cell) {
        IEnumerable<IBlock> contents = Enrich(cell.Contents);
        return new WCell(cell.Row, cell.Props, contents);
    }

    override protected IBlock Enrich(IBlock block) {
        if (block is WTable table)
            return EnrichTable(table);
        return base.Enrich(block);
    }

    public IBlock Enrich1(IBlock block) {
        return Enrich(block);
    }

}

}
