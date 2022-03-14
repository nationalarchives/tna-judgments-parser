
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class Citation : FirstMatch2 {

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (!line.Any())
            return line;
        if (line.Last() is not WText wText)
            return line;
        Match match = Regex.Match(wText.Text, @"(\[?\d{4}[\]\[] UKUT \d+ ?\((AAC|IAC|LC|TCC)\)) *$");
        if (!match.Success)
            return line;
        List<IInline> enriched = Helper.SplitOnGroup(wText, match.Groups[1], (text, props) => new WNeutralCitation(text, props));
        return Enumerable.Concat(line.SkipLast(1), enriched);
    }

}

}
