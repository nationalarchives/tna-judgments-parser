
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
                match = Regex.Match(last.Text, @"(\[?\d{4}[\]\[] UKFTT \d+ ?\((TC|GRC)\)) *$");
            if (match.Success) {
                List<IInline> enriched = Helper.SplitOnGroup(last, match.Groups[1], (text, props) => new WNeutralCitation(text, props));
                return Enumerable.Concat(line.SkipLast(1), enriched);
            }
        }
        if (line.First() is WText first) {
            Match match = Regex.Match(first.Text, @"^Neutral [Cc]itation [Nn]umber: (\[\d{4}\] UKUT \d+ \((AAC|IAC|LC|TCC)\))");
            if (!match.Success)
                match = Regex.Match(first.Text, @"^UT Neutral [Cc]itation [Nn]umber: (\[\d{4}\] UKUT \d+ \((AAC|IAC|LC|TCC)\))");
            if (!match.Success)
                match = Regex.Match(first.Text, @"^NCN: (\[\d{4}\] UKUT \d+ \((AAC|IAC|LC|TCC)\))");
            if (match.Success) {
                List<IInline> enriched = Helper.SplitOnGroup(first, match.Groups[1], (text, props) => new WNeutralCitation(text, props));
                return Enumerable.Concat(enriched, line.Skip(1));
            }
            if (line.Skip(1).Any() && line.Skip(1).All(i => i is WText)) {
                string all = ILine.TextContent(line);
                match = Regex.Match(first.Text, @"^(Neutral Citation: )\[\d{4}\]");
                bool match2 = Regex.IsMatch(all, @"^Neutral Citation: \[\d{4}\] UKUT \d+ \((AAC|IAC|LC|TCC)\)$");
                if (match.Success && match2) {
                    System.Tuple<WText, WText> split = first.Split(match.Groups[1].Length);
                    WNeutralCitation2 ncn = new WNeutralCitation2 { Contents = line.Skip(1).Cast<WText>().Prepend(split.Item2) };
                    return new List<IInline>(2) { split.Item1, ncn };
                }
            }
        }
        return line;
    }

}

}
