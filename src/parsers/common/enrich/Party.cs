
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class PartyEnricher : Enricher {

    delegate IFormattedText Wrapper(string text, RunProperties props);

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        bool representation = false;
        foreach (IBlock block in blocks) {
            if (!(block is ILine line)) {
                enriched.Add(block);
                continue;
            }
            representation = representation || NormalizeLine(line.Contents).Contains("Representation");
            if (representation)
                enriched.Add(block);
            else
                enriched.Add(Enrich(block));
        }
        return enriched;
    }


    static IInline[] Split(WText text, Group group, Wrapper wrapper) {
        string before = text.Text.Substring(0, group.Index);
        string during = group.Value;
        string after = text.Text.Substring(group.Index + group.Length);
        IInline[] replacement = {
            new WText(before, text.properties),
            wrapper(during, text.properties)
        };
        if (!string.IsNullOrEmpty(after))
            replacement[2] = new WText(after, text.properties);
        return replacement;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        while (line.Count() > 0 && line.Last() is WTab)
            line = line.SkipLast(1);
        if (line.Count() < 3)
            return line;
        IInline first = line.First();
        if (first is not WText text1)
            return line;
        if (!Regex.IsMatch(text1.Text, @"^\s*(Claimant|Respondent)\:?\s*$"))
            return line;
        IEnumerable<IInline> middle = line.Skip(1).Take(line.Count() - 2);
        if (!middle.All(i => i is WTab))
            return line;
        IInline last = line.Last();
        if (last is not WText text3)
            return line;
        return middle.Prepend(first).Append(new WParty(text3));
    }

}

}
