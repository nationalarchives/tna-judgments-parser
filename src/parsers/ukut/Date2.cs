
using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class Date2 : Enricher {

    internal override IEnumerable<IDecision> Enrich(IEnumerable<IDecision> body) {
        if (!body.Any())
            return body;
        IDecision last = body.Last();
        IDecision enriched = Enrich(last);
        if (object.ReferenceEquals(enriched, last))
            return body;
        return body.SkipLast(1).Append(enriched);
    }

    internal IDecision Enrich(IDecision decision) {
        if (!decision.Contents.Any())
            return decision;
        if (decision.Contents.All(child => child is ILeaf)) {
            IEnumerable<ILeaf> children2 = EnrichLast<ILeaf>(decision.Contents.Cast<ILeaf>(), EnrichLeaf);
            if (!object.ReferenceEquals(children2, decision.Contents)) {
                return Substitute(decision, children2);

            }
        }
        IDivision last = decision.Contents.Last();
        IDivision enriched = Enrich(last);
        if (object.ReferenceEquals(enriched, last))
            return decision;
        return new Decision {
            Author = decision.Author,
            Contents = decision.Contents.SkipLast(1).Append(enriched)
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
        if (!branch.Children.Any())
            return branch;
        if (branch.Children.All(child => child is ILeaf)) {
            IEnumerable<ILeaf> children2 = EnrichLast<ILeaf>(branch.Children.Cast<ILeaf>(), EnrichLeaf);
            if (!object.ReferenceEquals(children2, branch.Children))
                return Substitute(branch, children2);
        }
        IDivision last = branch.Children.Last();
        IDivision enriched = Enrich(last);
        if (object.ReferenceEquals(enriched, last))
            return branch;
        return Substitute(branch, branch.Children.SkipLast(1).Append(enriched));
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
