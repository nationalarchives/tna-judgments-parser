
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class PartyEnricher : Enricher {

    delegate IFormattedText Wrapper(string text, RunProperties props);

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        // bool representation = false;
        foreach (IBlock block in blocks) {
            if (block is WTable table)
                enriched.Add(EnrichTable(table));
            else
                enriched.Add(block);
            // if (block is ILine line) {
            //     representation = representation || NormalizeLine(line.Contents).Contains("Representation");
            //     if (representation)
            //         enriched.Add(block);
            //     else
            //         enriched.Add(Enrich(block));
            // }
        }
        return enriched;
    }


    static IInline[] Split(WText text, Group group, Wrapper wrapper) {
        string before = text.Text.Substring(0, group.Index);
        string during = group.Value;
        string after = text.Text.Substring(group.Index + group.Length);
        IInline[] replacement = {
            new WText(before, text.properties),
            wrapper(during, text.properties)
        };
        if (!string.IsNullOrEmpty(after))
            replacement[2] = new WText(after, text.properties);
        return replacement;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        while (line.Count() > 0 && line.Last() is WTab)
            line = line.SkipLast(1);
        if (line.Count() < 3)
            return line;
        IInline first = line.First();
        if (first is not WText text1)
            return line;
        if (!Regex.IsMatch(text1.Text, @"^\s*(Claimant|Respondent)\:?\s*$"))
            return line;
        IEnumerable<IInline> middle = line.Skip(1).Take(line.Count() - 2);
        if (!middle.All(i => i is WTab))
            return line;
        IInline last = line.Last();
        if (last is not WText text3)
            return line;
        return middle.Prepend(first).Append(new WParty(text3));
    }

    private WTable EnrichTable(WTable table) {
        IEnumerable<WRow> rows = EnrichRows(table.TypedRows);
        return new WTable(table.Main, rows);
    }

    private IEnumerable<WRow> EnrichRows(IEnumerable<WRow> rows) {
        return rows.Select(row => EnrichRow(row));
    }

    private WRow EnrichRow(WRow row) {
        if (row.Cells.Count() != 3)
            return row;
        WCell first = (WCell) row.Cells.ElementAt(0);
        WCell second = (WCell) row.Cells.ElementAt(1);
        WCell third = (WCell) row.Cells.ElementAt(2);
        if (!first.Contents.All(block => block is ILine line && string.IsNullOrWhiteSpace(Enricher.NormalizeLine(line))))
            return row;
        if (third.Contents.Count() != 1)
            return row;
        if (third.Contents.First() is not ILine line3)
            return row;
        string three = Enricher.NormalizeLine(line3);
        if (three == "Appellant" || three == "Appellants" || three == "Defendant/Appellant") {
            second = EnrichCell(second, PartyRole.Appellant);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        if (three == "Claimant" || three == "Claimants") {
            second = EnrichCell(second, PartyRole.Claimant);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        if (three == "Defendant" || three == "Defendants") {
            second = EnrichCell(second, PartyRole.Defendant);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        if (three == "Respondent" || three == "Respondents" || three == "Claimant/ Respondent") {
            second = EnrichCell(second, PartyRole.Respondent);
            return new WRow(row.Main, new List<WCell>(3){ first, second, third });
        }
        return row;
    }

    private WCell EnrichCell(WCell cell, PartyRole role) {
        IEnumerable<IBlock> contents = cell.Contents.Select(
            block => {
                if (block is not WLine line)
                    return block;
                if (line.Contents.Count() != 1)
                    return line;
                IInline first = line.Contents.First();
                if (first is not WText wText)
                    return line;
                WParty party = new WParty(wText) { Role = role };
                return new WLine(line, new List<IInline>(1) { party });
            }
        );
        return new WCell(cell.Main, contents);
    }

}

}
