
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse.UKSC {

class DateEnricher : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> contents = new List<IBlock>(blocks.Count());
        IEnumerator<IBlock> enumerator = blocks.GetEnumerator();
        while (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            IBlock enriched = Enrich(block);
            contents.Add(enriched);
            if (!Object.ReferenceEquals(enriched, block)) {
                while (enumerator.MoveNext())
                    contents.Add(enumerator.Current);
                return contents;
            }
            if (block is ILine line1 && line1.NormalizedContent() == "JUDGMENT GIVEN ON") {
                if (enumerator.MoveNext()) {
                    IBlock next = enumerator.Current;
                    if (next is WLine line2 && line2.Contents.All(inline => inline is WText)) {
                        try {
                            DateTime dt = DateTime.Parse(((ILine) line2).NormalizedContent(), culture);
                            WDocDate dd = new WDocDate(line2.Contents.Cast<WText>(), dt);
                            WLine line3 = new WLine(line2, new List<IInline>(1) {dd});
                            contents.Add(line3);
                            while (enumerator.MoveNext())
                                contents.Add(enumerator.Current);
                            return contents;
                        } catch (FormatException) {
                        }
                    }
                    contents.Add(next);
                }
            }
        }
        return blocks;
    }
    // internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
    //     bool found = false;
    //     List<IBlock> contents = new List<IBlock>(blocks.Count());
    //     foreach (IBlock block in blocks) {
    //         if (found) {
    //             contents.Add(block);
    //             continue;
    //         }
    //         IBlock enriched = Enrich(block);
    //         found = !Object.ReferenceEquals(enriched, block);
    //         contents.Add(enriched);
    //     }
    //     if (found)
    //         return contents;
    //     return blocks;
    // }

    protected override IBlock Enrich(IBlock block) {
        if (block is WLine line)
            return Enrich(line);
        return block;
    }

    protected override WLine Enrich(WLine line) {
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (Object.ReferenceEquals(enriched, line.Contents))
            return line;
        return new WLine(line, enriched);
    }

    private static readonly CultureInfo culture = new CultureInfo("en-GB");

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        List<IInline> contents = new List<IInline>(line.Count());
        IEnumerator<IInline> enumerator = line.GetEnumerator();
        while (enumerator.MoveNext()) {
            IInline inline = enumerator.Current;
            if (inline is WText wText && wText.Text.Trim() == "JUDGMENT GIVEN ON") {
                if (enumerator.MoveNext()) {
                    IInline next1 = enumerator.Current;
                    if (next1 is WLineBreak br) {
                        if (enumerator.MoveNext()) {
                            IInline next2 = enumerator.Current;
                            if (next2 is WText date) {
                                try {
                                    DateTime dt = DateTime.Parse(date.Text, culture);
                                    WDocDate dd = new WDocDate(date, dt);
                                    contents.Add(inline);
                                    contents.Add(br);
                                    contents.Add(dd);
                                    while (enumerator.MoveNext())
                                        contents.Add(enumerator.Current);
                                    return contents;
                                } catch (FormatException) {
                                }
                            }
                        }
                    }
                }
            }
            contents.Add(inline);
        }
        return line;
    }

}

}
