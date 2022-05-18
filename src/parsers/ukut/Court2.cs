
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class CourtType2 : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (Util.Descendants<WCourtType>(blocks).Any())
            return blocks;
        if (Util.Descendants<WCourtType2>(blocks).Any())
            return blocks;
        WNeutralCitation nc = Util.Descendants<WNeutralCitation>(blocks).FirstOrDefault();
        if (nc is null)
            return blocks;
        const int limit = 10;
        int i = 0;
        while (i < blocks.Count() && i < limit) {
            IBlock block1 = blocks.ElementAt(i);
            List<ILine> one = Match1(block1, nc);
            if (one is not null) {
                IEnumerable<IBlock> before = blocks.Take(i);
                IEnumerable<IBlock> after = blocks.Skip(i + 1);
                return Enumerable.Concat(Enumerable.Concat(before, one), after);
            }
            i += 1;
        }
        return blocks;
    }

    protected List<ILine> Match1(IBlock block, WNeutralCitation nc) {
        Court court;
        if (nc.Text.EndsWith("(IAC)"))
            court = Courts.UpperTribunal_ImmigrationAndAsylumChamber;
        else if (nc.Text.EndsWith("(LC)"))
            court = Courts.UpperTribunal_LandsChamber;
        else if (nc.Text.EndsWith("(TCC)"))
            court = Courts.UpperTribunal_TaxAndChanceryChamber;
        else
            return null;
        Combo1 combo = new Combo1 {
            Re = new Regex(@"^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
            Court = court
        };
        if (combo.Match(block))
            return combo.Transform(block);
        return null;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new System.NotImplementedException();
    }

}

}
