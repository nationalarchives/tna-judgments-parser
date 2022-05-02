
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

    // internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
    //     List<IBlock> contents = new List<IBlock>(blocks.Count());
    //     for (int i = 0; i < blocks.Count(); i++) {
    //         IBlock block = blocks.ElementAt(i);
    //         contents.Add(block);
    //         if (!IsJudgmentTitle(block))
    //             continue;
    //         while (++i < blocks.Count() && blocks.ElementAt(i) is ILine line && line.IsEmpty()) {
    //             contents.Add(line);
    //         }
    //         if (i == blocks.Count())
    //             break;
    //         if (blocks.ElementAt(i) is not WLine next1)
    //             break;
    //         WLine enriched1 = Enrich(next1);
    //         if (!Object.ReferenceEquals(enriched1, next1)) {
    //             contents.Add(enriched1);
    //             while (++i < blocks.Count()) {
    //                 contents.Add(blocks.ElementAt(i));
    //             }
    //             return contents;
    //         }
    //         if (i > blocks.Count() - 3)
    //             break;
    //         if (blocks.ElementAt(++i) is not WLine next2)
    //             break;
    //         if (blocks.ElementAt(++i) is not WLine next3)
    //             break;
    //         List<WLine> enriched3 = EnrichThreeLinesOrNull(next1, next2, next3);
    //         if (enriched3 is not null) {
    //             contents.AddRange(enriched3);
    //             while (++i < blocks.Count()) {
    //                 contents.Add(blocks.ElementAt(i));
    //             }
    //             return contents;
    //         }
    //         break;
    //     }
    //     return blocks;
    // }

    private bool IsJudgmentTitle(IBlock block) {
        if (block is not WLine line)
            return false;
        if (line.Contents.Count() != 1)
            return false;
        IInline first = line.Contents.First();
        if (first is not WText text)
            return false;
        string trimmed = text.Text.Trim();
        if (trimmed == "JUDGMENT")
            return true;
        if (trimmed == "COSTS JUDGMENT")    // [2021] UKSC 15
            return true;
        return false;
    }

    protected override WLine Enrich(WLine line) {
        IEnumerable<IInline> enriched = Enrich(line.Contents);
        if (Object.ReferenceEquals(enriched, line.Contents))
            return line;
        return new WLine(line, enriched);
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (!line.Any())
            return line;
        if (line.Count() == 1)
            return EnrichSingle(line);
        IEnumerable<IInline> try1 = EnrichWithoutPartyRolesOrNull(line);
        if (try1 is not null)
            return try1;
        IEnumerable<IInline> try2 = EnrichWithLineBreaksOrNull(line);
        if (try2 is not null)
            return try2;
        IEnumerable<IInline> try3 = EnrichWithTwoPartiesAndOneRoleOrNull(line);
        if (try3 is not null)
            return try3;
        List<IInline> contents = new List<IInline>(line.Count());
        IEnumerator<IInline> enumerator = line.GetEnumerator();
        if (!enumerator.MoveNext())
            return line;
        IInline first = enumerator.Current;
        if (first is not WText text1)
            return line;
        IEnumerable<IInline> enriched1 = EnrichOnePartyAndRoleOrNull(text1);
        if (enriched1 is null)
            return line;
        contents.AddRange(enriched1);

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
        IEnumerable<IInline> enriched3 = EnrichOnePartyAndRoleOrNull(text3);
        if (enriched3 is null)
            return line;
        contents.AddRange(enriched3);
        
        if (!enumerator.MoveNext())
            return contents;

        IInline following = enumerator.Current;
        if (following is ILineBreak) {
            contents.Add(following);
            while (enumerator.MoveNext())
                contents.Add(enumerator.Current);
            return contents;
        } else {
            return line;
        }
    }

    private IEnumerable<IInline> EnrichSingle(IEnumerable<IInline> line) {
        IInline inline = line.First();
        if (inline is not WText text)
            return line;
        IEnumerable<IInline> enriched = EnrichSingleOrNull(text);
        if (enriched is null)
            return line;
        return enriched;
    }
    private IEnumerable<IInline> EnrichSingleOrNull(WText text) {
        if (text.Text.StartsWith("In the matter of "))
            return new List<IInline>(1) { new WDocTitle(text) };
        var parts = Regex.Split(text.Text, @" v ", RegexOptions.IgnoreCase);
        if (parts.Length != 2)
            return null;
        IEnumerable<IInline> enriched1 = EnrichOnePartyAndRoleOrNull( new WText(parts[0], text.properties) );
        if (enriched1 is null)
            return null;
        IEnumerable<IInline> enriched2 = EnrichOnePartyAndRoleOrNull( new WText(parts[1], text.properties) );
        if (enriched2 is null)
            return null;
        List<IInline> contents = new List<IInline>();
        contents.AddRange(enriched1);
        contents.Add(new WText(" v ", text.properties));
        contents.AddRange(enriched2);
        return contents;
    }
    // private IEnumerable<IInline> EnrichSingleOrNull(WText text) {
    //     if (text.Text.StartsWith("In the matter of "))
    //         return new List<IInline>(1) { new WDocTitle(text) };
    //     Match match = Regex.Match(text.Text, @"^([A-Z0-9][A-Za-z0-9,\./& ’]*?)( \([A-Za-z ]+\))? \(([A-Z][a-z]+)\) v ([A-Z0-9][A-Za-z0-9,\./& ’]*?)( \([A-Za-z0-9, ]+\))? \(([A-Z][a-z]+)\) *$");
    //     if (!match.Success)
    //         return null;
    //     Group firstPartyNameGroup = match.Groups[1];
    //     Group firstPartyMiddleGroup = match.Groups[2];
    //     Group firstPartyRoleGroup = match.Groups[3];
    //     Group secondPartyNameGroup = match.Groups[4];
    //     Group secondPartyMiddleGroup = match.Groups[5];
    //     Group secondPartyRoleGroup = match.Groups[6];
    //     PartyRole? firstPartyRole = ParseRole(firstPartyRoleGroup.Value);
    //     PartyRole? secondPartyRole = ParseRole(secondPartyRoleGroup.Value);
    //     if (!firstPartyRole.HasValue)
    //         return null;
    //     if (!secondPartyRole.HasValue)
    //         return null;
    //     List<IInline> contents = new List<IInline>(11);
    //     contents.Add(new WParty(firstPartyNameGroup.Value, text.properties) { Role = firstPartyRole.Value });
    //     if (firstPartyMiddleGroup.Length > 0)
    //         contents.Add(new WText(firstPartyMiddleGroup.Value, text.properties));
    //     contents.Add(new WText(" (", text.properties));
    //     contents.Add(new WRole() { Contents = new List<IInline>(1) { new WText(firstPartyRoleGroup.Value, text.properties) }, Role = firstPartyRole.Value });
    //     contents.Add(new WText(") v ", text.properties));
    //     contents.Add(new WParty(secondPartyNameGroup.Value, text.properties) { Role = secondPartyRole.Value });
    //     if (secondPartyMiddleGroup.Length > 0)
    //         contents.Add(new WText(secondPartyMiddleGroup.Value, text.properties));
    //     contents.Add(new WText(" (", text.properties));
    //     contents.Add(new WRole() { Contents = new List<IInline>(1) { new WText(secondPartyRoleGroup.Value, text.properties) }, Role = secondPartyRole.Value });
    //     contents.Add(new WText(")", text.properties));
    //     if (secondPartyRoleGroup.Index + secondPartyRoleGroup.Length < text.Text.Length)
    //         contents.Add(new WText(text.Text.Substring(secondPartyRoleGroup.Index + secondPartyRoleGroup.Length), text.properties));
    //     return contents;
    // }

    private IEnumerable<IInline> EnrichWithoutPartyRolesOrNull(IEnumerable<IInline> line) {
        if (line.Count() != 3)
            return null;
        if (line.ElementAt(0) is not WText text1)
            return null;
        if (line.ElementAt(1) is not WText text2)
            return null;
        if (line.ElementAt(2) is not WText text3)
            return null;
        Match match1 = Regex.Match(text1.Text, @"^([A-Z0-9][A-Za-z0-9,\./ ’]*?) $");
        if (!match1.Success)
            return null;
        if (text2.Text != "v")
            return null;
        Match match2 = Regex.Match(text3.Text, @"^ ([A-Z0-9][A-Za-z0-9,\./ ’]*?) ?$");
        if (!match2.Success)
            return null;
        List<IInline> contents = new List<IInline>();
        contents.Add(new WParty(match1.Groups[1].Value, text1.properties) { Role = PartyRole.BeforeTheV });
        contents.Add(new WText(" ", text1.properties));
        contents.Add(text2);
        contents.Add(new WText(" ", text3.properties));
        contents.Add(new WParty(match2.Groups[1].Value, text3.properties) { Role = PartyRole.AfterTheV });
        if (match2.Groups[1].Index + match2.Groups[1].Length < text3.Text.Length)
            contents.Add(new WText(text3.Text.Substring(match2.Groups[1].Index + match2.Groups[1].Length), text3.properties));
        return contents;
    }

    private IEnumerable<IInline> EnrichWithTwoPartiesAndOneRoleOrNull(IEnumerable<IInline> line) {
        if (line.Count() != 3)
            return null;
        if (line.ElementAt(0) is not WText text1)
            return null;
        if (line.ElementAt(1) is not WText text2)
            return null;
        if (line.ElementAt(2) is not WText text3)
            return null;
        Match match1 = Regex.Match(text1.Text, @"^([A-Z0-9][A-Za-z0-9,\./ ’]*?) $");
        if (!match1.Success)
            return null;
        if (text2.Text != "v")
            return null;
        IEnumerable<IInline> enriched3 = EnrichOnePartyAndRoleOrNull(text3);
        if (enriched3 is null)
            return null;

        PartyRole role2 = enriched3.OfType<WRole>().First().Role;
        PartyRole role1;
        if (role2 == PartyRole.Respondent)
            role1 = PartyRole.Appellant;
        else if (role2 == PartyRole.Appellant)
            role1 = PartyRole.Respondent;
        else
            return null;

        List<IInline> contents = new List<IInline>();
        contents.Add(new WParty(match1.Groups[1].Value, text1.properties) { Role = role1});
        contents.Add(new WText(" ", text1.properties));
        contents.Add(text2);
        contents.AddRange(enriched3);
        return contents;
    }

    private IEnumerable<IInline> EnrichWithLineBreaksOrNull(IEnumerable<IInline> line) {
        if (!line.Where(inline => inline is WLineBreak).Any())
            return null;
        if (!line.All(inline => inline is WText || inline is WLineBreak))
            return null;
        IEnumerable<IInline> enriched = line.SelectMany(inline => {
            if (inline is WLineBreak)
                return new List<IInline>(1) { inline };
            IEnumerable<IInline> enriched1 = EnrichSingleOrNull((WText) inline);
            if (enriched1 is null)
                return new List<IInline>(1) { inline };
            return enriched1;
        });
        if (!enriched.Where(inline => inline is WParty || inline is WDocTitle).Any())
            return null;
        return enriched;
    }

    private PartyRole? ParseRole(string text) {
        switch (text) {
            case "Appellant":
            case "Appellants":
            case "Appellant/Cross-Respondent":
                return PartyRole.Appellant;
            case "Applicant":
            case "Applicants":
                return PartyRole.Applicant;
            case "Respondent":
            case "Respondents":
            case "Respondent/Cross-Appellant":
                return PartyRole.Respondent;
        }
        return null;
    }

    private List<WLine> EnrichThreeLinesOrNull(WLine one, WLine two, WLine three) {
        WLine enriched1 = EnrichOnePartyAndRoleOrNull(one);
        if (enriched1 is null)
            return null;
        if (!((ILine)two).NormalizedContent().Equals("V", StringComparison.InvariantCultureIgnoreCase))
            return null;
        WLine enriched3 = EnrichOnePartyAndRoleOrNull(three);
        if (enriched3 is null)
            return null;
        return new List<WLine>(3) { enriched1, two, enriched3 };
    }

    private WLine EnrichOnePartyAndRoleOrNull(WLine line) {
        if (line.Contents.Count() != 1)
            return null;
        IInline first = line.Contents.First();
        IEnumerable<IInline> enriched = EnrichOnePartyAndRoleOrNull(first);
        if (enriched is null)
            return null;
        return new WLine(line, enriched);
    }

    // private List<IInline> EnrichOnePartyAndRoleOrNull(IInline inline) {
    //     if (inline is not WText text)
    //         return null;
    //     Match match = Regex.Match(text.Text, @"^ *([A-Z0-9][A-Za-z0-9,\./ ’]*?)( \([A-Za-z ]+\))? \(([A-Z][a-z]+)\) *$");
    //     if (!match.Success)
    //         return null;
    //     PartyRole? role = ParseRole(match.Groups[3].Value);
    //     if (!role.HasValue)
    //         return null;
    //     Group partyGroup = match.Groups[1];
    //     Group middleGroup = match.Groups[2];
    //     Group roleGroup = match.Groups[3];
    //     List<IInline> contents = new List<IInline>();
    //     if (partyGroup.Index > 0)
    //         contents.Add(new WText(text.Text.Substring(0, partyGroup.Index), text.properties));
    //     contents.Add(new WParty(partyGroup.Value, text.properties) { Role = role });
    //     if (middleGroup.Length > 0)
    //         contents.Add(new WText(middleGroup.Value, text.properties));
    //     contents.Add(new WText(" (", text.properties));
    //     contents.Add(new WRole() { Contents = new List<IInline>(1) { new WText(roleGroup.Value, text.properties) }, Role = role.Value });
    //     contents.Add(new WText(text.Text.Substring(roleGroup.Index + roleGroup.Length), text.properties));
    //     return contents;
    // }

    private IEnumerable<IInline> EnrichOnePartyAndRoleOrNull(IInline inline) {
        if (inline is not WText text)
            return null;
        string remainder = text.Text;
        LinkedList<IInline> contents = new LinkedList<IInline>();

        Match lastParentheticalMatch = Regex.Match(remainder, @" \(([^\(\)]+)\) *$");
        if (!lastParentheticalMatch.Success)
            return null;
        Group lastParentheticalGroup = lastParentheticalMatch.Groups[1];
        PartyRole? role = ParseRole(lastParentheticalGroup.Value);
        if (!role.HasValue) {
            WText trailingText = new WText(lastParentheticalMatch.Value, text.properties);
            contents.AddFirst(trailingText);
            remainder = remainder.Substring(0, lastParentheticalMatch.Index);
            lastParentheticalMatch = Regex.Match(remainder, @" \(([A-Z][a-z]+)\) *$");
            if (!lastParentheticalMatch.Success)
                return null;
            lastParentheticalGroup = lastParentheticalMatch.Groups[1];
            role = ParseRole(lastParentheticalGroup.Value);
            if (!role.HasValue)
                return null;
        }
        WText beforeRole = new WText(remainder.Substring(lastParentheticalMatch.Index, lastParentheticalGroup.Index - lastParentheticalMatch.Index), text.properties);
        WRole wRole = new WRole() { Contents = new List<IInline>(1) { new WText(lastParentheticalGroup.Value, text.properties) }, Role = role.Value };
        WText afterRole = new WText(remainder.Substring(lastParentheticalGroup.Index + lastParentheticalGroup.Length), text.properties);
        contents.AddFirst(afterRole);
        contents.AddFirst(wRole);
        contents.AddFirst(beforeRole);
        remainder = remainder.Substring(0, lastParentheticalMatch.Index);

        // if (role.HasValue) {
        // } else {
        //     WText middleText = new WText(lastParentheticalMatch.Value, text.properties);
        //     contents.AddFirst(middleText);
        //     remainder = remainder.Substring(0, lastParentheticalMatch.Index);
        //     Match roleMatch = Regex.Match(remainder, @" \(([A-Z][a-z]+)\) *$");
        //     if (!roleMatch.Success)
        //         return null;
        //     Group roleGroup = roleMatch.Groups[1];
        //     role = ParseRole(roleGroup.Value);
        //     WRole wRole = new WRole() { Contents = new List<IInline>(1) { new WText(roleGroup.Value, text.properties) }, Role = role.Value };
        //     contents.AddLast(new WText(remainder.Substring(roleMatch.Index, roleGroup.Index - roleMatch.Index), text.properties));
        //     contents.AddLast(wRole);
        //     contents.AddLast(new WText(remainder.Substring(roleGroup.Index + roleGroup.Length), text.properties));
        //     remainder = remainder.Substring(0, roleMatch.Index);
        // }

        // Match roleMatch = Regex.Match(remainder, @" \(([A-Z][a-z]+)\) *$");
        // if (!roleMatch.Success)
        //     return null;
        // Group roleGroup = roleMatch.Groups[1];
        // PartyRole? role = ParseRole(roleGroup.Value);

        // if (!role.HasValue) {
        //     Match followingMatch = Regex.Match(remainder, @" \([^\(\)]\) *$");
        //     if (!followingMatch.Success)
        //         return null;
        //     WText followingText = new WText(followingMatch.Value, text.properties);
        //     contents.AddFirst(followingText);
        //     remainder = remainder.Substring(0, followingMatch.Index);
        //     roleMatch = Regex.Match(remainder, @" \(([A-Z][a-z]+)\) *$");
        // }

        // if (!role.HasValue)
        //     return null;
        // WRole wRole = new WRole() { Contents = new List<IInline>(1) { new WText(roleGroup.Value, text.properties) }, Role = role.Value };
        // contents.AddLast(new WText(remainder.Substring(roleMatch.Index, roleGroup.Index - roleMatch.Index), text.properties));
        // contents.AddLast(wRole);
        // contents.AddLast(new WText(remainder.Substring(roleGroup.Index + roleGroup.Length), text.properties));

        // remainder = remainder.Substring(0, roleMatch.Index);

        // trailing parenthetical is not part of the name
        Match middleMatch = Regex.Match(remainder, @" \([^\(\)]+\) *$");
        if (middleMatch.Success) {
            WText middleText = new WText(middleMatch.Value, text.properties);
            contents.AddFirst(middleText);
            remainder = remainder.Substring(0, middleMatch.Index);
        }

        Match partyMatch = Regex.Match(remainder, @"^ *(.+?) *$");
        if (!partyMatch.Success)
            return null;
        Group partyGroup = partyMatch.Groups[1];
        if (string.IsNullOrWhiteSpace(partyGroup.Value))
            return null;
        if (partyGroup.Index + partyGroup.Length < remainder.Length)
            contents.AddFirst(new WText(remainder.Substring(partyGroup.Index + partyGroup.Length), text.properties));
        WParty party = new WParty(partyGroup.Value, text.properties) { Role = role };
        contents.AddFirst(party);
        if (partyGroup.Index > 0)
            contents.AddFirst(new WText(remainder.Substring(0, partyGroup.Index), text.properties));

        return Merger.Merge(contents);
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
        if (cell.Contents.Count() >= 1) {
            IBlock block = cell.Contents.First();
            if (block is WLine line) {
                WLine enriched = Enrich(line);
                if (!Object.ReferenceEquals(enriched, line)) {
                    IEnumerable<IBlock> rest = cell.Contents.Skip(1).Select(block => block is WLine wLine ? Enrich(wLine) : block);
                    return new WTable(table.Main, table.Properties, table.Grid, table.TypedRows.Skip(1).Prepend(
                        new WRow(row1.Table, row1.TypedCells.Skip(1).Prepend(
                            new WCell(cell.Row, cell.Props, rest.Prepend(enriched))
                        ))
                    ));
                }
            }
        }
        if (cell.Contents.Count() >= 3) {
            IBlock first = cell.Contents.ElementAt(0);
            IBlock second = cell.Contents.ElementAt(1);
            IBlock third = cell.Contents.ElementAt(2);
            if (first is not WLine line1)
                return table;
            if (second is not WLine line2)
                return table;
            if (third is not WLine line3)
                return table;
            List<WLine> enriched = EnrichThreeLinesOrNull(line1, line2, line3);
            if (enriched is null)
                return table;
            IEnumerable<IBlock> rest = cell.Contents.Skip(3);
            return new WTable(table.Main, table.Properties, table.Grid, table.TypedRows.Skip(1).Prepend(
                new WRow(row1.Table, row1.TypedCells.Skip(1).Prepend(
                    new WCell(cell.Row, cell.Props, Enumerable.Concat(enriched, rest))
                ))
            ));
        }
        return table;
    }

}

}
