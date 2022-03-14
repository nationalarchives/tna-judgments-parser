
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class CourtType : AbstractCourtType {

    protected override IEnumerable<Combo2> Combo2s() {
        return new List<Combo2>(2) {
            new Combo2 {
                Re1 = new Regex(@"^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
                Re2 = new Regex(@"^\(?IMMIGRATION (AND|&) ASYLUM CHAMBER\)?$", RegexOptions.IgnoreCase),
                Court = Courts.UpperTribunal_ImmigrationAndAsylumChamber
            },
            new Combo2 {
                Re1 = new Regex(@"^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
                Re2 = new Regex(@"^\(?TAX (AND|&) CHANCERY CHAMBER\)?$", RegexOptions.IgnoreCase),
                Court = Courts.UpperTribunal_TaxAndChanceryChamber
            },
            new Combo2 {
                Re1 = new Regex(@"^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
                Re2 = new Regex(@"^\(?ADMINISTRATIVE APPEALS CHAMBER\)?$", RegexOptions.IgnoreCase),
                Court = Courts.UpperTribunal_TaxAndChanceryChamber
            }
        };
    }

    protected override IEnumerable<Combo1> Combo1s() {
        return new List<Combo1>(1) {
            new Combo1 {
                Re = new Regex(@"^(IN THE )?UPPER TRIBUNAL \(LANDS CHAMBER\)$", RegexOptions.IgnoreCase),
                Court = Courts.UpperTribunal_LandsChamber
            }
        };
    }

}

}
