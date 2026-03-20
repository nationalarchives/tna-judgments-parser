#nullable enable

using System.Collections.Generic;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

internal class CourtType : AbstractCourtType
{
    protected override IEnumerable<Combo3> Combo3s { get; } =
    [
        new()
        {
            Re1 = new Regex("^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^ADMINISTRATIVE APPEALS CHAMBER$", RegexOptions.IgnoreCase),
            Re3 = new Regex(@"^\(TRAFFIC COMMISSIONER APPEALS\)$", RegexOptions.IgnoreCase),
            Court = Courts.UpperTribunal_AdministrativeAppealsChamber
        },

        new GRCCombo()
    ];
    
    protected override IEnumerable<Combo2> Combo2s { get; } =
    [
        new()
        {
            Re1 = new Regex("^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^\(?IMMIGRATION (AND|&) ASYLUM CHAMBER\)?$", RegexOptions.IgnoreCase),
            Court = Courts.UpperTribunal_ImmigrationAndAsylumChamber
        },

        new()
        {
            Re1 = new Regex("^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^\(?TAX (AND|&) CHANCERY CHAMBER\)?$", RegexOptions.IgnoreCase),
            Court = Courts.UpperTribunal_TaxAndChanceryChamber
        },

        new()
        {
            Re1 = new Regex("^(IN THE )?UPPER TRIBUNAL$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^\(?ADMINISTRATIVE APPEALS CHAMBER\)?$", RegexOptions.IgnoreCase),
            Court = Courts.UpperTribunal_AdministrativeAppealsChamber
        },

        new()
        {
            Re1 = new Regex("^FIRST-TIER TRIBUNAL$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^TAX CHAMBER$", RegexOptions.IgnoreCase),
            Court = Courts.FirstTierTribunal_Tax
        },

        new()
        {
            Re1 = new Regex("^First-tier Tribunal$", RegexOptions.IgnoreCase),
            Re2 = new Regex(@"^\(?General Regulatory Chamber\)?$", RegexOptions.IgnoreCase),
            Court = Courts.FirstTierTribunal_GRC
        },

        new()
        {
            Re1 = new Regex("^PROPERTY CHAMBER, LAND REGISTRATION$", RegexOptions.IgnoreCase),
            Re2 = new Regex("^FIRST-TIER TRIBUNAL$", RegexOptions.IgnoreCase),
            Court = Courts.FirstTierTribunal_PropertyChamber
        }
    ];

    protected override IEnumerable<Combo1> Combo1s { get; } =
    [
        new()
        {
            Re = new Regex(@"^(IN THE )?UPPER TRIBUNAL \(IMMIGRATION (AND|&) ASYLUM CHAMBER\)$",
                RegexOptions.IgnoreCase),
            Court = Courts.UpperTribunal_ImmigrationAndAsylumChamber
        },

        new()
        {
            Re = new Regex("^Asylum and Immigration Tribunal$", RegexOptions.IgnoreCase),
            Court = Courts.OldAsylumAndImmigrationTribunal
        },

        new()
        {
            Re = new Regex(@"^(IN THE )?UPPER TRIBUNAL \(LANDS CHAMBER\)$", RegexOptions.IgnoreCase),
            Court = Courts.UpperTribunal_LandsChamber
        },

        new()
        {
            Re = new Regex("^FIRST-TIER TRIBUNAL TAX CHAMBER$", RegexOptions.IgnoreCase),
            Court = Courts.FirstTierTribunal_Tax
        }
    ];

}
