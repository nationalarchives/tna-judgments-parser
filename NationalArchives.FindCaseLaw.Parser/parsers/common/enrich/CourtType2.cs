
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse {

class CourtType2 {

    internal static IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        WNeutralCitation nc = Util.Descendants<WNeutralCitation>(blocks).FirstOrDefault();
        if (nc is null)
            return blocks;
        const int limit = 10;
        int i = 0;
        while (i < blocks.Count() && i < limit) {
            IBlock block1 = blocks.ElementAt(i);
            if (i < blocks.Count() - 2) {
                IBlock block2 = blocks.ElementAt(i + 1);
                IBlock block3 = blocks.ElementAt(i + 2);
                List<WLine> three = Match3(block1, block2, block3, nc);
                if (three is not null) {
                    IEnumerable<IBlock> before = blocks.Take(i);
                    IEnumerable<IBlock> after = blocks.Skip(i + 3);
                    return Enumerable.Concat(Enumerable.Concat(before, three), after);
                }
            }
            i += 1;
        }
        return blocks;
    }

    private static bool Match(Regex regex, IBlock block) {
        if (!(block is WLine line))
            return false;
        if (line.Contents.Count() == 0)
            return false;
        IInline first = line.Contents.First();
        if (!line.Contents.All(inline => inline is WText))
            return false;
        string text = line.NormalizedContent;
        return regex.IsMatch(text);
    }

    static List<Func<IBlock, IBlock, IBlock, WNeutralCitation, List<WLine>>> functions = new List<Func<IBlock, IBlock, IBlock, WNeutralCitation, List<WLine>>>() {
        (one, two, three, nc) => {
            var re1 = new Regex(@"^IN THE HIGH COURT OF JUSTICE$", RegexOptions.IgnoreCase);
            var re2 = new Regex(@"^BUSINESS AND PROPERTY COURTS OF ENGLAND AND WALES$", RegexOptions.IgnoreCase);
            var re3 = new Regex(@"\d+(st|nd|rd|th)? (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$", RegexOptions.IgnoreCase);
            if (!Match(re1, one))
                return null;
            if (!Match(re2, two))
                return null;
            if (!Match(re3, three))
                return null;
            Court court;
            if (nc.Text.Contains("(Ch)"))
                court = Courts.EWHC_Chancery_BusinessAndProperty;
            else
                return null;
            return new List<WLine>(3) { Transform1(one, court), Transform1(two, court), (WLine) three };
        }
    };

    private static List<WLine> Match3(IBlock one, IBlock two, IBlock three, WNeutralCitation nc) {
        foreach (var function in functions) {
            var result = function(one, two, three, nc);
            if (result is not null)
                return result;
        }
        return null;
    }

    private static WLine Transform1(IBlock block, Court court) {
        WLine line = (WLine) block;
        if (line.Contents.Count() == 1) {
            WText text = (WText) line.Contents.First();
            WCourtType ct = new WCourtType(text.Text, text.properties) { Code = court.Code };
            return WLine.Make(line, new List<IInline>(1) { ct });
        } else {
            WCourtType2 ct = new WCourtType2() { Code = court.Code, Contents = line.Contents.Cast<WText>() };
            return WLine.Make(line, new List<IInline>(1) { ct });
        }
    }

}

}
