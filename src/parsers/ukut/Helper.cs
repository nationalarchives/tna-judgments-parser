
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers {

class Helper {

    internal static List<IInline> SplitOnGroup(WText wText, Group group, Func<string, RunProperties, IInline> constructor) {
        List<IInline> enriched = new List<IInline>(3);
        if (group.Index > 0) {
            WText leading = new WText(wText.Text.Substring(0, group.Index), wText.properties);
            enriched.Add(leading);
        }
        IInline middle = constructor(group.Value, wText.properties);
        enriched.Add(middle);
        if (group.Index + group.Length < wText.Text.Length) {
            WText trailing = new WText(wText.Text.Substring(group.Index + group.Length), wText.properties);
            enriched.Add(trailing);
        }
        return enriched;
    }

}

}
