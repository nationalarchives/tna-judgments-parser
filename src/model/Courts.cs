#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments;

public readonly record struct Court {
    public string Code { get; init; }
    public string LongName { get; init; }
    public string ShortName { get; init; }
    public string URL { get; init; }
    public Regex? CitationPattern { get; init; }
}

public readonly partial struct Courts {

    public static readonly Court SupremeCourt = new Court() {
        Code = "UKSC",
        LongName = "The Supreme Court",
        URL = "https://www.supremecourt.uk/",
        CitationPattern = new Regex(@"^\[\d{4}\] UKSC \d+$")
    };

    public static readonly Court PrivyCouncil = new Court() {
        Code = "UKPC",
        LongName = "The Judicial Committee of the Privy Council",
        URL = "https://www.jcpc.uk/",
        CitationPattern = new Regex(@"^\[\d{4}\] UKPC \d+$")
    };

    public static readonly Court CoA_Crim = new Court {
        Code = "EWCA-Criminal",
        LongName = "The Court of Appeal of England and Wales (Criminal Division)",
        ShortName = "Court of Appeal Criminal Division",
        URL = "https://www.gov.uk/courts-tribunals/court-of-appeal-criminal-division",
        CitationPattern = new Regex(@"^\[\d{4}\] EWCA Crim \d+$")
    };
    public static readonly Court CoA_Civil = new Court {
        Code = "EWCA-Civil",
        LongName = "The Court of Appeal of England and Wales (Civil Division)",
        ShortName = "Court of Appeal Criminal Division",
        URL = "https://www.gov.uk/courts-tribunals/court-of-appeal-civil-division",
        CitationPattern = new Regex(@"^\[\d{4}\] EWCA Civ \d+$")
    };
    
    /* The High Court */

    public static readonly Court EWHC = new Court {
        Code = "EWHC",
        LongName = "The High Court of Justice",
        ShortName = "The High Court",
        URL = ""
    };

    /* The three Divisions of the High Court: Kings's/Queen's Bench, Chancery and Family */

    public static readonly Court EWHC_QBD = new Court {
        Code = "EWHC-QBD",
        LongName = "The Queen's Bench Division of the High Court",
        ShortName = "The Queen's Bench Division",
        URL = "https://www.gov.uk/courts-tribunals/queens-bench-division-of-the-high-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(QB\)$")
    };

    public static readonly Court EWHC_Chancery = new Court {
        Code = "EWHC-Chancery",
        LongName = "The Chancery Division of the High Court",
        ShortName = "The Chancery Division",
        URL = "https://www.gov.uk/courts-tribunals/chancery-division-of-the-high-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(Ch\)$")
    };

    public static readonly Court EWHC_Family = new Court {
        Code = "EWHC-Family",
        LongName = "The Family Division of the High Court",
        ShortName = "The Family Division",
        URL = "https://www.gov.uk/courts-tribunals/family-division-of-the-high-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(Fam\)$")
    };

    /* The four courts (non-specialist) within the Queen's Bench Division */

    // public static readonly Court EWHC_QBD_General = new Court {
    //     Code = "EWHC-QBD-General",
    //     LongName = "The Queen's Bench Division of the High Court",
    //     ShortName = "The Queen's Bench Division",
    //     URL = "https://www.gov.uk/courts-tribunals/queens-bench-division-of-the-high-court"
    // };
    public static readonly Court EWHC_QBD_Administrative = new Court {
        Code = "EWHC-QBD-Admin",
        LongName = "The Administrative Court (Queen's Bench Division)",
        ShortName = "The Administrative Court",
        URL = "https://www.gov.uk/courts-tribunals/administrative-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(Admin\)$")
    };
    public static readonly Court EWHC_QBD_Planning = new Court {
        Code = "EWHC-QBD-Planning",
        LongName = "The Planning Court (Queen's Bench Division)",
        ShortName = "The Planning Court",
        URL = "https://www.gov.uk/courts-tribunals/planning-court"
    };
    public static readonly Court EWHC_QBD_BusinessAndProperty = new Court {
        Code = "EWHC-QBD-BusinessAndProperty",
        LongName = "The Business and Property Courts (Queen's Bench Division)",
        ShortName = "The Business and Property Courts",
        URL = "https://www.gov.uk/courts-tribunals/the-business-and-property-courts"
    };

    /* "Specialist" Business and Property Courts within the Queen's Bench Division */

    public static readonly Court EWHC_QBD_Commercial = new Court {
        Code = "EWHC-QBD-Commercial",
        LongName = "The Business and Property Courts (Commercial Court)",
        ShortName = "The Commercial Court",
        URL = "https://www.gov.uk/courts-tribunals/commercial-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(Comm\)$")
    };

    public static readonly Court EWHC_QBD_Admiralty = new Court {
        Code = "EWHC-QBD-Admiralty",
        LongName = "The Business and Property Courts (Admiralty Court)",
        ShortName = "The Admiralty Court",
        URL = "https://www.gov.uk/courts-tribunals/admiralty-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(Admlty\)$")
    };

    public static readonly Court EWHC_QBD_TCC = new Court {
        Code = "EWHC-QBD-TCC",
        LongName = "The Business and Property Courts (Technology and Construction Court)",
        ShortName = "The Technology and Construction Court",
        URL = "https://www.gov.uk/courts-tribunals/technology-and-construction-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(TCC\)$")
    };

    public static readonly Court EWHC_QBD_Commercial_Financial = new Court {
        Code = "EWHC-QBD-Commercial-Financial",
        LongName = "The Business and Property Courts (The Financial List) (QBD)",
        ShortName = "The Financial List",
        URL = "https://www.gov.uk/courts-tribunals/the-financial-list"
    };

    public static readonly Court EWHC_QBD_Commercial_Circuit = new Court {
        Code = "EWHC-QBD-Commercial-Circuit",
        LongName = "The Business and Property Courts (Circuit Commercial Court)",
        ShortName = "The Circuit Commercial Court",
        URL = "https://www.gov.uk/courts-tribunals/commercial-circuit-court"
    };

    /* King's Bench */

    private static Court ConvertQueensToKings(Court court) {
        return new Court {
            Code = court.Code.Replace("-QBD", "-KBD"),
            LongName = court.LongName.Replace("Queen", "King").Replace("QBD", "KBD"),
            ShortName = court.ShortName.Replace("Queen", "King").Replace("QBD", "KBD"),
            URL = court.URL,
            CitationPattern = court.CitationPattern is null ? null : new Regex(court.CitationPattern.ToString().Replace(@"\(QB\)", @"\(KB\)"))
        };
    }
    public static readonly Court EWHC_KBD = ConvertQueensToKings(EWHC_QBD);
    public static readonly Court EWHC_KBD_Administrative = ConvertQueensToKings(EWHC_QBD_Administrative);
    public static readonly Court EWHC_KBD_Planning = ConvertQueensToKings(EWHC_QBD_Planning);
    public static readonly Court EWHC_KBD_BusinessAndProperty = ConvertQueensToKings(EWHC_QBD_BusinessAndProperty);
    public static readonly Court EWHC_KBD_Commercial = ConvertQueensToKings(EWHC_QBD_Commercial);
    public static readonly Court EWHC_KBD_Admiralty = ConvertQueensToKings(EWHC_QBD_Admiralty);
    public static readonly Court EWHC_KBD_TCC = ConvertQueensToKings(EWHC_QBD_TCC);
    public static readonly Court EWHC_KBD_Commercial_Financial = ConvertQueensToKings(EWHC_QBD_Commercial_Financial);
    public static readonly Court EWHC_KBD_Commercial_Circuit = ConvertQueensToKings(EWHC_QBD_Commercial_Circuit);

    /* Courts within the Chancery Division of the High Court -- all are specialist "Business and Property Courts" */

    public static readonly Court EWHC_Chancery_BusinessAndProperty = new Court {
        Code = "EWHC-Chancery-BusinessAndProperty",
        LongName = "The Business and Property Courts (Chancery Division)",
        ShortName = "The Business and Property Courts",
        URL = "https://www.gov.uk/courts-tribunals/the-business-and-property-courts"
    };

    public static readonly Court EWHC_Chancery_BusinessList = new Court {
        Code = "EWHC-Chancery-Business",
        LongName = "The Business and Property Courts (Business List)",
        ShortName = "The Business List",
        URL = "https://www.gov.uk/courts-tribunals/the-business-list"
    };

    public static readonly Court EWHC_Chancery_InsolvencyAndCompanies = new Court {
        Code = "EWHC-Chancery-InsolvencyAndCompanies",
        LongName = "The Business and Property Courts (Insolvency and Companies List)",
        ShortName = "The Insolvency and Companies List ",
        URL = "https://www.gov.uk/courts-tribunals/insolvency-list"
    };

    public static readonly Court EWHC_Chancery_Financial = new Court {
        Code = "EWHC-Chancery-Financial",
        LongName = "The Business and Property Courts (Financial List)",
        ShortName = "The Financial List",
        URL = "https://www.gov.uk/courts-tribunals/the-financial-list"
    };

    // public static readonly Court EWHC_Chancery_Competition = new Court {
    //     Code = "EWHC-Chancery-Competition",
    //     LongName = "Business and Property Courts (Competition List)",
    //     ShortName = "Competition List",
    //     URL = "https://www.gov.uk/courts-tribunals/the-competition-list"
    // };

    public static readonly Court EWHC_Chancery_IntellectualProperty = new Court {
        Code = "EWHC-Chancery-IntellectualProperty",
        LongName = "The Business and Property Courts (Intellectual Property List)",
        ShortName = "The Intellectual Property List",
        URL = "https://www.gov.uk/courts-tribunals/the-intellectual-property-list"
    };

    // public static readonly Court EWHC_Chancery_Revenue = new Court {
    //     Code = "EWHC-Chancery-Revenue",
    //     LongName = "Business and Property Courts (Revenue List)",
    //     ShortName = "Revenue List",
    //     URL = "https://www.gov.uk/courts-tribunals/the-revenue-list"
    // };

    public static readonly Court EWHC_Chancery_PropertyTrustsProbate = new Court {
        Code = "EWHC-Chancery-PropertyTrustsProbate",
        LongName = "The Business and Property Courts (Property, Trusts and Probate List)",
        ShortName = "The Property, Trusts and Probate List",
        URL = "https://www.gov.uk/courts-tribunals/the-property-trusts-and-probate-list"
    };

    public static readonly Court EWHC_Chancery_Patents = new Court {
        Code = "EWHC-Chancery-Patents",
        LongName = "The Business and Property Courts (Patents Court)",
        ShortName = "The Patents Court",
        URL = "https://www.gov.uk/courts-tribunals/patents-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(Pat\)$")
    };

    public static readonly Court EWHC_Chancery_IPEC = new Court {
        Code = "EWHC-Chancery-IPEC",
        LongName = "The Business and Property Courts (Intellectual Property Enterprise Court)",
        ShortName = "The Intellectual Property Enterprise Court",
        URL = "https://www.gov.uk/courts-tribunals/intellectual-property-enterprise-court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(IPEC\)$")
    };

    public static readonly Court EWHC_Chancery_Appeals = new Court {
        Code = "EWHC-Chancery-Appeals",
        LongName = "The Business and Property Courts (Chancery Appeals)",
        ShortName = "Chancery Appeals",
        URL = "https://www.gov.uk/courts-tribunals/chancery-division-of-the-high-court"
    };

    public static readonly Court EWHC_SeniorCourtsCosts = new Court {
        Code = "EWHC-SeniorCourtsCosts",
        LongName = "The England and Wales High Court (Senior Courts Costs Office)",
        ShortName = "The Senior Courts Costs Office",
        URL = "https://www.gov.uk/courts-tribunals/senior-courts-costs-office",
        CitationPattern = new Regex(@"^\[\d{4}\] EWHC \d+ \(SCCO\)$")
    };

    /* other courts */

    public static readonly Court EWCOP = new Court {
        Code = "EWCOP",
        LongName = "The Court of Protection",
        URL = "https://www.gov.uk/courts-tribunals/court-of-protection",
        CitationPattern = new Regex(@"^\[\d{4}\] EWCOP \d+$")
    };
    public static readonly Court EWCOP_T1 = new Court {
        Code = "EWCOP-T1",
        LongName = "The Court of Protection (Tier 1 cases)",
        ShortName = "The Court of Protection",
        URL = "https://www.gov.uk/courts-tribunals/court-of-protection",
        CitationPattern = new Regex(@"^\[\d{4}\] EWCOP \d+ \(T1\)$")
    };
    public static readonly Court EWCOP_T2 = new Court {
        Code = "EWCOP-T2",
        LongName = "The Court of Protection (Tier 2 cases)",
        ShortName = "The Court of Protection",
        URL = "https://www.gov.uk/courts-tribunals/court-of-protection",
        CitationPattern = new Regex(@"^\[\d{4}\] EWCOP \d+ \(T2\)$")
    };
    public static readonly Court EWCOP_T3 = new Court {
        Code = "EWCOP-T3",
        LongName = "The Court of Protection (Tier 3 cases)",
        ShortName = "The Court of Protection",
        URL = "https://www.gov.uk/courts-tribunals/court-of-protection",
        CitationPattern = new Regex(@"^\[\d{4}\] EWCOP \d+ \(T3\)$")
    };

    public static readonly Court EWFC = new Court {
        Code = "EWFC",
        LongName = "The Family Court",
        URL = "https://www.judiciary.uk/you-and-the-judiciary/going-to-court/family-law-courts/",
        CitationPattern = new Regex(@"^\[\d{4}\] EWFC \d+$")
    };
    public static readonly Court EWFC_B = new Court {
        Code = "EWFC-B",
        LongName = "The Family Court (B cases)",
        ShortName = "The Family Court",
        CitationPattern = new Regex(@"^\[\d{4}\] EWFC \d+ \(B\)$")
    };

    public static readonly Court EWCC = new Court {
        Code = "EWCC",
        LongName = "The County Court",
        URL = "https://www.judiciary.uk/courts-and-tribunals/county-court/",
        CitationPattern = new Regex(@"^\[\d{4}\] EWCC \d+$")
    };

    public static readonly Court EWCR = new Court {
        Code = "EWCR",
        LongName = "The Crown Court",
        URL = "https://www.judiciary.uk/courts-and-tribunals/crown-court/",
        CitationPattern = new Regex(@"^\[\d{4}\] EWCR \d+$")
    };

    /* tribunals */

    public static readonly Court UpperTribunal_AdministrativeAppealsChamber = new Court {
        Code = "UKUT-AAC",
        LongName = "United Kingdom Upper Tribunal (Administrative Appeals Chamber)",
        URL = "https://www.gov.uk/courts-tribunals/upper-tribunal-administrative-appeals-chamber",
        CitationPattern = new Regex(@"^\[\d{4}\] UKUT \d+ \(AAC\)$")
    };

    public static readonly Court OldAsylumAndImmigrationTribunal = new Court {
        Code = "UKAIT",
        LongName = "United Kingdom Asylum and Immigration Tribunal",
        ShortName = "Asylum and Immigration Tribunal",
        URL = "http://www.tribunals.gov.uk/ImmigrationAsylum/",  // no longer active but should be unique?
        CitationPattern = UKAITRegex()
    };
    [GeneratedRegex("^\\[\\d{4}\\] UKAIT \\d+$")]
    private static partial Regex UKAITRegex();

    public static readonly Court UpperTribunal_ImmigrationAndAsylumChamber = new Court {
        Code = "UKUT-IAC",
        LongName = "United Kingdom Upper Tribunal (Immigration and Asylum Chamber)",
        URL = "https://www.gov.uk/courts-tribunals/upper-tribunal-immigration-and-asylum-chamber",
        CitationPattern = new Regex(@"^\[\d{4}\] UKUT \d+ \(IAC\)$")
    };

    public static readonly Court OldImmigrationServicesTribunal = new Court {
        Code = "UKIST",
        LongName = "The Immigation Services Tribunal", //United Kingdom Immigration Services Tribunal",
        URL = "", //https://www.gov.uk/courts-tribunals/first-tier-tribunal-immigration-and-asylum"
        CitationPattern = new Regex(@"^\[\d{4}\] UKIST \d+$")
    };
    public static readonly Court UpperTribunal_LandsChamber = new Court {
        Code = "UKUT-LC",
        LongName = "United Kingdom Upper Tribunal (Lands Chamber)",
        URL = "https://www.gov.uk/courts-tribunals/upper-tribunal-lands-chamber",
        CitationPattern = new Regex(@"^\[\d{4}\] UKUT \d+ \(LC\)$")
    };
    public static readonly Court UpperTribunal_TaxAndChanceryChamber = new Court {
        Code = "UKUT-TCC",
        LongName = "United Kingdom Upper Tribunal (Tax and Chancery Chamber)",
        URL = "https://www.gov.uk/courts-tribunals/upper-tribunal-tax-and-chancery-chamber",
        CitationPattern = new Regex(@"^\[\d{4}\] UKUT \d+ \(TCC\)$")
    };

    public static readonly Court EmploymentAppealTribunal = new Court {
        Code = "EAT",
        LongName = "Employment Appeal Tribunal",
        URL = "https://www.gov.uk/courts-tribunals/employment-appeal-tribunal",
        CitationPattern = new Regex(@"^\[\d{4}\] EAT \d+$")
    };

    public static readonly Court FirstTierTribunal_Tax = new Court {
        Code = "UKFTT-TC",
        LongName = "United Kingdom First-tier Tribunal (Tax)",
        URL = "https://www.gov.uk/courts-tribunals/first-tier-tribunal-tax",
        CitationPattern = new Regex(@"^\[\d{4}\] UKFTT \d+ \(TC\)$")
    };

    public static readonly Court FirstTierTribunal_GRC = new Court {
        Code = "UKFTT-GRC",
        LongName = "United Kingdom First-tier Tribunal (General Regulatory Chamber)",
        URL = "https://www.gov.uk/courts-tribunals/first-tier-tribunal-general-regulatory-chamber",
        CitationPattern = new Regex(@"^\[\d{4}\] UKFTT \d+ \(GRC\)$")
    };

    public static readonly Court FirstTierTribunal_PropertyChamber = new() {
        Code = "FTT-PC",
        LongName = "United Kingdom First-tier Tribunal (Property Chamber)",
        URL = "https://www.gov.uk/courts-tribunals/first-tier-tribunal-property-chamber",
        CitationPattern = new Regex(@"^\[\d{4}\] UKFTT \d+ \(PC\)$")
    };

    public static readonly Court EmploymentTribunal = new Court {
        Code = "ET",
        LongName = "The Employment Tribunal",
        // ShortName = "Employment Tribunal",
        URL = "https://www.gov.uk/courts-tribunals/employment-tribunal"
    };

    public static readonly Court InvestigatoryPowersTribunal = new() {
        Code = "UKIPT",
        LongName = "The Investigatory Powers Tribunal",
        URL = "https://investigatorypowerstribunal.org.uk/",
        CitationPattern = new Regex(@"^\[\d{4}\] UKIPTrib \d+$")
    };

    public static readonly Court ConsumerCreditAppealsTribunal = new() {
        Code = "UKFTT-Credit",
        LongName = "Consumer Credit Appeals Tribunal",
        URL = "https://webarchive.nationalarchives.gov.uk/ukgwa/20090516110219/http://www.consumercreditappeals.tribunals.gov.uk/",
    };

    public static readonly Court ClaimsManagementServicesTribunal = new() {
        Code = "FTT-Claims",
        LongName = "Claims Management Services Tribunal",
        URL = "https://webarchive.nationalarchives.gov.uk/ukgwa/20200626120017/http://claimsmanagement.decisions.tribunals.gov.uk/",
    };

    public static readonly Court EstateAgentsTribunal = new() {
        Code = "UKFTT-Estate",
        LongName = "Estate Agents Tribunal",
        URL = "https://webarchive.nationalarchives.gov.uk/ukgwa/20130206050212/https://www.justice.gov.uk/tribunals/estate-agents",
    };

    public const string FirstTierTribunalChamberCodesPattern = "TC|GRC|PC";
    public const string UpperTribunalChamberCodesPattern = "AAC|IAC|LC|TCC";
    
    public static readonly Court[] All = {
        SupremeCourt,
        PrivyCouncil,
        CoA_Crim,
        CoA_Civil,
        EWHC,
        EWHC_KBD, EWHC_QBD,
        EWHC_Chancery,
        EWHC_Family,
        EWHC_KBD_Administrative, EWHC_QBD_Administrative,
        EWHC_KBD_Planning, EWHC_QBD_Planning,
        EWHC_KBD_BusinessAndProperty, EWHC_QBD_BusinessAndProperty,
        EWHC_KBD_Commercial, EWHC_QBD_Commercial,
        EWHC_KBD_Admiralty, EWHC_QBD_Admiralty,
        EWHC_KBD_TCC, EWHC_QBD_TCC,
        EWHC_KBD_Commercial_Financial, EWHC_QBD_Commercial_Financial,
        EWHC_KBD_Commercial_Circuit, EWHC_QBD_Commercial_Circuit,
        EWHC_Chancery_BusinessAndProperty,
        EWHC_Chancery_BusinessList,
        EWHC_Chancery_InsolvencyAndCompanies,
        EWHC_Chancery_Financial,
        // EWHC_Chancery_Competition,
        EWHC_Chancery_IntellectualProperty,
        // EWHC_Chancery_Revenue,
        EWHC_Chancery_PropertyTrustsProbate,
        EWHC_Chancery_Patents,
        EWHC_Chancery_IPEC,
        EWHC_Chancery_Appeals,
        EWHC_SeniorCourtsCosts,

        EWCOP, EWCOP_T1, EWCOP_T2, EWCOP_T3,
        EWFC, EWFC_B,
        EWCC,
        EWCR,

        UpperTribunal_AdministrativeAppealsChamber,
        UpperTribunal_ImmigrationAndAsylumChamber, OldAsylumAndImmigrationTribunal, OldImmigrationServicesTribunal,
        UpperTribunal_LandsChamber,
        UpperTribunal_TaxAndChanceryChamber,

        EmploymentAppealTribunal,

        FirstTierTribunal_Tax,
        FirstTierTribunal_GRC,
        FirstTierTribunal_PropertyChamber,

        EmploymentTribunal,

        InvestigatoryPowersTribunal,
        ConsumerCreditAppealsTribunal,
        ClaimsManagementServicesTribunal,
        EstateAgentsTribunal
    };

    public static readonly ImmutableDictionary<string, Court> ByCode = All.ToImmutableDictionary(c => c.Code, c => c);

    public static Court? ExtractFromCitation(string cite)
    {
        var cleanedCite = WhitespaceRegex().Replace(cite, " ").Trim();

        return All.FirstOrDefault(c => c.CitationPattern?.IsMatch(cleanedCite) ?? false);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
