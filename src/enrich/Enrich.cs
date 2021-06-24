
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class Enricher {

    internal IEnumerable<IDecision> Enrich(IEnumerable<IDecision> body) {
        return body.Select(d => new Decision { Author = (d.Author is null) ? null : Enrich((WLine) d.Author), Contents = Enrich(d.Contents) });
    }

    internal IEnumerable<IDivision> Enrich(IEnumerable<IDivision> divs) {
        return divs.Select(div => Enrich(div));
    }
    internal IDivision Enrich(IDivision div) {
        if (div is BigLevel big)
            return new BigLevel() { Number = big.Number, Heading = big.Heading, Children = Enrich(big.Children) };
        if (div is CrossHeading xhead)
            return new CrossHeading() { Heading = Enrich((WLine) xhead.Heading), Children = Enrich(xhead.Children) };
        if (div is GroupOfParagraphs group)
            return new GroupOfParagraphs() { Children = Enrich(group.Children) };
        if (div is WNewNumberedParagraph np)
            return new WNewNumberedParagraph(np.Number, Enrich(np.Contents));
        if (div is WDummyDivision dummy)
            return new WDummyDivision(Enrich(dummy.Contents));
        throw new Exception();
    }

    internal IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) => blocks.Select(Enrich);

    internal IBlock Enrich(IBlock block) {
        if (block is WOldNumberedParagraph np)
            return new WOldNumberedParagraph(np.Number, Enrich(np));
        if (block is WLine line)
            return Enrich(line);
        return block;
    }

    internal WLine Enrich(WLine line) {
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        return new WLine(line, enriched);
    }

    abstract internal IEnumerable<IInline> Enrich(IEnumerable<IInline> line);

    internal IEnumerable<IAnnex> Enrich(IEnumerable<IAnnex> annexes) {
        return annexes.Select(a => new Annex { Number = a.Number, Contents = Enrich(a.Contents) });
    }

    // protected string NormalizeInnerText(OpenXmlElement e) {
    //     IEnumerable<string> texts = e.Descendants()
    //         .Where(e => e is Text || e is TabChar)
    //         .Select(e => { if (e is Text) return e.InnerText; if (e is TabChar) return " "; return ""; });
    //     return string.Join("", texts).Trim();
    // }
    protected string NormalizeLine(IEnumerable<IInline> line) {
        IEnumerable<string> texts = line
            .Select(i => { if (i is IFormattedText t) return t.Text; if (i is TabChar) return " "; return ""; });
        return string.Join("", texts).Trim();
    }

}

}