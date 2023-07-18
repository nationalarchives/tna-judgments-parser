
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Enrich {

[Obsolete]
class DocType {

    internal IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return blocks;
        IBlock first = blocks.First();
        IBlock enriched = EnrichBlock(first);
        if (object.ReferenceEquals(enriched, first))
            return blocks;
        return blocks.Skip(1).Prepend(enriched);
    }

    protected IBlock EnrichBlock(IBlock block) {
        if (block is WLine line)
            return EnrichLine(line);
        return block;
    }

    protected WLine EnrichLine(WLine line) {
        string text = line.NormalizedContent;
        if (!text.Equals("EXPLANATORY MEMORANDUM TO", StringComparison.InvariantCultureIgnoreCase))
            return line;
        Model.DocType2 docType = new Model.DocType2 { Contents = line.Contents };
        return WLine.Make(line, new List<IInline>(1) { docType });
    }

}

[Obsolete]
class DocNumber {

    internal IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return blocks;
        IBlock last = blocks.Last();
        IBlock enriched = EnrichBlock(last);
        if (object.ReferenceEquals(enriched, last))
            return blocks;
        return blocks.SkipLast(1).Append(enriched);
    }

    protected IBlock EnrichBlock(IBlock block) {
        if (block is WLine line)
            return EnrichLine(line);
        return block;
    }

    protected WLine EnrichLine(WLine line) {
        string text = line.NormalizedContent;
        if (!Regex.IsMatch(text, @"^\d{4} No\. \d+$"))
            return line;
        Model.DocNumber2 docNum = new Model.DocNumber2 { Contents = line.Contents };
        return WLine.Make(line, new List<IInline>(1) { docNum });
    }

}

}
