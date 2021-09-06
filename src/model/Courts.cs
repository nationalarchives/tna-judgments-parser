
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

    public static readonly Court CoA_Crim = new Court {
        Code = "EWCA-Criminal",
        LongName = "Court of Appeal Criminal Division",
        // ShortName = "Court of Appeal Criminal Division",
        URL = "https://www.gov.uk/courts-tribunals/court-of-appeal-criminal-division"
    };
    public static readonly Court CoA_Civil = new Court {
        Code = "EWCA-Civil",
        LongName = "Court of Appeal Civil Division",
        // ShortName = "Court of Appeal Criminal Division",
        URL = "https://www.gov.uk/courts-tribunals/court-of-appeal-civil-division"
    };
    
    // public static readonly Court EWHC = new Court {
    //     Code = "EWHC",
    //     Name = "Queen’s Bench Division of the High Court of Justice",
    //     URL = "https://www.gov.uk/courts-tribunals/queens-bench-division-of-the-high-court"
    // };
    public static readonly Court EWHC_QBD = new Court {
        Code = "EWHC-QBD",
        LongName = "Queen's Bench Division of the High Court",
        ShortName = "Queen's Bench Division",
        URL = "https://www.gov.uk/courts-tribunals/queens-bench-division-of-the-high-court"
    };
    public static readonly Court EWHC_QBD_Admin = new Court {
        Code = "EWHC-QBD-Admin",
        LongName = "Queen's Bench Division of the High Court (Administrative Court)",
        ShortName = "Administrative Court",
        URL = "https://www.gov.uk/courts-tribunals/administrative-court"
    };
    public static readonly Court EWHC_QBD_Admin_Planning = new Court {
        Code = "EWHC-QBD-Admin-Planning",
        LongName = "Queen's Bench Division of the High Court (Planning Court)",
        ShortName = "Planning Court",
        URL = "https://www.gov.uk/courts-tribunals/planning-court"
    };
    public static readonly Court EWHC_QBD_Circuit_Commercial_Court = new Court {
        Code = "EWHC-QBD-Circuit-Commercial",
        LongName = "Queen's Bench Division of the High Court (Circuit Commercial Court)",
        ShortName = "Circuit Commercial Court",
        URL = "https://www.gov.uk/courts-tribunals/commercial-circuit-court"
    };

    public static readonly Court EWHC_QBD_Chancery = new Court {
        Code = "EWHC-Chancery",
        LongName = "Chancery Division of the High Court",
        ShortName = "Chancery Division",
        URL = "https://www.gov.uk/courts-tribunals/chancery-division-of-the-high-court"
    };

    public static readonly Court HC_Chancery_BusAndProp_BusinessList = new Court {
        Code = "EWHC-Business",
        LongName = "Business and Property Courts (Chancery Division) (Business List)",
        ShortName = "Business List",
        URL = "https://www.gov.uk/courts-tribunals/the-business-list"
    };

    public static readonly Court EWHC_Commercial = new Court {
        Code = "EWHC-Commercial",
        LongName = "Business and Property Courts (Commercial Court)",
        ShortName = "Commercial Court",
        URL = "https://www.gov.uk/courts-tribunals/commercial-court"
    };

    public static readonly Court EWHC_Chancery_Patents = new Court {
        Code = "EWHC-Patents",
        LongName = "Business and Property Courts (Chancery Division) (Patents Court)",
        ShortName = "Patents Court",
        URL = "https://www.gov.uk/courts-tribunals/patents-court"
    };

    public static readonly Court EWHC_QBD_TCC = new Court {
        Code = "EWHC-QBD-TCC",
        LongName = "Queen's Bench Division of the High Court / Technology and Construction Court",
        ShortName = "Technology and Construction Court",
        URL = "https://www.gov.uk/courts-tribunals/technology-and-construction-court"
    };

    public static readonly Court EWHC_QBD_Costs = new Court {
        Code = "EWHC-Costs",
        LongName = "England and Wales High Court (Senior Courts Costs Office)",
        ShortName = "Senior Courts Costs Office",
        URL = "https://www.gov.uk/courts-tribunals/senior-courts-costs-office"
    };

    public static readonly Court EWCOP = new Court {
        Code = "EWCOP",
        LongName = "Court of Protection",
        URL = "https://www.gov.uk/courts-tribunals/court-of-protection"
    };

    public static readonly Court EWFC = new Court {
        Code = "EWFC",
        LongName = "Family Court",
        URL = "https://www.gov.uk/courts-tribunals/family-division-of-the-high-court"
    };

    public static readonly Court EmploymentTribunal = new Court {
        Code = "ET",
        LongName = "Employment Tribunal",
        // ShortName = "Employment Tribunal",
        URL = "https://www.gov.uk/courts-tribunals/employment-tribunal"
    };

    public static readonly Court[] All = {
        
        CoA_Crim,
        CoA_Civil,

        EWHC_QBD,
        EWHC_QBD_Admin,
        EWHC_QBD_Admin_Planning,
        EWHC_QBD_Circuit_Commercial_Court,

        EWHC_QBD_Chancery,
        HC_Chancery_BusAndProp_BusinessList,
        EWHC_Commercial,
        EWHC_Chancery_Patents,
        EWHC_QBD_TCC,
        EWHC_QBD_Costs,

        EWCOP,
        EWFC,

        EmploymentTribunal
    };

    public static readonly ImmutableDictionary<string, Court> ByCode =
        new Dictionary<string, Court>(All.Select(c => new KeyValuePair<string, Court>(c.Code, c)))
        .ToImmutableDictionary();

}

}

// public readonly struct Counts

// }

// class CourtsOld {

//     public static readonly ImmutableDictionary<string, Court> courts = new Dictionary<string, Court> {

//         { "UKSC", new Court{
//             Name = "Supreme Court",
//             URL = "https://www.supremecourt.uk/" } 
//         },
//         { "UKHL", new Court{
//             Name = "House of Lords",
//             URL = "https://www.parliament.uk/business/lords/" }
//         },
//         { "EWCA-Civ", new Court{ Name = "Court of Appeal (Civil Division)" }
//         },
//         { "EWCA-Crim", new Court{ Name = "Court of Appeal (Criminal Division)" }
//         },

//         { "EWHC-Ch", new Court{ Name = "High Court of Justice (Chancery Division)" }
//         },
//         { "EWHC-QB", new Court{ Name = "High Court of Justice (Queen’s Bench Division)" }
//         },
//         { "EWHC-Admin", new Court{ Name = "High Court of Justice (Administrative Court)" }
//         },
//         { "EWHC-Comm", new Court{ Name = "High Court of Justice (Commercial Court)" }
//         },

//         {
//             "ETEW",
//         }

//         {
//             "ETS"
//         }
        
//     }.ToImmutableDictionary();

// }
