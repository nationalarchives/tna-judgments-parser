
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

abstract class FirstMatch : Enricher {

    protected int? Limit;

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        var enumerator = blocks.GetEnumerator();
        List<IBlock> contents = new List<IBlock>(blocks.Count());
        int i = 0;
        while (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            IBlock enrichecd = Enrich(block);
            contents.Add(enrichecd);
            if (!object.ReferenceEquals(enrichecd, block))
                break;
            if (Limit.HasValue && i >= Limit.Value)
                return blocks;
            i += 1;
        }
        while (enumerator.MoveNext())
            contents.Add(enumerator.Current);
        return contents;
    }

}

abstract class FirstMatch2 : FirstMatch {

    protected override IBlock Enrich(IBlock block) {
        if (block is WOldNumberedParagraph np)
            return new WOldNumberedParagraph(np.Number, Enrich((WLine) np));
        if (block is WLine line)
            return Enrich(line);
        if (block is WTable table)
            return EnrichTable(table);
        return block;
    }

    protected virtual WTable EnrichTable(WTable table) {
        IEnumerator<WRow> rows = table.TypedRows.GetEnumerator();
        List<WRow> enriched = new List<WRow>();
        while (rows.MoveNext()) {
            WRow before = rows.Current;
            WRow after = EnrichRow(before);
            enriched.Add(after);
            if (!object.ReferenceEquals(before, after)) {
                while (rows.MoveNext())
                    enriched.Add(rows.Current);
                return new WTable(table.Main, table.Properties, table.Grid, enriched);
            }
        }
        return table;
    }

    protected virtual WRow EnrichRow(WRow row) {
        IEnumerator<WCell> cells = row.TypedCells.GetEnumerator();
        List<WCell> enriched = new List<WCell>();
        while (cells.MoveNext()) {
            WCell before = cells.Current;
            WCell after = EnrichCell(before);
            enriched.Add(after);
            if (!object.ReferenceEquals(before, after)) {
                while (cells.MoveNext())
                    enriched.Add(cells.Current);
                return new WRow(row.Table, row.Properties, enriched);
            }
        }
        return row;
    }

    protected virtual WCell EnrichCell(WCell cell) {
        IEnumerator<IBlock> contents = cell.Contents.GetEnumerator();
        List<IBlock> enriched = new List<IBlock>();
        while (contents.MoveNext()) {
            IBlock before = contents.Current;
            IBlock after = Enrich(before);
            enriched.Add(after);
            if (!object.ReferenceEquals(before, after)) {
                while (contents.MoveNext())
                    enriched.Add(contents.Current);
                return new WCell(cell.Row, cell.Props, enriched);
            }
        }
        return cell;
    }

}

}
