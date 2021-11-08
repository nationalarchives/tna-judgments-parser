
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse.UKSC {

class SealRemover : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return blocks;
        IBlock block = blocks.First();
        if (block is not WLine line)
            return blocks;
        if (!line.Contents.Any())
            return blocks;
        IInline inline = line.Contents.First();
        if (inline is not WImageRef image)
            return blocks;
        line = new WLine(line, line.Contents.Skip(1));
        return blocks.Skip(1).Prepend(line);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

}

}
