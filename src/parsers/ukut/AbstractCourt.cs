
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers {

abstract class AbstractCourtType : Enricher2 {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        int i = 0;
        while (i < blocks.Count()) {
            IBlock block1 = blocks.ElementAt(i);
            if (i < blocks.Count() - 1) {
                IBlock block2 = blocks.ElementAt(i + 1);
                List<ILine> two = Match2(block1, block2);
                if (two is not null) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 2);
                    return Enumerable.Concat(Enumerable.Concat(before, two), after);
                }
            }
            List<ILine> one = Match1(block1);
            if (one is not null) {
                IEnumerable<IBlock> before = blocks.Take(i);
                IEnumerable<IBlock> after = blocks.Skip(i + 1);
                return Enumerable.Concat(Enumerable.Concat(before, one), after);
            }
            if (block1 is WTable table) {
                WTable enriched = EnrichTable(table);
                if (!object.ReferenceEquals(table, enriched)) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 1);
                return Enumerable.Concat(before.Append(enriched), after);
                }
            }
            i += 1;
        }
        return blocks;
    }

    protected abstract IEnumerable<Combo2> Combo2s();

    protected abstract IEnumerable<Combo1> Combo1s();

    protected List<ILine> Match2(IBlock one, IBlock two) {
        foreach (Combo2 combo in Combo2s())
            if (combo.Match(one, two))
                return combo.Transform(one, two);
        return null;
    }

    protected List<ILine> Match1(IBlock block) {
        foreach (Combo1 combo in Combo1s())
            if (combo.Match(block))
                return combo.Transform(block);
        return null;
    }

    protected override WCell EnrichCell(WCell cell) {
        IEnumerable<IBlock> contents = Enrich(cell.Contents);
        if (object.ReferenceEquals(contents, cell.Contents))
            return cell;
        return new WCell(cell.Row, contents);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new System.NotImplementedException();
    }

}

}