
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class Enricher {

    internal static IEnumerable<IDecision> Enrich(IEnumerable<IDecision> body, IEnumerable<Enricher> enrichers) {
        return enrichers.Aggregate(body, (done, enricher) => enricher.Enrich(done));
    }
    internal static IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks, IEnumerable<Enricher> enrichers) {
        return enrichers.Aggregate(blocks, (done, enricher) => enricher.Enrich(done));
    }
    internal static IEnumerable<IAnnex> Enrich(IEnumerable<IAnnex> annexes, IEnumerable<Enricher> enrichers) {
        return enrichers.Aggregate(annexes, (done, enricher) => enricher.Enrich(done));
    }



    internal virtual IEnumerable<IDecision> Enrich(IEnumerable<IDecision> body) {
        return body.Select(d => new Decision { Author = (d.Author is null) ? null : Enrich((WLine) d.Author), Contents = Enrich(d.Contents) });
    }

    internal IEnumerable<IDivision> Enrich(IEnumerable<IDivision> divs) {
        return divs.Select(div => Enrich(div));
    }
    internal virtual IDivision Enrich(IDivision div) {
        if (div is BigLevel big)
            return new BigLevel() { Number = big.Number, Heading = big.Heading, Children = Enrich(big.Children) };
        if (div is CrossHeading xhead)
            return new CrossHeading() { Heading = Enrich((WLine) xhead.Heading), Children = Enrich(xhead.Children) };
        if (div is GroupOfParagraphs group)
            return new GroupOfParagraphs() { Children = Enrich(group.Children) };
        if (div is WNewNumberedParagraph np)
            return new WNewNumberedParagraph(np.Number, Enrich(np.Contents));
        if (div is WTableOfContents toc)
            return toc;
        if (div is WDummyDivision dummy)
            return new WDummyDivision(Enrich(dummy.Contents));
        throw new Exception();
    }

    internal virtual IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) => blocks.Select(Enrich);

    protected virtual IBlock Enrich(IBlock block) {
        if (block is WOldNumberedParagraph np)
            return new WOldNumberedParagraph(np.Number, Enrich(np));
        if (block is WLine line)
            return Enrich(line);
        // if (block is WTable table)
        //     return EnrichTable(table);
        return block;
    }

    // protected WTable EnrichTable(WTable table) {
    //     IEnumerable<WRow> rows = table.TypedRows.Select(row => {
    //         IEnumerable
    //     });
    //     return new WTable(table.Main, rows);

    // }

    protected virtual WLine Enrich(WLine line) {
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (object.ReferenceEquals(enriched, line.Contents))
            return line;
        return new WLine(line, enriched);
    }

    abstract protected IEnumerable<IInline> Enrich(IEnumerable<IInline> line);

    internal IEnumerable<IAnnex> Enrich(IEnumerable<IAnnex> annexes) {
        return annexes.Select(a => new Annex { Number = a.Number, Contents = Enrich(a.Contents) });
    }

    // protected string NormalizeInnerText(OpenXmlElement e) {
    //     IEnumerable<string> texts = e.Descendants()
    //         .Where(e => e is Text || e is TabChar)
    //         .Select(e => { if (e is Text) return e.InnerText; if (e is TabChar) return " "; return ""; });
    //     return string.Join("", texts).Trim();
    // }

    internal static string NormalizeInlines(IEnumerable<IInline> line) {
        IEnumerable<string> texts = line
            .Select(i => { if (i is IFormattedText t) return t.Text; if (i is ITab) return " "; return ""; });
        return string.Join("", texts).Trim();
    }

    // protected string NormalizeLine(IEnumerable<IInline> line) {
    //     IEnumerable<string> texts = line
    //         .Select(i => { if (i is IFormattedText t) return t.Text; if (i is ITab) return " "; return ""; });
    //     return string.Join("", texts).Trim();
    // }
    internal static string NormalizeLine(ILine line) {
        return NormalizeInlines(line.Contents);
    }
    // public static string NormalizeContent(this ILine line) {
    //     return NormalizeInlines(line.Contents);
    // }

}

abstract class Enricher2 : Enricher {

    protected override IBlock Enrich(IBlock block) {
        if (block is WLine line)
            return EnrichLine(line);
        if (block is WTable table)
            return EnrichTable(table);
        return block;
    }

    protected WTable EnrichTable(WTable table) {
        IEnumerator<WRow> rows = table.TypedRows.GetEnumerator();
        List<WRow> enriched = new List<WRow>();
        while (rows.MoveNext()) {
            WRow before = rows.Current;
            WRow after = EnrichRow(before);
            enriched.Add(after);
            if (!object.ReferenceEquals(before, after)) {
                while (rows.MoveNext())
                    enriched.Add(rows.Current);
                return new WTable(table.Main, table.Properties, table.Grid, enriched);
            }
        }
        return table;
    }

    private WRow EnrichRow(WRow row) {
        IEnumerator<WCell> cells = row.TypedCells.GetEnumerator();
        List<WCell> enriched = new List<WCell>();
        while (cells.MoveNext()) {
            WCell before = cells.Current;
            WCell after = EnrichCell(before);
            enriched.Add(after);
            if (!object.ReferenceEquals(before, after)) {
                while (cells.MoveNext())
                    enriched.Add(cells.Current);
                return new WRow(row.Table, enriched);
            }
        }
        return row;
    }

    protected virtual WCell EnrichCell(WCell cell) {
        IEnumerator<IBlock> contents = cell.Contents.GetEnumerator();
        List<IBlock> enriched = new List<IBlock>();
        while (contents.MoveNext()) {
            IBlock before = contents.Current;
            IBlock after = Enrich(before);
            enriched.Add(after);
            if (!object.ReferenceEquals(before, after)) {
                while (contents.MoveNext())
                    enriched.Add(contents.Current);
                return new WCell(cell.Row, cell.Props, enriched);
            }
        }
        return cell;
    }

    private WLine EnrichLine(WLine line) {
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (object.ReferenceEquals(enriched, line.Contents))
            return line;
        return new WLine(line, enriched);
    }

}


}