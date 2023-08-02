
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
            },
            new GRCCombo3("Charity") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Claims") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Community right to bid") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Consultant lobbyists") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Conveyancing") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Copyright licensing") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Electronic communications and postal services") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Environment") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Estate agent") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Examination boards") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Food labelling") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Gambling Appeals") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Immigration services") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Individual Electronic") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Information rights") {
                Court = Courts.FirstTierTribunal_GRC_InformationRights
            },
            new GRCCombo3("Letting and managing agents") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Local government standards") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Pensions Regulation") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Secondary ticketing") {
                Court = Courts.FirstTierTribunal_GRC
            },
            new GRCCombo3("Welfare of animals") {
                Court = Courts.FirstTierTribunal_GRC
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
                Court = Courts.UpperTribunal_AdministrativeAppealsChamber
            },
            new Combo2 {
                Re1 = new Regex(@"^FIRST-TIER TRIBUNAL$", RegexOptions.IgnoreCase),
                Re2 = new Regex(@"^TAX CHAMBER$", RegexOptions.IgnoreCase),
                Court = Courts.FirstTierTribunal_Tax
            },
            new Combo2 {
                Re1 = new Regex(@"^First-tier Tribunal$", RegexOptions.IgnoreCase),
                Re2 = new Regex(@"^\(?General Regulatory Chamber\)?$", RegexOptions.IgnoreCase),
                Court = Courts.FirstTierTribunal_GRC
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

class GRCCombo3 : Combo3 {

    internal GRCCombo3(string name) {
        Re1 = new Regex(@"^First-tier Tribunal$", RegexOptions.IgnoreCase);
        Re2 = new Regex(@"^General Regulatory Chamber$", RegexOptions.IgnoreCase);
        Re3 = new Regex(@"^\[?" + name + @"\]?$", RegexOptions.IgnoreCase);
    }

}

}
