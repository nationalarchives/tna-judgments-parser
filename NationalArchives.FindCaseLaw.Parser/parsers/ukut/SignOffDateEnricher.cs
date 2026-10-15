using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

internal class SignOffDateEnricher : Enricher
{
    internal override IEnumerable<IDecision> Enrich(IEnumerable<IDecision> body)
    {
        return EnrichLast(body, EnrichDecision);
    }

    internal IDecision EnrichDecision(IDecision decision)
    {
        var enriched = EnrichLast(decision.Contents, Enrich);
        if (ReferenceEquals(enriched, decision.Contents))
        {
            return decision;
        }

        return new Decision { Author = decision.Author, Contents = enriched };
    }

    internal override IDivision Enrich(IDivision division)
    {
        if (division is IBranch branch)
        {
            return EnrichBranch(branch);
        }

        if (division is ILeaf leaf)
        {
            return EnrichLeaf(leaf);
        }

        if (division is ITableOfContents)
        {
            return division;
        }

        throw new NotImplementedException();
    }

    internal IDivision EnrichBranch(IBranch branch)
    {
        var enriched = EnrichLast(branch.Children, Enrich);
        if (ReferenceEquals(enriched, branch.Children))
        {
            return branch;
        }

        return Substitute(branch, enriched);
    }

    private IBranch Substitute(IBranch branch, IEnumerable<IDivision> children)
    {
        if (branch is BigLevel bl)
        {
            return new BigLevel
            {
                Number = bl.Number,
                Heading = bl.Heading,
                Children = children
            };
        }

        if (branch is CrossHeading xh)
        {
            return new CrossHeading { Heading = xh.Heading, Children = children };
        }

        if (branch is GroupOfParagraphs)
        {
            return new GroupOfParagraphs { Children = children };
        }

        if (branch is BranchParagraph bp)
        {
            return new BranchParagraph
            {
                Number = bp.Number,
                Intro = bp.Intro,
                Children = children
            };
        }

        if (branch is BranchSubparagraph bsp)
        {
            return new BranchSubparagraph
            {
                Number = bsp.Number,
                Intro = bsp.Intro,
                Children = children
            };
        }

        throw new NotImplementedException();
    }

    internal IEnumerable<T> EnrichLast<T>(IEnumerable<T> items, Func<T, T> enrich)
    {
        var list = new LinkedList<T>();
        var enumerator = items.Reverse().GetEnumerator();
        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
            var enriched = enrich(item);
            list.AddFirst(enriched);
            if (ReferenceEquals(enriched, item))
            {
                continue;
            }

            while (enumerator.MoveNext())
            {
                list.AddFirst(enumerator.Current);
            }

            return list;
        }

        return items;
    }

    internal ILeaf EnrichLeaf(ILeaf leaf)
    {
        if (!leaf.Contents.Any())
        {
            return leaf;
        }

        var enriched = Enrich(leaf.Contents);
        if (ReferenceEquals(enriched, leaf.Contents))
        {
            return leaf;
        }

        if (leaf is WNewNumberedParagraph np)
        {
            return new WNewNumberedParagraph(np.Number, enriched);
        }

        if (leaf is WDummyDivision)
        {
            return new WDummyDivision(enriched);
        }

        if (leaf is LeafSubparagraph sp)
        {
            return new LeafSubparagraph { Number = sp.Number, Contents = enriched };
        }

        throw new NotImplementedException();
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        return EnrichLast(blocks, Enrich);
    }

    protected override WLine Enrich(WLine line)
    {
        return LineDateEnricher.Enrich(line, "decision", 1);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        throw new NotImplementedException();
    }
}
