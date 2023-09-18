
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class GRCCombo : Combo3 {

    public new static Regex Re1 => new(@"^First-tier Tribunal$", RegexOptions.IgnoreCase);
    public new static Regex Re2 => new(@"^\(?General Regulatory Chamber\)?$", RegexOptions.IgnoreCase);
    public new static Court Court => Courts.FirstTierTribunal_GRC;

    override internal bool Match(IBlock one, IBlock two, IBlock three) {
        return Match(Re1, one) && Match(Re2, two) && MatchJurisdiction(three);
    }
    override internal List<WLine> Transform(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) { Transform1(one), Transform1(two), TransformJurisdiction(three) };
    }

    override internal bool MatchFirstRun(IBlock one, IBlock two, IBlock three) {
        return MatchFirstRun(Re1, one) && Match(Re2, two) && MatchJurisdiction(three);
    }
    override internal List<WLine> TransformFirstRun(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) { TransformFirstRun(one), Transform1(two), TransformJurisdiction(three) };
    }

    override internal bool MatchTwoFirstRuns(IBlock one, IBlock two, IBlock three) {
        return MatchFirstRun(Re1, one) && MatchFirstRun(Re2, two) && MatchJurisdiction(three);
    }
    override internal List<WLine> TransformTwoFirstRuns(IBlock one, IBlock two, IBlock three) {
        return new List<WLine>(3) { TransformFirstRun(one), TransformFirstRun(two), TransformJurisdiction(three) };
    }


    private static bool MatchJurisdiction(IBlock block) {
        // if (block is not WLine line)
        //     return false;
        // string text = line.NormalizedContent.TrimStart('(').TrimEnd(')');
        // foreach (var x in X)
        //     foreach (string pattern in x.Patterns)
        //         if (text.StartsWith(pattern, StringComparison.InvariantCultureIgnoreCase))
        //             return true;
        // return false;
        return GetJurisdiction(block) is not null;
    }

    private static GRCJurisdiction GetJurisdiction(IBlock block) {
        if (block is not WLine line)
            return null;
        string text = line.NormalizedContent.TrimStart('(').TrimEnd(')');
        foreach (var x in X)
            foreach (string pattern in x.Patterns)
                if (text.StartsWith(pattern, StringComparison.InvariantCultureIgnoreCase))
                    return x;
        return null;
    }

    private static WLine TransformJurisdiction(IBlock block) {
        WLine line = (WLine) block;
        GRCJurisdiction x = GetJurisdiction(block);
        WDocJurisdiction juris = new() { Contents = line.Contents, LongName = x.Name, ShortName = x.Abbreviation };
        return WLine.Make(line, new List<IInline>(1) { juris });
    }

    // static readonly IDictionary<string, ISet<string>> Jurisdictions = new Dictionary<string, ISet<string>>() {
    //     { "Charity", new HashSet<string>() { "Charity" } },
    //     { "InfoRights", new HashSet<string>() { "Information Rights" } },
    //     { "AnimalWelfare", new HashSet<string>() { "Welfare of animals" } }  
    // };

    // https://www.judiciary.uk/courts-and-tribunals/tribunals/first-tier-tribunal/general-regulatory-chamber/the-work-of-the-general-regulatory-chamber/
    // https://www.gov.uk/courts-tribunals/first-tier-tribunal-general-regulatory-chamber
    // https://sway.office.com/xyqXZSFJRCZIo1DV?ref=Link


