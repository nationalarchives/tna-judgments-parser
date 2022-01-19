
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse.UKSC {

class PartyEnricher : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> contents = new List<IBlock>(blocks.Count());
        IEnumerator<IBlock> enumerator = blocks.GetEnumerator();
        while (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            contents.Add(block);
            if (!IsJudgmentTitle(block))
                continue;
            if (!enumerator.MoveNext())
                break;
            while (enumerator.Current is ILine line && line.IsEmpty()) {
                contents.Add(line);
                if (!enumerator.MoveNext())
                    return blocks;
            }
            if (enumerator.Current is WLine title) {
                WLine enriched = Enrich(title);
                if (Object.ReferenceEquals(enriched, title))
                    break;
                contents.Add(enriched);
                while (enumerator.MoveNext())
                    contents.Add(enumerator.Current);
                return contents;
            } else if (enumerator.Current is WTable table) {
                WTable enriched = EnrichTable(table);
                if (Object.ReferenceEquals(enriched, table))
                    break;
                contents.Add(enriched);
                while (enumerator.MoveNext())
                    contents.Add(enumerator.Current);
                return contents;
            } else {
                return blocks;
            }
        }
        return blocks;
    }

    private bool IsJudgmentTitle(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not WText text)
            return false;
        return text.Text.Trim() == "JUDGMENT";
    }

    protected override WLine Enrich(WLine line) {
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (Object.ReferenceEquals(enriched, line.Contents))
            return line;
        return new WLine(line, enriched);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        List<IInline> contents = new List<IInline>(line.Count());
        IEnumerator<IInline> enumerator = line.GetEnumerator();
        if (!enumerator.MoveNext())
            return line;
        IInline first = enumerator.Current;
        if (first is not WText text1)
            return line;
        Match match1 = Regex.Match(text1.Text, @"^([A-Z][A-Za-z ’]*?) \(([A-Z][a-z]+)\) $");
        if (!match1.Success)
            return line;
        PartyRole? role1 = ParseRole(match1.Groups[2].Value);
        if (!role1.HasValue)
            return line;
        contents.Add(new WParty(match1.Groups[1].Value, text1.properties) { Role = role1 });
        contents.Add(new WText(" (", text1.properties));
        contents.Add(new WRole() { Contents = new List<IInline>(1) { new WText(match1.Groups[2].Value, text1.properties) }, Role = role1.Value });
        contents.Add(new WText(") ", text1.properties));

        if (!enumerator.MoveNext())
            return line;
        IInline next = enumerator.Current;
        if (next is not WText text2)
            return line;
        if (text2.Text != "v")
            return line;
        contents.Add(next);

        if (!enumerator.MoveNext())
            return line;
        IInline third = enumerator.Current;
        if (third is not WText text3)
            return line;
        Match match2 = Regex.Match(text3.Text, @"^ ([A-Z0-9][A-Za-z0-9, ]*?) \(([A-Z][a-z]+)\)");
        if (!match2.Success)
            return line;
        PartyRole? role2 = ParseRole(match2.Groups[2].Value);
        if (!role2.HasValue)
            return line;
        contents.Add(new WText(" ", text3.properties));
        contents.Add(new WParty(match2.Groups[1].Value, text3.properties) { Role = role2 });
        contents.Add(new WText(" (", text3.properties));
        contents.Add(new WRole() { Contents = new List<IInline>(1) { new WText(match2.Groups[2].Value, text3.properties) }, Role = role2.Value });
        contents.Add(new WText(text3.Text.Substring(match2.Groups[2].Index + match2.Groups[2].Length), text3.properties));

        // if (!enumerator.MoveNext())
        //     return line;
        // IInline fourth = enumerator.Current;
        // if (third is not WText text3)
        //     return line;
        // if (fourth is not WText text4)
        //     return line;
        // Match match2 = Regex.Match(text.Text, @"^ \(([A-Za-z])\)$");
        // if (!match2.Success)
        //     return line;
        // PartyRole? role2 = ParseRole(match2.Groups[1].Value);
        // if (!role2.HasValue)
        //     return line;
        // contents.Add(new WParty(text3) { Role = role2 });
        // contents.Add(new WText(" (", text.properties));
        // contents.Add(new WRole() { Contents = new List<IInline>(1) { new WText(match2.Groups[1].Value, text.properties) }, Role = role1.Value });
        // contents.Add(new WText(")", text.properties));
        
        if (enumerator.MoveNext())
            return line;
        return contents;
    }

    private PartyRole? ParseRole(string text) {
        switch (text) {
            case "Appellant":
                return PartyRole.Appellant;
            case "Applicant":
                return PartyRole.Applicant;
            case "Respondent":
                return PartyRole.Respondent;
        }
        return null;
    }

    private WTable EnrichTable(WTable table) {
        if (!table.TypedRows.Any())
            return table;
        WRow row1 = table.TypedRows.First();
        if (row1.TypedCells.Count() != 1)
            return table;
        WCell cell = row1.TypedCells.First();
        if (!cell.Contents.Any())
            return table;
        IBlock block = cell.Contents.First();
        if (block is not WLine line)
            return table;
        WLine enriched = Enrich(line);
        if (Object.ReferenceEquals(enriched, line.Contents))
            return table;
        return new WTable(table.Main, table.Properties, table.Grid, table.TypedRows.Skip(1).Prepend(
            new WRow(row1.Table, row1.TypedCells.Skip(1).Prepend(
                new WCell(cell.Row, cell.Contents.Skip(1).Prepend(enriched))
            ))
        ));
    }

}

}
