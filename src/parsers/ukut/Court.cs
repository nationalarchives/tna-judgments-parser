
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class CourtType : AbstractCourtType {

    protected override IEnumerable<Combo3> Combo3s() {
        return new List<Combo3>(1) {
            new Combo3 {
                Re1 = new Regex(@"^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
                Re2 = new Regex(@"^ADMINISTRATIVE APPEALS CHAMBER$", RegexOptions.IgnoreCase),
                Re3 = new Regex(@"^\(TRAFFIC COMMISSIONER APPEALS\)$", RegexOptions.IgnoreCase),
                Court = Courts.UpperTribunal_AdministrativeAppealsChamber
            }
        };
    }

    protected override IEnumerable<Combo2> Combo2s() {
        return new List<Combo2>(4) {
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
            },
            new Combo2 {
                Re1 = new Regex(@"^FIRST-TIER TRIBUNAL$", RegexOptions.IgnoreCase),
                Re2 = new Regex(@"^TAX CHAMBER$", RegexOptions.IgnoreCase),
                Court = Courts.FirstTierTribunal_Tax
            }
        };
    }

    protected override IEnumerable<Combo1> Combo1s() {
        return new List<Combo1>(3) {
            new Combo1 {
                Re = new Regex(@"^(IN THE )?UPPER TRIBUNAL \(IMMIGRATION (AND|&) ASYLUM CHAMBER\)$", RegexOptions.IgnoreCase),
                Court = Courts.UpperTribunal_ImmigrationAndAsylumChamber
            },
            new Combo1 {
                Re = new Regex(@"^(IN THE )?UPPER TRIBUNAL \(LANDS CHAMBER\)$", RegexOptions.IgnoreCase),
                Court = Courts.UpperTribunal_LandsChamber
            },
            new Combo1 {
                Re = new Regex(@"^FIRST-TIER TRIBUNAL TAX CHAMBER$", RegexOptions.IgnoreCase),
                Court = Courts.FirstTierTribunal_Tax
            }
        };
    }

}

}
