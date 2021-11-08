
using System;
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse {

class RestrictionsEnricher : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return blocks;
        List<IBlock> contents = new List<IBlock>();
        IEnumerator<IBlock> enumerator = blocks.GetEnumerator();
        if (!enumerator.MoveNext())
            return blocks;
        IBlock block = enumerator.Current;
        if (!IsRestriction(block))
            return blocks;
        WRestriction restriction = new WRestriction((WLine) block);
        contents.Add(restriction);
        while (enumerator.MoveNext()) {
            block = enumerator.Current;
            if (!IsRestriction(block)) {
                contents.Add(block);
                break;
            }
            restriction = new WRestriction((WLine) block);
            contents.Add(restriction);
        }
        while (enumerator.MoveNext())
            contents.Add(enumerator.Current);
        return contents;
    }

    private static bool IsRestriction(IBlock block) {
        if (block is not ILine line)
            return false;
        string color = line.GetCSSStyles().GetValueOrDefault("color");
        if (color is not null && color == "red")
            return true;
        if (color is not null && color.ToLower() == "#ff0000")
            return true;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not IFormattedText text)
            return false;
        color = text.GetCSSStyles().GetValueOrDefault("color");
        if (color is not null && color == "red")
            return true;
        if (color is not null && color.ToLower() == "#ff0000")
            return true;
        return false;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

}

}
