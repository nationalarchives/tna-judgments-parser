
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse.UKSC {

class JudgeEnricher : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> contents = new List<IBlock>(blocks.Count());
        IEnumerator<IBlock> enumerator = blocks.GetEnumerator();
        while (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            if (block is not WLine line)
                break;
            WLine enriched = Enrich(line);
            contents.Add(enriched);
            if (!Object.ReferenceEquals(enriched, line)) {
                while (enumerator.MoveNext())
                    contents.Add(enumerator.Current);
                return contents;
            }
        }
        return blocks;
    }

    protected override WLine Enrich(WLine line) {
        if (line.Style != "JudgmentJudges")
            return line;
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (Object.ReferenceEquals(enriched, line.Contents))
            return line;
        return new WLine(line, enriched);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        bool found = false;
        List<IInline> contents = new List<IInline>(line.Count());
        foreach (IInline inline in line) {
            IInline enriched = Enrich(inline);
            contents.Add(enriched);
            found = found || enriched is WJudge;
        }
        if (found)
            return contents;
        return line;
    }

    private IInline Enrich(IInline inline) {
        if (inline is not WText wText)
            return inline;
        if (wText.Text.StartsWith("Lord ") || wText.Text.StartsWith("Lady "))
            return new WJudge(wText.Text, wText.properties);
        return inline;
    }

}

}
