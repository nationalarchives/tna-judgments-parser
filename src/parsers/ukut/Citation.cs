using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

class Citation : FirstMatch2 {

    internal Citation() {
        Limit = 10;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (!line.Any())
            return line;
        if (line.Last() is WText last) {
            var match = RegexHelpers.GetFirstMatch(last.Text,
                $@"(\[?\d{{4}}[\]\[] UKUT \d+ *\(({Courts.UpperTribunalChamberCodesPattern})\)) *$",
                $@"(\[?\d{{4}}[\]\[] UKFTT \d+ *\(({Courts.FirstTierTribunalChamberCodesPattern})\)) *$",
                @"(\[?\d{4}\]? UKAIT \d+) *$"
            );

            if (match.Success) {
                List<IInline> enriched = Helper.SplitOnGroup(last, match.Groups[1], (text, props) => new WNeutralCitation(text, props));
                return Enumerable.Concat(line.SkipLast(1), enriched);
            }

            if (last.Text == "." && line.SkipLast(1).Any() && line.SkipLast(1).Last() is WText penult) {  // [2023] UKFTT 00004 (GRC)
                var match2 = RegexHelpers.GetFirstMatch(penult.Text,
                    $@"(\[?\d{{4}}[\]\[] UKUT \d+ *\(({Courts.UpperTribunalChamberCodesPattern})\)) *$",
                    $@"(\[?\d{{4}}[\]\[] UKFTT \d+ *\(({Courts.FirstTierTribunalChamberCodesPattern})\)) *$",
                    @"(\[?\d{4}\]? UKAIT \d+) *$"
                );

                if (match2.Success) {
                    List<IInline> enriched = Helper.SplitOnGroup(penult, match2.Groups[1], (text, props) => new WNeutralCitation(text, props));
                    return Enumerable.Concat(line.SkipLast(2), enriched).Append(last);
                }
            }
        }
        if (line.First() is WText first) {
            var match = RegexHelpers.GetFirstMatch(first.Text,
                $@"^Neutral [Cc]itation [Nn]umber: (\[\d{{4}}\] UKUT \d+ \(({Courts.UpperTribunalChamberCodesPattern})\))",
                $@"^UT Neutral [Cc]itation [Nn]umber: (\[\d{{4}}\] UKUT \d+ \(({Courts.UpperTribunalChamberCodesPattern})\))",
                $@"^NCN: (\[\d{{4}}\] UKUT \d+ \(({Courts.UpperTribunalChamberCodesPattern})\))",
                $@"^NCN:? (\[\d{{4}}\] UKFTT \d+ \(({Courts.FirstTierTribunalChamberCodesPattern})\))",
                $@"^ *(\[\d{{4}}\] UKUT \d+ \(({Courts.UpperTribunalChamberCodesPattern})\))");  // [2023] UKUT 168 (LC)

            if (match.Success) {
                List<IInline> enriched = Helper.SplitOnGroup(first, match.Groups[1], (text, props) => new WNeutralCitation(text, props));
                return Enumerable.Concat(enriched, line.Skip(1));
            }

            if (line.Skip(1).Any() && line.Skip(1).All(i => i is WText)) {
                string all = IInline.ToString(line);
                match = Regex.Match(first.Text, @"^(Neutral Citation: )\[\d{4}\]");
                bool match2 = Regex.IsMatch(all, $@"^Neutral Citation: \[\d{{4}}\] UKUT \d+ \(({Courts.UpperTribunalChamberCodesPattern})\)$");
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
