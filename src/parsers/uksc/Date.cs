
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

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
            if (IsLineBeforeDate(block)) {
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

    private static bool IsLineBeforeDate(IBlock block) {
        if (block is not ILine line)
            return false;
        string normalized = line.NormalizedContent();
        ISet<string> ok = new HashSet<string>() { "JUDGMENT GIVEN ON", "JUDGMENT DELIVERED ON", "ON" };
        return ok.Contains(normalized);
    }

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
        IEnumerable<IBlock> contents = cell.Contents.Where(block => !block.IsEmptyLine());
        if (!contents.Any())
            return false;
        IBlock block = contents.Last();
        return IsLineBeforeDate(block);
    }

    private static bool IsEmptyRow(WRow row) {
        return row.Cells.All(cell => cell.Contents.All(block => block is ILine line && line.IsEmpty()));
    }

    private static WRow EnrichRow(WRow row) {
        if (row.Cells.Count() != 1)
            return row;
        WCell cell = row.TypedCells.First();
        IEnumerable<IBlock> contents = cell.Contents.Where(block => !block.IsEmptyLine());
        if (contents.Count() != 1)
            return row;

        IBlock block = contents.First();
        if (block is not WLine line)
            return row;
        WLine enriched = EnrichLine(line);
        if (Object.ReferenceEquals(enriched, line))
            return row;

        IEnumerable<IBlock> newContents = cell.Contents.Select(block => {
            if (block.IsEmptyLine())
                return block;
            if (block is not WLine line)
                return block;
            return EnrichLine(line);
        });

        return new WRow(row.Table, row.TablePropertyExceptions, row.Properties, new List<WCell>(1) {
            new WCell(row, cell.Props, newContents)
        });
    }

    private static WLine EnrichLine(WLine line) {
        if (line.Contents.Count() == 1)
            return EnrichLine1(line);
        if (line.Contents.Count() >= 3)
            return EnrichLine3(line);
        return line;
    }

    private static WLine EnrichLine1(WLine line) {
        if (line.Contents.Count() != 1)
            return line;
        IInline inline = line.Contents.First();
        if (inline is not WText text)
            return line;
        DateTime dt;
        try {
            dt = BetterParse(text.Text);
        } catch (FormatException) {
            return line;
        }
        return new WLine(line, new List<IInline>(1) { new WDocDate(text, dt) });
    }

    private static WLine EnrichLine3(WLine line) {
        if (line.Contents.Count() < 3)
            return line;
        IInline inline1 = line.Contents.ElementAt(0);
        IInline inline2 = line.Contents.ElementAt(1);
        IInline inline3 = line.Contents.ElementAt(2);
        if (inline1 is not WText text1)
            return line;
        if (inline2 is not WText text2)
            return line;
        if (inline3 is not WText text3)
            return line;
        // [2014] UKPC 28
        if (!line.Contents.Skip(3).All(inline => inline is IFormattedText text && string.IsNullOrWhiteSpace(text.Text)))
            return line;
        Match match1 = Regex.Match(text1.Text.Trim(), @"^((Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday),? )?(\d+)$");
        Match match2 = Regex.Match(text2.Text.Trim(), @"^(st|nd|rd|th)$");
        Match match3 = Regex.Match(text3.Text.Trim(), @"^(January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$");
        if (!match1.Success)
            return line;
        if (!match2.Success)
            return line;
        if (!match3.Success)
            return line;
        string toParse = text1.Text + " " + text3.Text;
        DateTime dt;
        try {
            dt = DateTime.Parse(toParse, culture);
        } catch (FormatException) {
            return line;
        }
        WDocDate docDate = new WDocDate(line.Contents.Cast<WText>(), dt);
        IEnumerable<IInline> contents = line.Contents.Skip(3).Prepend(docDate);
        return new WLine(line, contents);

    }

    private static DateTime BetterParse(string s) {
        s = Regex.Replace(s, @"\s+", " ").Trim();
        try {
            return DateTime.Parse(s, culture);
        } catch (FormatException) {
        }
        Match match = Regex.Match(s, @"^\d+(st|nd|rd|th) [A-Z][a-z]+ \d{4}$");
        Group group = match.Groups[1];
        string s2 = s.Substring(0, group.Index) + s.Substring(group.Index + group.Length);
        return DateTime.Parse(s2, culture);
    }

}

}
