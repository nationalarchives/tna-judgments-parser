
using System;
using System.Collections.Generic;
using System.Linq;

namespace UK.Gov.Legislation.Judgments.Parse {

class RestrictionsEnricher : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return blocks;
        List<IBlock> contents = new List<IBlock>();
        IEnumerator<IBlock> enumerator = blocks.GetEnumerator();
        if (!enumerator.MoveNext())
            return blocks;
        IBlock block = enumerator.Current;
        if (IsTableWithRestriction(block))
            return blocks.Skip(1).Prepend(EnrichTableWithRestriction(block));
        if (!IsRestriction(block))
            return blocks;
        WRestriction restriction = new WRestriction((WLine) block);
        contents.Add(restriction);
        while (enumerator.MoveNext()) {
            block = enumerator.Current;
            if (!IsRestriction(block)) {
                contents.Add(block);
                break;
            }
            restriction = new WRestriction((WLine) block);
            contents.Add(restriction);
        }
        while (enumerator.MoveNext())
            contents.Add(enumerator.Current);
        return contents;
    }

    private static bool IsRestriction(IBlock block) {
        if (block is not ILine line)
            return false;
        string content = line.NormalizedContent();
        if (content.StartsWith("If this Transcript is to be reported or published, there is a requirement to "))
            return true;
        if (content.StartsWith("WARNING: reporting restrictions may apply to the contents transcribed in this document"))
            return true;
        string color = line.GetCSSStyles().GetValueOrDefault("color");
        if (color is not null && color == "red")
            return true;
        if (color is not null && color.ToLower() == "#ff0000")
            return true;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not IFormattedText text)
            return false;
        color = text.GetCSSStyles().GetValueOrDefault("color");
        if (color is not null && color == "red")
            return true;
        if (color is not null && color.ToLower() == "#ff0000")
            return true;
        return false;
    }

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
        return new WTable(table.Main, table.Properties, table.TypedRows.Skip(1).Prepend(
            new WRow(firstRow.Table, firstRow.TypedCells.Skip(1).Prepend(
                new WCell(firstCell.Row, firstCell.Contents.Skip(1).Prepend(restriction))
            ))
        ));
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new NotImplementedException();
    }

}

}
