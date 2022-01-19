
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse.UKSC {

class CiteEnricher : Enricher {

    private static string[] patterns = {
        @"^ *(\[\d{4}\] (UKSC|UKPC) \d+) *$",
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
        if (line.Count() == 3) {
            IInline one = line.ElementAt(0);
            IInline two = line.ElementAt(1);
            IInline three = line.ElementAt(2);
            if (one is not WText wText1)
                return line;
            if (two is not WText wText2)
                return line;
            if (three is not WText wText3)
                return line;
            if (wText2.Text != " ")
                return line;
            if (!IFormattedText.HaveSameFormatting(wText1, wText3))
                return line;
            string combined = wText1.Text + wText2.Text + wText3.Text;
            foreach (string pattern in patterns) {
                Match match = Regex.Match(combined, pattern);
                if (match.Success) {
                    Group group = match.Groups[1];
                    List<IInline> contents2 = new List<IInline>(3) { };
                    if (group.Index > 0) {
                        WText before = new WText(combined.Substring(0, group.Index), wText1.properties);
                        contents2.Add(before);
                    }
                    WNeutralCitation nc = new WNeutralCitation(group.Value, wText1.properties);
                    contents2.Add(nc);
                    if (combined.Length > group.Index + group.Length) {
                        WText after = new WText(combined.Substring(group.Index + group.Length), wText3.properties);
                        contents2.Add(after);
                    }
                    return contents2;
                }
            }
        }
        return line;
    }

    private List<IInline> Enrich(IInline inline) {
        if (inline is not WText wText)
            return new List<IInline>(1) { inline };
        foreach (string pattern in patterns) {
            Match match = Regex.Match(wText.Text, pattern);
            if (match.Success) {
                Group group = match.Groups[1];
                List<IInline> contents = new List<IInline>(3) { };
                if (group.Index > 0) {
                    WText before = new WText(wText.Text.Substring(0, group.Index), wText.properties);
                    contents.Add(before);
                }
                WNeutralCitation nc = new WNeutralCitation(group.Value, wText.properties);
                contents.Add(nc);
                if (wText.Text.Length > group.Index + group.Length) {
                    WText after = new WText(wText.Text.Substring(group.Index + group.Length), wText.properties);
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
