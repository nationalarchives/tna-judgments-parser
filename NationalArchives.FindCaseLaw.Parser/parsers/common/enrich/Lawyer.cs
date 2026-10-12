
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class LawyerEnricher : Enricher {

    delegate IFormattedText Wrapper(string text, RunProperties props);

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        bool representation = false;
        foreach (IBlock block in blocks) {
            if (!(block is WLine line)) {
                enriched.Add(block);
                continue;
            }
            representation = representation || line.TextContent.Contains("Representation");
            if (representation)
                enriched.Add(Enrich(block));
            else
                enriched.Add(block);
        }
        return enriched;
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
        return middle.Prepend(first).Append(new WLawyer(text3));
    }

}

}
