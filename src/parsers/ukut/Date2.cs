
using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class Date2 : Enricher {

    internal override IEnumerable<IDecision> Enrich(IEnumerable<IDecision> body) {
        return EnrichLast<IDecision>(body, EnrichDecision);
    }

    internal IDecision EnrichDecision(IDecision decision) {
        IEnumerable<IDivision> enriched = EnrichLast<IDivision>(decision.Contents, Enrich);
        if (object.ReferenceEquals(enriched, decision.Contents))
            return decision;
        return new Decision {
            Author = decision.Author,
            Contents = enriched
        };
    }

    private IDecision Substitute(IDecision decision, IEnumerable<ILeaf> children) {
        if (decision is Decision d)
            return new Decision() { Author = decision.Author, Contents = children };
        throw new NotImplementedException();
    }

    internal override IDivision Enrich(IDivision division) {
        if (division is IBranch branch)
            return EnrichBranch(branch);
        if (division is ILeaf leaf)
            return EnrichLeaf(leaf);
        throw new NotImplementedException();
    }

    internal IDivision EnrichBranch(IBranch branch) {
        IEnumerable<IDivision> enriched = EnrichLast<IDivision>(branch.Children, Enrich);
        if (object.ReferenceEquals(enriched, branch.Children))
            return branch;
        return Substitute(branch, enriched);
    }

    private IBranch Substitute(IBranch branch, IEnumerable<IDivision> children) {
        if (branch is BigLevel bl)
            return new BigLevel() {
                Number = bl.Number,
                Heading = bl.Heading,
                Children = children
            };
        if (branch is CrossHeading xh)
            return new CrossHeading() {
                Heading = xh.Heading,
                Children = children
            };
        if (branch is GroupOfParagraphs gp)
            return new GroupOfParagraphs() {
                Children = children
            };
        if (branch is BranchParagraph bp)
            return new BranchParagraph {
                Number = bp.Number,
                Intro = bp.Intro,
                Children = children
            };
        if (branch is BranchSubparagraph bsp)
            return new BranchSubparagraph {
                Number = bsp.Number,
                Intro = bsp.Intro,
                Children = children
            };
        throw new NotImplementedException();
    }

    internal IEnumerable<T> EnrichLast<T>(IEnumerable<T> items, Func<T,T> enrich) {
        LinkedList<T> list = new LinkedList<T>();
        IEnumerator<T> enumerator = items.Reverse().GetEnumerator();
        while (enumerator.MoveNext()) {
            T item = enumerator.Current;
            T enriched = enrich(item);
            list.AddFirst(enriched);
            if (object.ReferenceEquals(enriched, item))
                continue;
            while (enumerator.MoveNext())
                list.AddFirst(enumerator.Current);
            return list;
        }
        return items;
    }

    internal ILeaf EnrichLeaf(ILeaf leaf) {
        if (!leaf.Contents.Any())
            return leaf;
        IEnumerable<IBlock> enriched = Enrich(leaf.Contents);
        if (object.ReferenceEquals(enriched, leaf.Contents))
            return leaf;
        if (leaf is WNewNumberedParagraph np)
            return new WNewNumberedParagraph(np.Number, enriched);
        if (leaf is WDummyDivision dd)
            return new WDummyDivision(enriched);
        if (leaf is LeafSubparagraph sp)
            return new LeafSubparagraph { Number = sp.Number, Contents = enriched };
        throw new NotImplementedException();
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        return EnrichLast<IBlock>(blocks, Enrich);
    }

    protected override WLine Enrich(WLine line) {
        return Date0.Enrich(line, "decision", 1);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

}

}
