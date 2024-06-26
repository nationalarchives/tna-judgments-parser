
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse {

partial class RestrictionsEnricher : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return blocks;
        List<IBlock> contents = new List<IBlock>();
        IEnumerator<IBlock> enumerator = blocks.GetEnumerator();
        bool found = false;
        bool skipped = false;
        while (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            if (IsRestriction(block)) {
                found = true;
                WRestriction restriction = new WRestriction((WLine) block);
                contents.Add(restriction);
                continue;
            }
            if (!skipped && IsTopRestriction(block)) {
                found = true;
                WRestriction restriction = new WRestriction((WLine) block);
                contents.Add(restriction);
                continue;
            }
            if (IsTableWithRestriction(block)) {
                found = true;
                var enriched = EnrichTableWithRestriction(block);
                contents.Add(enriched);
                continue;
            }
            contents.Add(block);
            skipped = true;
        }
        if (found)
            return contents;
        return blocks;
    }

    private static bool IsTopRestriction(IBlock block) {
        if (IsRestriction(block))
            return true;
        if (block is not WLine line)
            return false;
        if (line.Style == "CourtOrder")
            return true;
        string color = ((ILine) line).GetCSSStyles().GetValueOrDefault("color");
        if (IsRed(color))
            return true;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not IFormattedText text)
            return false;
        color = text.GetCSSStyles(null).GetValueOrDefault("color");
        if (IsRed(color))
            return true;
        string content = line.NormalizedContent;
        if (content == "IN CONFIDENCE")
            return true;
        if (content.StartsWith("This Judgment was delivered in private."))
            return true;
        return false;
    }

    private static bool IsRestriction(IBlock block) {
        if (block is not WLine line)
            return false;
        string content = line.NormalizedContent;
        if (content.StartsWith("If this Transcript is to be reported or published, there is a requirement to "))
            return true;
        if (content.StartsWith("WARNING: reporting restrictions may apply to the contents transcribed in this document"))
            return true;
        return false;
    }

    static bool IsRed(string color) {
        if (color is null)
            return false;
        if (color.Equals("red", StringComparison.OrdinalIgnoreCase))
            return true;
        return RedRegex().IsMatch(color);
    }

    [GeneratedRegex("^[1-9A-F][0-9A-F]0{4}$", RegexOptions.IgnoreCase)]
    private static partial Regex RedRegex();


    private static bool IsTableWithRestriction(IBlock block) {
        if (block is not WTable table)
            return false;
        IBlock first = table.Rows.FirstOrDefault()?.Cells.FirstOrDefault()?.Contents.FirstOrDefault();
        return IsRestriction(first);
    }
    private static ITable EnrichTableWithRestriction(IBlock block) {
        WTable table = (WTable) block;
        WRow firstRow = table.TypedRows.First();
        WCell firstCell = firstRow.TypedCells.First();
        WLine firstLine = (WLine) firstCell.Contents.First();
        WRestriction restriction = new WRestriction(firstLine);
        return new WTable(table.Main, table.Properties, table.Grid, table.TypedRows.Skip(1).Prepend(
            new WRow(firstRow.Table, firstRow.TablePropertyExceptions, firstRow.Properties, firstRow.TypedCells.Skip(1).Prepend(
                new WCell(firstCell.Row, firstCell.Props, firstCell.Contents.Skip(1).Prepend(restriction))
            ))
        ));
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

}

}
