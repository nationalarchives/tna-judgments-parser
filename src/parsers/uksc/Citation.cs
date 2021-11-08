
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse.UKSC {

class CiteEnricher : Enricher {

    private static string[] patterns = {
        @"^(\[\d{4}\] (UKSC|UKPC) \d+) *$",
    };

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        bool found = false;
        List<IBlock> contents = new List<IBlock>(blocks.Count());
        foreach (IBlock block in blocks) {
            if (found) {
                contents.Add(block);
                continue;
            }
            IBlock enriched = Enrich(block);
            found = !Object.ReferenceEquals(enriched, block);
            contents.Add(enriched);
        }
        if (found)
            return contents;
        return blocks;
    }

    protected override IBlock Enrich(IBlock block) {
        if (block is WLine line)
            return Enrich(line);
        return block;
    }

    protected override WLine Enrich(WLine line) {
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (Object.ReferenceEquals(enriched, line.Contents))
            return line;
        return new WLine(line, enriched);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        bool found = false;
        List<IInline> contents = new List<IInline>(line.Count());
        foreach (IInline inline in line) {
            if (found) {
                contents.Add(inline);
                continue;
            }
            List<IInline> enriched = Enrich(inline);
            found = enriched.OfType<WNeutralCitation>().Any();
            contents.AddRange(enriched);
        }
        if (found)
            return contents;
        return line;
    }

    private List<IInline> Enrich(IInline inline) {
        if (inline is not WText wText)
            return new List<IInline>(1) { inline };
        foreach (string pattern in patterns) {
            Match match = Regex.Match(wText.Text, pattern);
            if (match.Success) {
                WNeutralCitation nc = new WNeutralCitation(match.Groups[1].Value, wText.properties);
                List<IInline> contents = new List<IInline>(2) { nc };
                if (wText.Text.Length > match.Groups[1].Index + match.Groups[1].Length) {
                    WText after = new WText(wText.Text.Substring(match.Groups[1].Index + match.Groups[1].Length), wText.properties);
                    contents.Add(after);
                }
                return contents;
            }
        }
        return new List<IInline>(1) { inline };
    }
    // private IInline Enrich(IInline inline) {
    //     if (inline is not WText wText)
    //         return inline;
    //     foreach (string pattern in patterns)
    //         if (Regex.IsMatch(wText.Text, pattern))
    //             return new WNeutralCitation(wText.Text, wText.properties);
    //     return inline;
    // }

}

}
