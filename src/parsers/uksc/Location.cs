
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse.UKSC {

class LocationEnricher : Enricher {

    internal bool IsJCPC(IEnumerable<IBlock> blocks) {
        foreach (IBlock block in blocks) {
            if (block is ILine line1)
                foreach (IInline inline1 in line1.Contents)
                    if (inline1 is WNeutralCitation cite1)
                        return Regex.IsMatch(cite1.Text, @"^\[\d{4}\] UKPC \d+$");
            if (block is ITable table)
                foreach (IRow row in table.Rows)
                    foreach (ICell cell in row.Cells)
                        foreach (ILine line2 in cell.Contents.OfType<ILine>())
                            foreach (IInline inline2 in line2.Contents)
                                if (inline2 is WNeutralCitation cite2)
                                    return Regex.IsMatch(cite2.Text, @"^\[\d{4}\] UKPC \d+$");
            }
        return false;
    }

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!IsJCPC(blocks))
            return blocks;
        List<IBlock> contents = new List<IBlock>(blocks.Count());
        IEnumerator<IBlock> enumerator = blocks.GetEnumerator();
        while (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            if (block is not WLine line)
                break;
            WLine enriched = Enrich(line);
            contents.Add(enriched);
            if (!Object.ReferenceEquals(enriched, line)) {
                while (enumerator.MoveNext())
                    contents.Add(enumerator.Current);
                return contents;
            }
        }
        return blocks;
    }

    protected override WLine Enrich(WLine line) {
        if (line.Style != "JudgmentDetail1")
            return line;
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (Object.ReferenceEquals(enriched, line.Contents))
            return line;
        return new WLine(line, enriched);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        List<IInline> contents = new List<IInline>(line.Count());
        IEnumerator<IInline> enumerator = line.GetEnumerator();
        bool party1Found = false;
        bool role1Found = false;
        bool party2Found = false;
        bool role2Found = false;
        while (enumerator.MoveNext()) {
            IInline inline = enumerator.Current;
            if (inline is WParty) {
                if (party2Found)
                    return line;
                if (party1Found)
                    party2Found = true;
                else
                    party1Found = true;
                contents.Add(inline);
                continue;
            }
            if (inline is WRole) {
                if (role2Found)
                    return line;
                if (role1Found)
                    role2Found = true;
                else
                    role1Found = true;
                contents.Add(inline);
                continue;
            }
            if (!party2Found) {
                contents.Add(inline);
                continue;
            }
            if (!role2Found) {
                contents.Add(inline);
                continue;
            }
            if (inline is not WText wText) {
                contents.Add(inline);
                continue;
            }
            Match match = Regex.Match(wText.Text, @"^\) \(([A-z][A-Za-z ]*)\)");
            if (!match.Success) {
                contents.Add(inline);
                continue;
            }
            contents.Add(new WText(") (", wText.properties));
            contents.Add(new WLocation(match.Groups[1].Value, wText.properties));
            contents.Add(new WText(")", wText.properties));
            while (enumerator.MoveNext())
                contents.Add(enumerator.Current);
            return contents;
        }
        return line;
    }

}

}
