
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
        new() {
            LongName = "Community Right to Bid",
            Abbreviation = "CommunityRightToBid",
            Patterns = new string[] { "Community", "Right to Bid", "Assets of Community Value" },
            Prefix = "CR"
        },
        new() {
            LongName = "Electronic Communications Postal Services and Network Information Systems",
            Abbreviation = "NetworkInformationSystems",
            Patterns = new string[] { "Electronic communications", "postal services", "Network Information Systems" },
            Prefix = "NIS"
        },
        new() {
            LongName = "Environment",
            Abbreviation = "Environment",
            Patterns = new string[] { "Environment" },
            Prefix = "NV"
        },
        new() {
            LongName = "Estate Agents",
            Abbreviation = "EstateAgents",
            Patterns = new string[] { "Estate agent" },
            Prefix = "EEA"
        },
        new() {
            LongName = "Examination Boards",
            Abbreviation = "ExamBoards",
            Patterns = new string[] { "Examination boards", "Exam boards" },
            Prefix = "EB"
        },
        new() {
            LongName = "Food Safety",
            Abbreviation = "FoodSafety",
            Patterns = new string[] { "Food safety", "Food labelling" },
            Prefix = "FD"
        },
        new() {
            LongName = "Gambling",
            Abbreviation = "Gambling",
            Patterns = new string[] { "Gambling" },
            Prefix = "GA"
        },
        new() {
            LongName = "Immigration Services",
            Abbreviation = "ImmigrationServices",
            Patterns = new string[] { "Immigration services" },
            Prefix = "IMS"
        },
        new() {
            LongName = "Individual Electoral Registration",
            Abbreviation = "IndividualElectoralRegistration",
            Patterns = new string[] { "individual electoral registration" },
            Prefix = "IR"
        },
        new() {
            LongName = "Information Rights",
            Abbreviation = "InformationRights",
            Patterns = new string[] { "Information Rights", "Freedom of Information", "Section 166 DPA 1998" },
            Prefix = "EA"
        },
        new() {
            LongName = "Pensions Regulation",
            Abbreviation = "Pensions",
            Patterns = new string[] { "Pensions Regulation", "Pensions" },
            Prefix = "PEN"
        },
        new() {
            LongName = "Standards and Licensing",
            Abbreviation = "StandardsAndLicensing",
            Patterns = new string[] { "Standards and Licensing", "Standards & Licensing", "Professional Regulation" },
            Prefix = "PR"
        },
        new() {
            LongName = "Transport",
            Abbreviation = "Transport",
            Patterns = new string[] { "Transport" },
            Prefix = "D"
        },
        new() {
            LongName = "Welfare of Animals",
            Abbreviation = "WelfareOfAnimals",
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
