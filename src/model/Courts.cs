#nullable enable

using System.Linq;
using System.Text.RegularExpressions;

using NationalArchives.FindCaseLaw.Utils;

using FclCourt = NationalArchives.FindCaseLaw.Utils.Court;

namespace UK.Gov.Legislation.Judgments;

public record Court
{
    private readonly FclCourt fclCourt;

    internal Court(FclCourt fclCourt)
    {
        this.fclCourt = fclCourt;
    }

    public string Code => fclCourt.Code;
    public string Name => fclCourt.LongName ?? fclCourt.Name;
    public string URL => fclCourt.IdentifierIri;
    public Regex? CitationPattern => fclCourt.NcnPattern is not null ? new Regex(fclCourt.NcnPattern) : null;
}

public static class Courts
{
    private static readonly CourtStore CourtStore = new();

    public static bool Exists(string courtCode)
    {
        return CourtStore.Exists(courtCode);
    }

    public static Court GetByCode(string courtCode)
    {
        return new Court(CourtStore.Get(courtCode));
    }

    public static Court? ExtractFromCitation(string cite)
    {
        var cleanedCite = cite.CleanWhitespace();

        if (string.IsNullOrWhiteSpace(cleanedCite))
        {
            return null;
        }

        FclCourt? fclCourt = CourtStore.Where(c => c.NcnPattern is not null
                                                   && Regex.IsMatch(cleanedCite, RegexHelpers.AddAnchors(c.NcnPattern)))
                                       .FirstOrDefault();

        return fclCourt is null ? null : new Court(fclCourt.Value);
    }

    public static readonly Court SupremeCourt = GetByCode("UKSC");

    public static readonly Court PrivyCouncil = GetByCode("UKPC");

    public static readonly Court CoA_Crim = GetByCode("EWCA-Criminal");
    public static readonly Court CoA_Civil = GetByCode("EWCA-Civil");

    /* The High Court */

    public static readonly Court EWHC = GetByCode("EWHC");

    /* The three Divisions of the High Court: Kings's/Queen's Bench, Chancery and Family */

    public static readonly Court EWHC_KBD = GetByCode("EWHC-KBD");
    public static readonly Court EWHC_QBD = GetByCode("EWHC-QBD");

    public static readonly Court EWHC_Chancery = GetByCode("EWHC-Chancery");

    public static readonly Court EWHC_Family = GetByCode("EWHC-Family");

    /* The four courts (non-specialist) within the Queen's Bench Division */

    public static readonly Court EWHC_QBD_Administrative = GetByCode("EWHC-QBD-Admin");
    public static readonly Court EWHC_QBD_Planning = GetByCode("EWHC-QBD-Planning");
    public static readonly Court EWHC_QBD_BusinessAndProperty = GetByCode("EWHC-QBD-BusinessAndProperty");

    /* "Specialist" Business and Property Courts within the Queen's Bench Division */

    public static readonly Court EWHC_QBD_Commercial = GetByCode("EWHC-QBD-Commercial");

    public static readonly Court EWHC_QBD_Admiralty = GetByCode("EWHC-QBD-Admiralty");

    public static readonly Court EWHC_QBD_TCC = GetByCode("EWHC-QBD-TCC");

    public static readonly Court EWHC_QBD_Commercial_Financial = GetByCode("EWHC-QBD-Commercial-Financial");

    public static readonly Court EWHC_QBD_Commercial_Circuit = GetByCode("EWHC-QBD-Commercial-Circuit");

    /* Courts within the Chancery Division of the High Court -- all are specialist "Business and Property Courts" */

    public static readonly Court EWHC_Chancery_BusinessAndProperty = GetByCode("EWHC-Chancery-BusinessAndProperty");

    public static readonly Court EWHC_Chancery_BusinessList = GetByCode("EWHC-Chancery-Business");

    public static readonly Court EWHC_Chancery_InsolvencyAndCompanies =
        GetByCode("EWHC-Chancery-InsolvencyAndCompanies");

    public static readonly Court EWHC_Chancery_Financial = GetByCode("EWHC-Chancery-Financial");

    public static readonly Court EWHC_Chancery_IntellectualProperty = GetByCode("EWHC-Chancery-IntellectualProperty");

    public static readonly Court EWHC_Chancery_PropertyTrustsProbate = GetByCode("EWHC-Chancery-PropertyTrustsProbate");

    public static readonly Court EWHC_Chancery_Patents = GetByCode("EWHC-Chancery-Patents");

    public static readonly Court EWHC_Chancery_IPEC = GetByCode("EWHC-Chancery-IPEC");

    public static readonly Court EWHC_Chancery_Appeals = GetByCode("EWHC-Chancery-Appeals");

    public static readonly Court EWHC_SeniorCourtsCosts = GetByCode("EWHC-SeniorCourtsCosts");

    /* other courts */

    public static readonly Court EWCOP = GetByCode("EWCOP");
    public static readonly Court EWCOP_T1 = GetByCode("EWCOP-T1");
    public static readonly Court EWCOP_T2 = GetByCode("EWCOP-T2");
    public static readonly Court EWCOP_T3 = GetByCode("EWCOP-T3");

    public static readonly Court EWFC = GetByCode("EWFC");
    public static readonly Court EWFC_B = GetByCode("EWFC-B");

    public static readonly Court EWCC = GetByCode("EWCC");

    public static readonly Court EWCR = GetByCode("EWCR");

    /* tribunals */

    public static readonly Court UpperTribunal_AdministrativeAppealsChamber = GetByCode("UKUT-AAC");

    public static readonly Court OldAsylumAndImmigrationTribunal = GetByCode("UKAIT");

    public static readonly Court UpperTribunal_ImmigrationAndAsylumChamber = GetByCode("UKUT-IAC");

    public static readonly Court UpperTribunal_LandsChamber = GetByCode("UKUT-LC");
    public static readonly Court UpperTribunal_TaxAndChanceryChamber = GetByCode("UKUT-TCC");

    public static readonly Court EmploymentAppealTribunal = GetByCode("EAT");

    public static readonly Court FirstTierTribunal_Tax = GetByCode("UKFTT-TC");

    public static readonly Court FirstTierTribunal_GRC = GetByCode("UKFTT-GRC");

    public static readonly Court FirstTierTribunal_PropertyChamber = GetByCode("FTT-PC");

    public static readonly Court InvestigatoryPowersTribunal = GetByCode("UKIPT");

    public const string FirstTierTribunalChamberCodesPattern = "TC|GRC|PC";
    public const string UpperTribunalChamberCodesPattern = "AAC|IAC|LC|TCC";
}
