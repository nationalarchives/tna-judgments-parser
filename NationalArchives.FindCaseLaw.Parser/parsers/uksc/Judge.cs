using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse.UKSC;

internal class JudgeEnricher : Enricher
{
    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
    {
        var contents = new List<IBlock>(blocks.Count());
        var enumerator = blocks.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var block = enumerator.Current;
            if (block is not WLine line)
            {
                break;
            }

            var enriched = Enrich(line);
            contents.Add(enriched);
            if (!ReferenceEquals(enriched, line))
            {
                while (enumerator.MoveNext())
                {
                    contents.Add(enumerator.Current);
                }

                return contents;
            }
        }

        return blocks;
    }

    protected override WLine Enrich(WLine line)
    {
        if (line.Style != "JudgmentJudges")
        {
            return line;
        }

        return base.Enrich(line);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
    {
        var found = false;
        var contents = new List<IInline>(line.Count());
        foreach (var inline in line)
        {
            var enriched = Enrich(inline);
            contents.Add(enriched);
            found = found || enriched is WJudge;
        }

        if (found)
        {
            return contents;
        }

        return line;
    }

    private IInline Enrich(IInline inline)
    {
        if (inline is not WText wText)
        {
            return inline;
        }

        if (wText.Text.StartsWith("Lord ") || wText.Text.StartsWith("Lady "))
        {
            return new WJudge(wText.Text, wText.properties);
        }

        return inline;
    }
}
