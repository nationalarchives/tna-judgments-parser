
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace UK.Gov.Legislation.Judgments {

public readonly struct Court {

    public string Code { get; init; }
    public string LongName { get; init; }
    public string ShortName { get; init; }
    public string URL { get; init; }

}

public readonly struct Courts {

    public static readonly Court SupremeCourt = new Court() {
        Code = "UK-Supreme-Court",
        LongName = "The UK Supreme Court",
        ShortName = "Supreme Court",
        URL = "https://www.supremecourt.uk/"
    };

    public static readonly Court CoA_Crim = new Court {
        Code = "EWCA-Criminal",
        LongName = "The Court of Appeal of England and Wales (Criminal Division)",
        ShortName = "Court of Appeal Criminal Division",
        URL = "https://www.gov.uk/courts-tribunals/court-of-appeal-criminal-division"
    };
    public static readonly Court CoA_Civil = new Court {
        Code = "EWCA-Civil",
        LongName = "The Court of Appeal of England and Wales (Civil Division)",
        ShortName = "Court of Appeal Criminal Division",
        URL = "https://www.gov.uk/courts-tribunals/court-of-appeal-civil-division"
    };
    
    /* The High Court */

    public static readonly Court EWHC = new Court {
        Code = "EWHC",
        LongName = "The High Court of Justice",
        ShortName = "The High Court",
        URL = ""
    };

    /* The three Divisions of the High Court: Queen's Bench, Chancery and Family */

    public static readonly Court EWHC_QBD = new Court {
        Code = "EWHC-QBD",
        LongName = "The Queen's Bench Division of the High Court",
        ShortName = "The Queen's Bench Division",
        URL = "https://www.gov.uk/courts-tribunals/queens-bench-division-of-the-high-court"
    };

    public static readonly Court EWHC_Chancery = new Court {
        Code = "EWHC-Chancery",
        LongName = "The Chancery Division of the High Court",
        ShortName = "The Chancery Division",
        URL = "https://www.gov.uk/courts-tribunals/chancery-division-of-the-high-court"
    };

    public static readonly Court EWHC_Family = new Court {
        Code = "EWHC-Family",
        LongName = "The Family Division of the High Court",
        ShortName = "The Family Division",
        URL = "https://www.gov.uk/courts-tribunals/family-division-of-the-high-court"
    };

    /* The four courts (non-specialist) within the Queen's Bench Division */

    public static readonly Court EWHC_QBD_General = new Court {
        Code = "EWHC-QBD-General",
        LongName = "The Queen's Bench Division of the High Court",
        ShortName = "The Queen's Bench Division",
        URL = "https://www.gov.uk/courts-tribunals/queens-bench-division-of-the-high-court"
    };
    public static readonly Court EWHC_QBD_Administrative = new Court {
        Code = "EWHC-QBD-Admin",
        LongName = "The Administrative Court (Queen's Bench Division)",
        ShortName = "The Administrative Court",
        URL = "https://www.gov.uk/courts-tribunals/administrative-court"
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
        URL = ""
    };

    /* "Specialist" Business and Property Courts within the Queen's Bench Division */

    public static readonly Court EWHC_QBD_Commercial = new Court {
        Code = "EWHC-QBD-Commercial",
        LongName = "The Business and Property Courts (Commercial Court)",
        ShortName = "The Commercial Court",
        URL = "https://www.gov.uk/courts-tribunals/commercial-court"
    };

    public static readonly Court EWHC_QBD_Admiralty = new Court {
        Code = "EWHC-QBD-Admiralty",
        LongName = "The Business and Property Courts (Admiralty Court)",
        ShortName = "The Admiralty Court",
        URL = "https://www.gov.uk/courts-tribunals/admiralty-court"
    };

    public static readonly Court EWHC_QBD_TCC = new Court {
        Code = "EWHC-QBD-TCC",
        LongName = "The Business and Property Courts (Technology and Construction Court)",
        ShortName = "The Technology and Construction Court",
        URL = "https://www.gov.uk/courts-tribunals/technology-and-construction-court"
    };

    public static readonly Court EWHC_QBD_Commercial_Financial = new Court {
        Code = "EWHC-QBD-Commercial-Financial",
        LongName = "The Business and Property Courts (The Financial List) (QBD)",
        ShortName = "The Financial List",
        URL = ""
    };

    public static readonly Court EWHC_QBD_Commercial_Circuit = new Court {
        Code = "EWHC-QBD-Commercial-Circuit",
        LongName = "The Business and Property Courts (Circuit Commercial Court)",
        ShortName = "The Circuit Commercial Court",
        URL = "https://www.gov.uk/courts-tribunals/commercial-circuit-court"
    };

    /* Courts within the Chancery Division of the High Court -- all are specialist "Business and Property Courts" */

    public static readonly Court EWHC_Chancery_BusinessList = new Court {
        Code = "EWHC-Chancery-Business",
        LongName = "The Business and Property Courts (Business List)",
        ShortName = "The Business List",
        URL = "https://www.gov.uk/courts-tribunals/the-business-list"
    };

    public static readonly Court EWHC_Chancery_InsolvencyAndCompanies = new Court {
        Code = "EWHC-Chancery-InsolvencyAndCompanies",
        LongName = "The Business and Property Courts (Insolvency and Companies List )",
        ShortName = "The Insolvency and Companies List ",
        URL = "https://www.gov.uk/courts-tribunals/insolvency-list"
    };

    // public static readonly Court EWHC_Chancery_Financial = new Court {
    //     Code = "EWHC-Chancery-Financial",
    //     LongName = "Business and Property Courts (Financial List) (Chancery)",
    //     ShortName = "Financial List",
    //     URL = ""    // https://www.gov.uk/courts-tribunals/the-financial-list
    // };

    // public static readonly Court EWHC_Chancery_Competition = new Court {
    //     Code = "EWHC-Chancery-Competition",
    //     LongName = "Business and Property Courts (Competition List)",
    //     ShortName = "Competition List",
    //     URL = "https://www.gov.uk/courts-tribunals/the-competition-list"
    // };

    // public static readonly Court EWHC_Chancery_IntellectualProperty = new Court {
    //     Code = "EWHC-Chancery-IntellectualProperty",
    //     LongName = "Business and Property Courts (Intellectual Property List)",
    //     ShortName = "Intellectual Property List",
    //     URL = "https://www.gov.uk/courts-tribunals/the-intellectual-property-list"
    // };

    // public static readonly Court EWHC_Chancery_Revenue = new Court {
    //     Code = "EWHC-Chancery-Revenue",
    //     LongName = "Business and Property Courts (Revenue List)",
    //     ShortName = "Revenue List",
    //     URL = "https://www.gov.uk/courts-tribunals/the-revenue-list"
    // };

    // public static readonly Court EWHC_Chancery_PropertyTrustsProbate = new Court {
    //     Code = "EWHC-Chancery-PropertyTrustsProbate",
    //     LongName = "Business and Property Courts (Property, Trusts and Probate List)",
    //     ShortName = "Property, Trusts and Probate List",
    //     URL = ""
    // };

    public static readonly Court EWHC_Chancery_Patents = new Court {
        Code = "EWHC-Chancery-Patents",
        LongName = "The Business and Property Courts (Patents Court)",
        ShortName = "The Patents Court",
        URL = "https://www.gov.uk/courts-tribunals/patents-court"
    };

    public static readonly Court EWHC_Chancery_IPEC = new Court {
        Code = "EWHC-Chancery-IPEC",
        LongName = "The Business and Property Courts (Intellectual Property Enterprise Court)",
        ShortName = "The Intellectual Property Enterprise Court",
        URL = "https://www.gov.uk/courts-tribunals/intellectual-property-enterprise-court"
    };

    public static readonly Court EWHC_Chancery_Appeals = new Court {
        Code = "EWHC-Chancery-Appeals",
        LongName = "The Business and Property Courts (Chancery_Appeals)",
        ShortName = "The Patents Court",
        URL = "https://www.gov.uk/courts-tribunals/patents-court"
    };

    /* other */

    public static readonly Court EWHC_SeniorCourtsCosts = new Court {
        Code = "EWHC-SeniorCourtsCosts",
        LongName = "The England and Wales High Court (Senior Courts Costs Office)",
        ShortName = "The Senior Courts Costs Office",
        URL = "https://www.gov.uk/courts-tribunals/senior-courts-costs-office"
    };

    public static readonly Court EWCOP = new Court {
        Code = "EWCOP",
        LongName = "The Court of Protection",
        URL = "https://www.gov.uk/courts-tribunals/court-of-protection"
    };

    public static readonly Court EWFC = new Court {
        Code = "EWFC",
        LongName = "The Family Court",
        URL = "https://www.judiciary.uk/you-and-the-judiciary/going-to-court/family-law-courts/"
    };

    public static readonly Court EmploymentTribunal = new Court {
        Code = "ET",
        LongName = "The Employment Tribunal",
        // ShortName = "Employment Tribunal",
        URL = "https://www.gov.uk/courts-tribunals/employment-tribunal"
    };

    public static readonly Court[] All = {
        SupremeCourt,
        CoA_Crim,
        CoA_Civil,
        EWHC,
        EWHC_QBD,
        EWHC_Chancery,
        EWHC_Family,
        EWHC_QBD_General,
        EWHC_QBD_Administrative,
        EWHC_QBD_Planning,
        EWHC_QBD_BusinessAndProperty,
        EWHC_QBD_Commercial,
        EWHC_QBD_Admiralty,
        EWHC_QBD_TCC,
        EWHC_QBD_Commercial_Financial,
        EWHC_QBD_Commercial_Circuit,
        EWHC_Chancery_BusinessList,
        EWHC_Chancery_InsolvencyAndCompanies,
        // EWHC_Chancery_Financial,
        // EWHC_Chancery_Competition,
        // EWHC_Chancery_IntellectualProperty,
        // EWHC_Chancery_Revenue,
        // EWHC_Chancery_PropertyTrustsProbate,
        EWHC_Chancery_Patents,
        EWHC_Chancery_IPEC,
        EWHC_Chancery_Appeals,
        EWHC_SeniorCourtsCosts,
        EWCOP,
        EWFC,
        EmploymentTribunal
    };

    public static readonly ImmutableDictionary<string, Court> ByCode =
        new Dictionary<string, Court>(All.Select(c => new KeyValuePair<string, Court>(c.Code, c)))
        .ToImmutableDictionary();

}

}

