
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse {

class RemoveTrailingWhitespace : Enricher {

    public IBlock Enrich1(IBlock block) {
        return base.Enrich(block);
    }

    private static bool IsWhitespace(IInline inline) {
        if (inline is WTab)
            return true;
        if (inline is WLineBreak)
            return true;
        if (inline is WText wt)
            return string.IsNullOrWhiteSpace(wt.Text);
        return false;
    }

    internal static IEnumerable<IInline> Remove(IEnumerable<IInline> line) {
        while (line.Any() && IsWhitespace(line.Last()))
            line = line.SkipLast(1);
        return line;
    }
    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        return Remove(line);
    }

}

}
