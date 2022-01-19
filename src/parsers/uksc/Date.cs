
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
        if (block is WTable table)
            return EnrichTable(table);
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

    private WTable EnrichTable(WTable table) {
        List<WRow> newRows = new List<WRow>(table.TypedRows.Count());
        IEnumerator<WRow> enumerator = table.TypedRows.GetEnumerator();
        while (enumerator.MoveNext()) {
            WRow row = enumerator.Current;
            newRows.Add(row);
            if (!IsTheJudgmentGivenOnRow(row))
                continue;
            if (!enumerator.MoveNext())
                break;
            while (IsEmptyRow(enumerator.Current)) {
                newRows.Add(enumerator.Current);
                if (!enumerator.MoveNext())
                    return table;
            }
            WRow enriched = EnrichRow(enumerator.Current);
            if (Object.ReferenceEquals(enriched, enumerator.Current))
                break;
            newRows.Add(enriched);
            while (enumerator.MoveNext())
                newRows.Add(enumerator.Current);
            return new WTable(table.Main, table.Properties, table.Grid, newRows);
        }
        return table;
    }

    private bool IsTheJudgmentGivenOnRow(WRow row) {
        if (row.Cells.Count() != 1)
            return false;
        WCell cell = row.TypedCells.First();
        if (cell.Contents.Count() != 1)
            return false;
        IBlock block = cell.Contents.First();
        if (block is not ILine line)
            return false;
        ISet<string> ok = new HashSet<string>() { "JUDGMENT GIVEN ON", "ON" };
        return ok.Contains(line.NormalizedContent());
    }

    private static bool IsEmptyRow(WRow row) {
        return row.Cells.All(cell => cell.Contents.All(block => block is ILine line && line.IsEmpty()));
    }

    private static WRow EnrichRow(WRow row) {
        if (row.Cells.Count() != 1)
            return row;
        WCell cell = row.TypedCells.First();
        if (cell.Contents.Count() != 1)
            return row;
        IBlock block = cell.Contents.First();
        if (block is not WLine line)
            return row;
        if (line.Contents.Count() != 1)
            return row;
        IInline inline = line.Contents.First();
        if (inline is not WText text)
            return row;
        DateTime dt;
        try {
            dt = DateTime.Parse(text.Text, culture);
        } catch (FormatException) {
            return row;
        }
        return new WRow(row.Table, new List<WCell>(1) {
            new WCell(row, new List<IBlock>(1) {
                new WLine(line, new List<IInline>(1) {
                    new WDocDate(text, dt)
                })
            })
        });
    }

}

}
