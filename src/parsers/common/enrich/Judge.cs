
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class Judge : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        while (blocks.Any()) {
            List<IBlock> magic = Magic(blocks);
            if (magic is not null) {
                enriched.AddRange(magic);
                enriched.AddRange(blocks.Skip(magic.Count));
                break;
            }
            enriched.Add(blocks.First());
            blocks = blocks.Skip(1);
        }
        return enriched;
    }

    private List<IBlock> Magic(IEnumerable<IBlock> blocks) {
        blocks = blocks.Take(6);
        if (!blocks.Any())
            return null;
        IBlock block = blocks.First();
        if (block is not ILine first)
            return null;
        ISet<string> starts = new HashSet<string> { "Before:", "Before :", "B e f o r e :", "B E F O R E:" };
        if (!starts.Contains(first.NormalizedContent()))
            return null;
        List<IBlock> enriched = new List<IBlock>();
        enriched.Add(block);
        blocks = blocks.Skip(1);
        // if (!blocks.Any())
        //     return null;
        bool found = false;
        while (blocks.Any()) {
            block = blocks.First();
            if (block is not WLine line)
                return null;
            if (line.Contents.Count() != 1) {
                enriched.Add(block);
                blocks = blocks.Skip(1);
                continue;
            }
            IInline inline = line.Contents.First();
            if (inline is not WText text) {
                enriched.Add(block);
                blocks = blocks.Skip(1);
                continue;
            }
            if (IsAJudgeName(text)) {
                found = true;
                WJudge judge = new WJudge(text.Text, text.properties);
                WLine line2 = new WLine(line, new List<IInline>(1) { judge });
                enriched.Add(new WLine(line2));
                blocks = blocks.Skip(1);
                continue;
            }
            if (Regex.IsMatch(text.Text, @"^ ?-( -)+ ?$"))
                return found ? enriched : null;
            enriched.Add(block);
            blocks = blocks.Skip(1);
        }
        return null;
    }

    private bool IsAJudgeName(WText text) {
        ISet<string> starts = new HashSet<string> { "LORD JUSTICE ", "MR JUSTICE ", "MR. JUSTICE ", "HIS HONOUR JUDGE " };
        foreach (string start in starts)
            if (text.Text.StartsWith(start))
                return true;
        return false;
    }

    // protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
    //     while (line.Count() > 0 && line.Last() is WTab)
    //         line = line.SkipLast(1);
    //     if (line.Count() < 3)
    //         return line;
    //     IInline first = line.First();
    //     if (first is not WText text1)
    //         return line;
    //     if (!Regex.IsMatch(text1.Text, @"^\s*Before\:?\s*$"))
    //         return line;
    //     IEnumerable<IInline> middle = line.Skip(1).Take(line.Count() - 2);
    //     if (!middle.All(i => i is WTab))
    //         return line;
    //     IInline last = line.Last();
    //     if (last is not WText text3)
    //         return line;
    //     if (Regex.IsMatch(text3.Text, @"^\s*Employment Judge\s"))
    //         return middle.Prepend(first).Append(new WJudge(text3));
    //     if (Regex.IsMatch(text3.Text, @"^\s*Judge\s"))
    //         return middle.Prepend(first).Append(new WJudge(text3));
    //     return line;
    // }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new System.NotImplementedException();
    }

}

}
