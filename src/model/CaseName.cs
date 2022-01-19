

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace UK.Gov.Legislation.Judgments {

class CaseName {

    internal static string Extract(IJudgment judgment) {

        Court? court = judgment.Metadata.Court();

        List<IParty> parties = new List<IParty>();
        List<IDocTitle> docTitle = new List<IDocTitle>();
        List<ILocation> location = new List<ILocation>();
        IParty party1 = null;
        IParty party2 = null;
        foreach (IBlock block in judgment.Header) {
            if (block is ILine line) {
                parties.AddRange(line.Contents.OfType<IParty>());
                docTitle.AddRange(line.Contents.OfType<IDocTitle>());
                location.AddRange(line.Contents.OfType<ILocation>());
            }
            if (block is ITable table)
                foreach (IRow row in table.Rows)
                    foreach (ICell cell in row.Cells)
                        foreach (ILine line2 in cell.Contents.OfType<ILine>()) {
                            parties.AddRange(line2.Contents.OfType<IParty>());
                            docTitle.AddRange(line2.Contents.OfType<IDocTitle>());
                            location.AddRange(line2.Contents.OfType<ILocation>());
                        }
        }
        party1 = parties.FirstOrDefault();
        party2 = parties.Where(party => party.Role != party1.Role).FirstOrDefault();
        if (party2 is null && parties.Count() == 2 && !parties.Last().Role.HasValue)
            party2 = parties.Last();
        
        if (party2 is not null) {
            string name1 = party1.Name;
            if (Regex.IsMatch(name1, @"^THE QUEEN \(?on the application of\)?$", RegexOptions.IgnoreCase)) {
                IParty next1 = parties.Skip(1).FirstOrDefault();
                if (next1 is not null && next1.Role == party1.Role) {
                    name1 = next1.Name + " (R on the application of)";
                }
            }
            if (Regex.IsMatch(name1, @"^THE QUEEN$", RegexOptions.IgnoreCase)) {
                IParty next1 = parties.Skip(1).FirstOrDefault();
                if (next1 is not null && next1.Role == party1.Role) {
                    string next1normalized = Regex.Replace(next1.Text, @"\s+", " ").Trim();
                    Match match = Regex.Match(next1normalized, @"^\(on the application of ([A-Z][A-Z0-9 ]*)\)$", RegexOptions.IgnoreCase);
                    if (match.Success) {
                        name1 = match.Groups[1].Value + " (R on the application of)";
                    }
                    match = Regex.Match(next1normalized, @"^on the application of ([A-Z][A-Z0-9 ]*)$", RegexOptions.IgnoreCase);
                    if (match.Success) {
                        name1 = match.Groups[1].Value + " (R on the application of)";
                    }
                    match = Regex.Match(next1normalized, @"^\(?on the application of\)?$", RegexOptions.IgnoreCase);
                    if (match.Success) {
                        IParty next2 = parties.Skip(2).FirstOrDefault();
                        if (next2 is not null && next2.Role == party1.Role) {
                            name1 = next2.Name + " (R on the application of)";
                        }
                    }
                    match = Regex.Match(next1normalized, @"^-on the application of-$", RegexOptions.IgnoreCase);
                    if (match.Success) {
                        IParty next2 = parties.Skip(2).FirstOrDefault();
                        if (next2 is not null && next2.Role == party1.Role) {
                            name1 = next2.Name + " (R on the application of)";
                        }
                    }
                }
            }
            string name2 = party2.Name;
            if (Regex.IsMatch(name2, @"^THE QUEEN \(?on the application of\)?$", RegexOptions.IgnoreCase)) {
                IParty next1 = parties.Where(p => p.Role == party2.Role).Skip(1).FirstOrDefault();
                if (next1 is not null) {
                    name2 = next1.Name + " (R on the application of)";
                }
            }
            string combined = name1 + " v " + name2;
            if (court.HasValue && court.Value.Equals(Courts.PrivyCouncil))
                if (location.Any())
                    return combined + " (" + location.First().Text + ")";
            return combined;
        }
        if (docTitle.Any())
            return string.Join(" ", docTitle.Select(dt => dt.Text.Trim()));
        // [2021] EWHC 3099 (Fam)
        if (party1 is not null && party2 is null && party1.Name.StartsWith("Re ") && party1.Role == PartyRole.Applicant)
            return party1.Name;
        return null;
    }

}

}
