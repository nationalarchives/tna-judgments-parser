
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace UK.Gov.Legislation.Judgments.Parse {

// class Merge {

    // static IEnumerable<IInline> RemoveTabs(IEnumerable<IInline> unfiltered) {
    //     List<IInline> filtered = new List<IInline>(unfiltered.Count());
    //     IInline last = null;
    // //     int indent = 0;
    //     foreach (IInline next in unfiltered) {
    //         if (next is WTab && last is WText text) {
    //             text.MinWidthInches = 1;
    //         } else {
    //             filtered.Add(next);
    //             last = next;
    //         }
    //     }
    //     return filtered;
    // }

    // static IEnumerable<IInline> MergeRuns(IEnumerable<IInline> unmerged) {
    //     List<IInline> merged = new List<IInline>(unmerged.Count());
    //     IInline last;
    //     for (int i = 0; i < unmerged.Count(); i++) {
    //         IInline next = unmerged.ElementAt(i);
    //     }
    //     IInline last = unmerged.FirstOrDefault();
    //     if (last is null)
    //         return unmerged;
    //     foreach (IInline next in unmerged.Skip(1)) {
    //         if (last is WText fText1 && next is WText fText2 && IFormattedText.HaveSameFormatting(fText1, fText2)) {
    //             last = new WText(fText1.Text + fText2.Text, fText1.properties);
    //         } else if (next is WTab) {
    //         } else {
    //             merged.Add(last);
    //             last = next;
    //         }
    //     }
    //     merged.Add(last);
    //     return merged;
    // }

// }

}
