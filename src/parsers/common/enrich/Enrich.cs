
using System;
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class Enricher {

    internal static IEnumerable<IDecision> Enrich(IEnumerable<IDecision> body, IEnumerable<Enricher> enrichers) {
        return enrichers.Aggregate(body, (done, enricher) => enricher.Enrich(done));
    }
    internal static IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks, IEnumerable<Enricher> enrichers) {
        return enrichers.Aggregate(blocks, (done, enricher) => enricher.Enrich(done));
    }
    internal static IEnumerable<IAnnex> Enrich(IEnumerable<IAnnex> annexes, IEnumerable<Enricher> enrichers) {
        return enrichers.Aggregate(annexes, (done, enricher) => enricher.Enrich(done));
    }



    internal virtual IEnumerable<IDecision> Enrich(IEnumerable<IDecision> body) {
        return body.Select(d => new Decision { Author = (d.Author is null) ? null : Enrich((WLine) d.Author), Contents = Enrich(d.Contents) });
    }

    internal IEnumerable<IDivision> Enrich(IEnumerable<IDivision> divs) {
        List<IDivision> enriched = new List<IDivision>(divs.Count());
        bool changed = false;
        foreach (IDivision div in divs) {
            IDivision enriched1 = Enrich(div);
            enriched.Add(enriched1);
            changed = changed || !Object.ReferenceEquals(enriched1, div);
        }
        return changed ? enriched : divs;
    }

    internal virtual IDivision Enrich(IDivision div) {
        if (div is BigLevel big)
            return new BigLevel() { Number = big.Number, Heading = big.Heading, Children = Enrich(big.Children) };
        if (div is CrossHeading xhead)
            return new CrossHeading() { Heading = Enrich((WLine) xhead.Heading), Children = Enrich(xhead.Children) };
        if (div is GroupOfParagraphs group)
            return new GroupOfParagraphs() { Children = Enrich(group.Children) };
        if (div is WNewNumberedParagraph np)
            return new WNewNumberedParagraph(np.Number, Enrich(np.Contents));
        if (div is WTableOfContents toc)
            return new WTableOfContents(EnrichLines(toc.Contents));
        if (div is WDummyDivision dummy)
            return new WDummyDivision(Enrich(dummy.Contents));
        if (div is BranchParagraph bp)
            return EnrichBranchParagraph(bp);
        if (div is BranchSubparagraph bsp)
            return EnrichBranchSubparagraph(bsp);
        if (div is LeafSubparagraph lsp)
            return EnrichLeafSubparagraph(lsp);
        throw new Exception();
    }

    private BranchParagraph EnrichBranchParagraph(BranchParagraph para) {
        IEnumerable<IBlock> enrichedIntro = para.Intro is null ? null : Enrich(para.Intro);
        IEnumerable<IDivision> enrichedChildren = Enrich(para.Children);
        if (Object.ReferenceEquals(enrichedIntro, para.Intro) && Object.ReferenceEquals(enrichedChildren, para.Children))
            return para;
        return new BranchParagraph { Number = para.Number, Intro = enrichedIntro, Children = enrichedChildren };
    }
    private BranchSubparagraph EnrichBranchSubparagraph(BranchSubparagraph para) {
        IEnumerable<IBlock> enrichedIntro = para.Intro is null ? null : Enrich(para.Intro);
        IEnumerable<IDivision> enrichedChildren = Enrich(para.Children);
        if (Object.ReferenceEquals(enrichedIntro, para.Intro) && Object.ReferenceEquals(enrichedChildren, para.Children))
            return para;
        return new BranchSubparagraph { Number = para.Number, Intro = enrichedIntro, Children = enrichedChildren };
    }
    private LeafSubparagraph EnrichLeafSubparagraph(LeafSubparagraph para) {
        IEnumerable<IBlock> enrichedContents = Enrich(para.Contents);
        if (Object.ReferenceEquals(enrichedContents, para.Contents))
            return para;
        return new LeafSubparagraph { Number = para.Number, Contents = enrichedContents };
    }

    internal virtual IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        bool changed = false;
        foreach (IBlock block in blocks) {
            IBlock enriched1 = Enrich(block);
            enriched.Add(enriched1);
            changed = changed || !Object.ReferenceEquals(enriched1, block);
        }
        return changed ? enriched : blocks;
    }

    protected virtual IBlock Enrich(IBlock block) {
        if (block is WLine line)
            return Enrich(line);
        return block;
    }

    // protected WTable EnrichTable(WTable table) {
    //     IEnumerable<WRow> rows = table.TypedRows.Select(row => {
    //         IEnumerable
    //     });
    //     return new WTable(table.Main, rows);

    // }

    internal virtual IEnumerable<ILine> EnrichLines(IEnumerable<ILine> blocks) => blocks.Select(EnrichILine);

    private ILine EnrichILine(ILine line) {
        if (line is WLine wLine)
            return Enrich(wLine);
        return line;
    }

    protected virtual WLine Enrich(WLine line) {
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (object.ReferenceEquals(enriched, line.Contents))
            return line;
        return WLine.Make(line, enriched);
    }

    abstract protected IEnumerable<IInline> Enrich(IEnumerable<IInline> line);

    internal IEnumerable<IAnnex> Enrich(IEnumerable<IAnnex> annexes) {
        return annexes.Select(a => new Annex { Number = a.Number, Contents = Enrich(a.Contents) });
    }
}

abstract class Enricher2 : Enricher {

    protected override IBlock Enrich(IBlock block) {
        if (block is WLine line)
            return Enrich(line);
        if (block is WTable table)
            return EnrichTable(table);
        return block;
    }

    protected WTable EnrichTable(WTable table) {
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

    private WRow EnrichRow(WRow row) {
        IEnumerator<WCell> cells = row.TypedCells.GetEnumerator();
        List<WCell> enriched = new List<WCell>();
        while (cells.MoveNext()) {
            WCell before = cells.Current;
            WCell after = EnrichCell(before);
            enriched.Add(after);
            if (!object.ReferenceEquals(before, after)) {
                while (cells.MoveNext())
                    enriched.Add(cells.Current);
                return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, enriched);
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