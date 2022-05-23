
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class Citation : FirstMatch2 {

    internal Citation() {
        Limit = 10;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (!line.Any())
            return line;
        if (line.Last() is WText last) {
            Match match = Regex.Match(last.Text, @"(\[?\d{4}[\]\[] UKUT \d+ ?\((AAC|IAC|LC|TCC)\)) *$");
            if (!match.Success)
                match = Regex.Match(last.Text, @"(\[?\d{4}[\]\[] UKFTT \d+ ?\((TC)\)) *$");
            if (match.Success) {
                List<IInline> enriched = Helper.SplitOnGroup(last, match.Groups[1], (text, props) => new WNeutralCitation(text, props));
                return Enumerable.Concat(line.SkipLast(1), enriched);
            }
        }
        if (line.First() is WText first) {
            Match match = Regex.Match(first.Text, @"^Neutral [Cc]itation [Nn]umber: (\[\d{4}\] UKUT \d+ \((AAC|IAC|LC|TCC)\))");
            if (!match.Success)
                match = Regex.Match(first.Text, @"UT Neutral [Cc]itation [Nn]umber: (\[\d{4}\] UKUT \d+ \((AAC|IAC|LC|TCC)\))");
            if (match.Success) {
                List<IInline> enriched = Helper.SplitOnGroup(first, match.Groups[1], (text, props) => new WNeutralCitation(text, props));
                return Enumerable.Concat(enriched, line.Skip(1));
            }
        }
        return line;
    }

}

}
