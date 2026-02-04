using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments;

class CaseName {

    internal static string Extract(IJudgment judgment) {

        Court? court = judgment.Metadata.Court;

        IEnumerable<IParty> parties = Enumerable.Concat(
            Util.Descendants<IParty>(judgment.CoverPage),
            Util.Descendants<IParty>(judgment.Header)
        );
        IEnumerable<IDocTitle> docTitle = Util.Descendants<IDocTitle>(judgment.Header);
        IEnumerable<ILocation> location = Util.Descendants<ILocation>(judgment.Header);

        IParty party1 = null;
        IParty party2 = null;
        party1 = parties.FirstOrDefault();
        party2 = parties.Where(party => party.Role != party1.Role).FirstOrDefault();
        if (party2 is null && parties.Count() == 2 && !parties.Last().Role.HasValue)
            party2 = parties.Last();
        
        if (party2 is not null) {
            string name1 = party1.Name;
            if (Regex.IsMatch(name1, @"^THE (KING|QUEEN) \(?on the application of\)?$", RegexOptions.IgnoreCase)) {
                IParty next1 = parties.Skip(1).FirstOrDefault();
                if (next1 is not null && next1.Role == party1.Role) {
                    party1.Suppress = true;
                    // next1.ROnTheApplicationOf = true;
                    name1 = next1.Name + " (R on the application of)";
                }
            }
            if (Regex.IsMatch(name1, @"^THE (KING|QUEEN)$", RegexOptions.IgnoreCase)) {
                IParty next1 = parties.Skip(1).FirstOrDefault();
                if (next1 is not null && next1.Role == party1.Role) {
                    char[] trim = { ' ', '(', ')', '-' };
                    string next1normalized = Regex.Replace(next1.Text, @"\s+", " ").Trim(trim);
                    Match match = Regex.Match(next1normalized, @"^on the application of ([A-Z][A-Z0-9 ]*)$", RegexOptions.IgnoreCase);
                    if (match.Success) {
                        party1.Suppress = true;
                        // next1.Name = match.Groups[1].Value;
                        // next1.ROnTheApplicationOf = true;
                        name1 = match.Groups[1].Value + " (R on the application of)";
                    }
                    match = Regex.Match(next1normalized, @"^on the application of$", RegexOptions.IgnoreCase);
                    if (match.Success) {
                        IParty next2 = parties.Skip(2).FirstOrDefault();
                        if (next2 is not null && next2.Role == party1.Role) {
                            party1.Suppress = true;
                            next1.Suppress = true;
                            // next2.ROnTheApplicationOf = true;
                            name1 = next2.Name + " (R on the application of)";
                        }
                    }
                }
            }
            string name2 = party2.Name;
            if (Regex.IsMatch(name2, @"^THE (KING|QUEEN) \(?on the application of\)?$", RegexOptions.IgnoreCase)) {
                IParty next1 = parties.Where(p => p.Role == party2.Role).Skip(1).FirstOrDefault();
                if (next1 is not null) {
                    party2.Suppress = true;
                    name2 = next1.Name + " (R on the application of)";
                }
            }
            string combined = name1 + " v " + name2;
            if (court == Courts.PrivyCouncil)
                if (location.Any())
                    return combined + " (" + location.First().Text + ")";
            return combined;
        }
        if (docTitle.Any()) {
            List<string> forName = new List<string>() { Util.NormalizeSpace(docTitle.First().Text) };
            foreach (IDocTitle item in docTitle.Skip(1)) {
                string normalized = Util.NormalizeSpace(item.Text);
                if (normalized.StartsWith("In the matter of ", StringComparison.InvariantCultureIgnoreCase))
                    break;
                if (normalized.StartsWith("Re ", StringComparison.InvariantCultureIgnoreCase))
                    break;
                if (normalized.StartsWith("Re: ", StringComparison.InvariantCultureIgnoreCase))
                    break;
                forName.Add(normalized);
            }
            return string.Join(" ", forName);
        }
        // [2021] EWHC 3099 (Fam)
        if (party1 is not null && party2 is null && party1.Name.StartsWith("Re ") && party1.Role == PartyRole.Applicant)
            return party1.Name;
        return null;
    }

}
