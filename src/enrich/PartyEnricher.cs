
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class PartyEnricher : Enricher {

    delegate IFormattedText Wrapper(string text, RunProperties props);

    private static IInline[] Split(WText text, Group group, Wrapper wrapper) {
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

    internal override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (line.Count() != 3)
            return line;
        IInline first = line.First();
        if (first is not WText text1)
            return line;
        if (!Regex.IsMatch(text1.Text, @"^\s*(Claimant|Respondent)\:?\s*$"))
            return line;
        IInline second = line.Skip(1).First();
        if (second is not WTab)
            return line;
        IInline third = line.Last();
        if (third is not WText text3)
            return line;
        return new List<IInline>(3) { first, second, new WParty(text3) };
    }

}

}