/*

Charity,
Community Right to Bid,
Electronic Communications, Postal Services and Network Information Systems,
Environment,
Estate Agents,
Exam Boards,
Food Safety,
Gambling,
Immigration Services,
Individual Electoral Regulation,
Information Rights,
Pensions,
Standards & Licensing,
Transport, and
Welfare of Animals.

*/



    static readonly GRCJurisdiction[] X = {
        new() {
            Name = "Charity",
            Abbreviation = "Charity",
            Patterns = new string[] { "Charity", "charities" }
        },
        new() {
            Name = "Claims Management",
            Abbreviation = "Claims-Management",
            Patterns = new string[] { "Claims" }
        },
        new() {
            Name = "Community Right to Bid",
            Abbreviation = "Community-Right-to-Bid",
            Patterns = new string[] { "Community", "Right to Bid" }
        },
        new() {
            Name = "Consultant Lobbyists",
            Abbreviation = "Consultant-Lobbyists",
            Patterns = new string[] { "Consultant", "lobbyists" }
        },
        new() {
            Name = "Conveyancing",
            Abbreviation = "Conveyancing",
            Patterns = new string[] { "Conveyancing" }
        },
        new() {
            Name = "Copyright Licensing",
            Abbreviation = "Copyright-Licensing",
            Patterns = new string[] { "Copyright" }
        },
        new() {
            Name = "Electronic communications and postal services",
            Abbreviation = "",
            Patterns = new string[] { "Electronic communications", "postal services" }
        },
        new() {
            Name = "Environment",
            Abbreviation = "Environment",
            Patterns = new string[] { "Environment" }
        },
        new() {
            Name = "Estate Agents",
            Abbreviation = "Estate-Agents",
            Patterns = new string[] { "Estate agent" }
        },
        new() {
            Name = "Examination Boards",
            Abbreviation = "Exam-Boards",
            Patterns = new string[] { "Examination boards", "Exam boards" }
        },
        new() {
            Name = "Food Labelling",
            Abbreviation = "Food",
            Patterns = new string[] { "Food labelling", "food safety" }
        },
        new() {
            Name = "Gambling",
            Abbreviation = "Gambling",
            Patterns = new string[] { "Gambling" }
        },
        new() {
            Name = "Immigration Services",
            Abbreviation = "Immigration-Services",
            Patterns = new string[] { "Immigration services" }
        },
        new() {
            Name = "Individual Electoral Registration",
            Abbreviation = "Individual-Electoral-Registration",
            Patterns = new string[] { "individual electoral registration" } // Individual Electronic
        },
        new() {
            Name = "Information Rights",
            Abbreviation = "InfoRights",
            Patterns = new string[] { "Information Rights" }
        },
        new() {
            Name = "Letting and Managing agents",
            Abbreviation = "Letting and managing Agents",
            Patterns = new string[] { "Letting and managing agents", "Letting", "managing agent" }
        },
        new() {
            Name = "Local Government Standards",
            Abbreviation = "Local Government",
            Patterns = new string[] { "Local government standards" }
        },
        new() {
            Name = "Pensions Regulation",
            Abbreviation = "Pensions",
            Patterns = new string[] { "Pensions Regulation" }
        },
        new() {
            Name = "Secondary Ticketing",
            Abbreviation = "Secondary ticketing",
            Patterns = new string[] { "Secondary ticketing" }
        },
        new() {
            Name = "Welfare of Animals",
            Abbreviation = "AnimalWelfare",
            Patterns = new string[] { "Welfare of animals" }
        }
    };

    // static readonly IDictionary<string, string> Jurisdictions3 = new Dictionary<string, string>() {
    //     { "charity", "Charity" },
    //     { "claims", "Claims" },
    //     { "community right to bid", "CommunityRightToBid" }
    // };

    // static readonly string[] Jurisdictions = {
    //     "Charity",
    //     "Claims management",
    //     "Community right to bid",
    //     "Consultant lobbyists",
    //     "Conveyancing",
    //     "Copyright licensing",
    //     "Electronic communications and postal services",
    //     "Environment",
    //     "Estate agent",
    //     "Examination boards",
    //     "Food labelling",
    //     "Gambling Appeals",
    //     "Immigration services",
    //     "Individual Electronic",
    //     "Information rights",
    //     "Letting and managing agents",
    //     "Local government standards",
    //     "Pensions Regulation",
    //     "Secondary ticketing",
    //     "Welfare of animals"
    // };

}

class GRCJurisdiction {

    internal string Name { get; init; }

    internal string Abbreviation { get; init; }

    internal string[] Patterns { get; init; }

}

}
