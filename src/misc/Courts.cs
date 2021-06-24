
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace UK.Gov.Legislation.Judgments {

public readonly struct Court {

    public string Code { get; init; }
    public string Name { get; init; }
    public string URL { get; init; }

}

public readonly struct Courts {

    public static readonly Court EmploymentTribunal = new Court {
        Code = "ET",
        Name = "Employment Tribunal",
        URL = "https://www.gov.uk/courts-tribunals/employment-tribunal"
    };

    public static readonly Court[] All = {
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
