
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse {

class RemoveTrailingWhitespace : Enricher {

    private static bool IsWhitespace(IInline inline) {
        if (inline is WTab)
            return true;
        if (inline is WText wt)
            return string.IsNullOrWhiteSpace(wt.Text);
        return false;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        while (line.Count() > 0 && IsWhitespace(line.Last()))
            line = line.SkipLast(1);
        return line;
    }

}

}
