
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse {

class Merger : Enricher {

    public static IEnumerable<IInline> Merge(IEnumerable<IInline> unmerged) {
        if (unmerged.Count() <= 1)
            return unmerged;
        List<IInline> merged = new List<IInline>(unmerged.Count());
        IInline last = unmerged.First();
        foreach (IInline next in unmerged.Skip(1)) {
            if (last is WHyperlink1 || next is WHyperlink1) {
                merged.Add(last);
                last = next;
            } else if (last is WText fText1 && next is WText fText2 && IFormattedText.HaveSameFormatting(fText1, fText2)) {
                last = new WText(fText1.Text + fText2.Text, fText1.properties);
            // } else if (last is WTab && next is WTab) {
            //     continue;
            } else {
                merged.Add(last);
                last = next;
            }
        }
        merged.Add(last);
        return merged;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> unmerged) {
        return Merge(unmerged);
    }

}

}
