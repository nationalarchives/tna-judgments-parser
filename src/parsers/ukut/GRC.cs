
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class GRCCombo : Combo3 {

    private static readonly ILogger Logger = Logging.Factory.CreateLogger<GRCCombo>();

    internal GRCCombo() {
        Re1 = new(@"^First-tier Tribunal$", RegexOptions.IgnoreCase);
        Re2 = new(@"^\(?General Regulatory Chamber\)?$", RegexOptions.IgnoreCase);
        Court = Courts.FirstTierTribunal_GRC;
    }

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
        return GetJurisdiction(block) is not null;
    }

    private static GRCJurisdiction GetJurisdiction(IBlock block) {
        if (block is not WLine line)
            return null;
        string text = line.NormalizedContent.TrimStart('(').TrimStart('[');
        foreach (var jd in Jurisdictions)
            foreach (string pattern in jd.Patterns)
                if (text.StartsWith(pattern, StringComparison.InvariantCultureIgnoreCase))
                    return jd;
        return null;
    }

    private static WLine TransformJurisdiction(IBlock block) {
        WLine line = (WLine) block;
        GRCJurisdiction jd = GetJurisdiction(block);
        Logger.LogInformation("found jurisdiction {}", jd.Abbreviation);
        WDocJurisdiction juris = new() { Contents = line.Contents, LongName = jd.LongName, ShortName = jd.Abbreviation };
        return WLine.Make(line, new List<IInline>(1) { juris });
    }

    // https://www.judiciary.uk/courts-and-tribunals/tribunals/first-tier-tribunal/general-regulatory-chamber/the-work-of-the-general-regulatory-chamber/
    // https://www.gov.uk/courts-tribunals/first-tier-tribunal-general-regulatory-chamber
    // https://sway.office.com/xyqXZSFJRCZIo1DV?ref=Link

    static readonly GRCJurisdiction[] Jurisdictions = {
        new() {
            LongName = "Charity",
            Abbreviation = "Charity",
            Patterns = new string[] { "Charity", "Charities" },
            Prefix = "CA"
        },
        // new() {
        //     Name = "Claims Management",
        //     Abbreviation = "Claims-Management",
        //     Patterns = new string[] { "Claims" },
        //     Prefix = null
        // },
        new() {
            LongName = "Community Right to Bid",
            Abbreviation = "CommunityRights",
            Patterns = new string[] { "Community", "Right to Bid" },
            Prefix = "CR"
        },
        // new() {
        //     Name = "Consultant Lobbyists",
        //     Abbreviation = "Consultant-Lobbyists",
        //     Patterns = new string[] { "Consultant", "lobbyists" },
        //     Prefix = null
        // },
        // new() {
        //     Name = "Conveyancing",
        //     Abbreviation = "Conveyancing",
        //     Patterns = new string[] { "Conveyancing" },
        //     Prefix = null
        // },
        // new() {
        //     Name = "Copyright Licensing",
        //     Abbreviation = "CopyrightLicensing",
        //     Patterns = new string[] { "Copyright" },
        //     Prefix = null
        // },
        // new() {
        //     Name = "Electronic communications and postal services",
        //     Abbreviation = "",
        //     Patterns = new string[] { "Electronic communications", "postal services" },
        //     Prefix = null
        // },
        new() {
            LongName = "Environment",
            Abbreviation = "Environment",
            Patterns = new string[] { "Environment" },
            Prefix = "NV" // NVZ
        },
        // new() {
        //     Name = "Estate Agents",
        //     Abbreviation = "Estate-Agents",
        //     Patterns = new string[] { "Estate agent" },
        //     Prefix = null
        // },
        // new() {
        //     Name = "Examination Boards",
        //     Abbreviation = "Exam-Boards",
        //     Patterns = new string[] { "Examination boards", "Exam boards" },
        //     Prefix = null
        // },
        // new() {
        //     Name = "Food Labelling",
        //     Abbreviation = "Food",
        //     Patterns = new string[] { "Food labelling", "food safety" },
        //     Prefix = null
        // },
        // new() {
        //     Name = "Gambling",
        //     Abbreviation = "Gambling",
        //     Patterns = new string[] { "Gambling" },
        //     Prefix = null
        // },
        // new() {
        //     LongName = "Immigration Services",
        //     Abbreviation = "ImmigrationServices",
        //     Patterns = new string[] { "Immigration services" },
        //     Prefix = null
        // },
        // new() {
        //     Name = "Individual Electoral Registration",
        //     Abbreviation = "Individual-Electoral-Registration",
        //     Patterns = new string[] { "individual electoral registration" }, // Individual Electronic
        //     Prefix = null
        // },
        new() {
            LongName = "Information Rights",
            Abbreviation = "InformationRights",
            Patterns = new string[] { "Information Rights", "Section 166 DPA 1998" },
            Prefix = "EA"
        },
        // new() {
        //     Name = "Letting and Managing agents",
        //     Abbreviation = "Letting and managing Agents",
        //     Patterns = new string[] { "Letting and managing agents", "Letting", "managing agent" },
        //     Prefix = null
        // },
        // new() {
        //     Name = "Local Government Standards",
        //     Abbreviation = "LocalGovernment",
        //     Patterns = new string[] { "Local government standards" },
        //     Prefix = null
        // },
        new() {
            LongName = "Pensions Regulation",
            Abbreviation = "Pensions",
            Patterns = new string[] { "Pensions Regulation", "Pensions" },
            Prefix = "PEN"
        },
        // new() {
        //     Name = "Secondary Ticketing",
        //     Abbreviation = "SecondaryTicketing",
        //     Patterns = new string[] { "Secondary ticketing" },
        //     Prefix = null
        // },
        new() {
            LongName = "Transport",
            Abbreviation = "Transport",
            Patterns = new string[] { "Transport" },
            Prefix = null
        },
        new() {
            LongName = "Welfare of Animals",
            Abbreviation = "AnimalWelfare",
            Patterns = new string[] { "Welfare of animals", "Animal Welfare" },
            Prefix = "WA"
        }
    };

}

class GRCJurisdiction {

    internal string LongName { get; init; }

    internal string Abbreviation { get; init; }

    internal string[] Patterns { get; init; }

    internal string Prefix { get; init; }

}

}
