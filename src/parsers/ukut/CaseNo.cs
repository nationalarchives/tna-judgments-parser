
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class CaseNo : Enricher {

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (!line.Any())
            return line;
        IInline last = line.Last();
        if (last is not WText text)
            return line;
        Regex re = new Regex(@"^ *(TC\d{5}) *$");
        Match match = re.Match(text.Text);
        if (!match.Success)
            return line;
        List<IInline> caseNo = Helper.SplitOnGroup(text, match.Groups[1], (text, props) => new WCaseNo(text, props));
        return Enumerable.Concat(line.SkipLast(1), caseNo);
    }
}

}
