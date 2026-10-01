#nullable enable

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

public partial class Courts()
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

    public static readonly Court SupremeCourt = GetByCode("UKSC");

    public static readonly Court PrivyCouncil = GetByCode("UKPC");

    public static readonly Court CoA_Crim = GetByCode("EWCA-Criminal");
    public static readonly Court CoA_Civil = GetByCode("EWCA-Civil");

    /* The High Court */

    public static readonly Court EWHC = GetByCode("EWHC");

    /* The three Divisions of the High Court: Kings's/Queen's Bench, Chancery and Family */

    public static readonly Court EWHC_KBD = GetByCode("EWHC-KBD");
    public static readonly Court EWHC_QBD = GetByCode("EWHC-QBD");

    public const string EwhcChanceryCourtCode = "EWHC-Chancery";
    public const string EwhcFamilyCourtCode = "EWHC-Family";

    public static readonly Court EWHC_Chancery = GetByCode(EwhcChanceryCourtCode);
    public static readonly Court EWHC_Family = GetByCode(EwhcFamilyCourtCode);

    /* The four courts (non-specialist) within the Queen's Bench Division */

    public const string EwhcQbdPlanningCourtCode = "EWHC-QBD-Planning";
    public const string EwhcKbdAdminCourtCode = "EWHC-KBD-Admin";
    public const string EwhcQbdAdminCourtCode = "EWHC-QBD-Admin";

    public static readonly Court EWHC_QBD_Administrative = GetByCode(EwhcQbdAdminCourtCode);
    public static readonly Court EWHC_QBD_Planning = GetByCode(EwhcQbdPlanningCourtCode);


    /* "Specialist" Business and Property Courts within the Queen's Bench Division */

    public const string EwhcKbdAdmiraltyCourtCode = "EWHC-KBD-Admiralty";
    public const string EwhcKbdBusinessAndPropertyCourtCode = "EWHC-KBD-BusinessAndProperty";
    public const string EwhcKbdCommercialCircuitCourtCode = "EWHC-KBD-Commercial-Circuit";
    public const string EwhcKbdCommercialCourtCode = "EWHC-KBD-Commercial";
    public const string EwhcKbdCommercialFinancialCourtCode = "EWHC-KBD-Commercial-Financial";
    public const string EwhcKbdTccCourtCode = "EWHC-KBD-TCC";

    public const string EwhcQbdAdmiraltyCourtCode = "EWHC-QBD-Admiralty";
    public const string EwhcQbdBusinessAndPropertyCourtCode = "EWHC-QBD-BusinessAndProperty";
    public const string EwhcQbdCommercialCircuitCourtCode = "EWHC-QBD-Commercial-Circuit";
    public const string EwhcQbdCommercialCourtCode = "EWHC-QBD-Commercial";
    public const string EwhcQbdCommercialFinancialCourtCode = "EWHC-QBD-Commercial-Financial";
    public const string EwhcQbdTccCourtCode = "EWHC-QBD-TCC";

    public static readonly Court EWHC_QBD_BusinessAndProperty = GetByCode(EwhcQbdBusinessAndPropertyCourtCode);
    public static readonly Court EWHC_QBD_Commercial = GetByCode(EwhcQbdCommercialCourtCode);
    public static readonly Court EWHC_QBD_Admiralty = GetByCode(EwhcQbdAdmiraltyCourtCode);
    public static readonly Court EWHC_QBD_TCC = GetByCode(EwhcQbdTccCourtCode);
    public static readonly Court EWHC_QBD_Commercial_Financial = GetByCode(EwhcQbdCommercialFinancialCourtCode);
    public static readonly Court EWHC_QBD_Commercial_Circuit = GetByCode(EwhcQbdCommercialCircuitCourtCode);

    /* Courts within the Chancery Division of the High Court -- all are specialist "Business and Property Courts" */

    public const string EwhcChanceryAppealsCourtCode = "EWHC-Chancery-Appeals";
    public const string EwhcChanceryBusinessAndPropertyCourtCode = "EWHC-Chancery-BusinessAndProperty";
    public const string EwhcChanceryBusinessCourtCode = "EWHC-Chancery-Business";
    public const string EwhcChanceryFinancialCourtCode = "EWHC-Chancery-Financial";
    public const string EwhcChanceryInsolvencyAndCompaniesCourtCode = "EWHC-Chancery-InsolvencyAndCompanies";
    public const string EwhcChanceryIntellectualPropertyCourtCode = "EWHC-Chancery-IntellectualProperty";
    public const string EwhcChanceryIpecCourtCode = "EWHC-Chancery-IPEC";
    public const string EwhcChanceryPatentsCourtCode = "EWHC-Chancery-Patents";
    public const string EwhcChanceryPropertyTrustsProbateCourtCode = "EWHC-Chancery-PropertyTrustsProbate";

    public static readonly Court
        EWHC_Chancery_BusinessAndProperty = GetByCode(EwhcChanceryBusinessAndPropertyCourtCode);

    public static readonly Court EWHC_Chancery_BusinessList = GetByCode(EwhcChanceryBusinessCourtCode);

    public static readonly Court EWHC_Chancery_InsolvencyAndCompanies =
        GetByCode(EwhcChanceryInsolvencyAndCompaniesCourtCode);

    public static readonly Court EWHC_Chancery_Financial = GetByCode(EwhcChanceryFinancialCourtCode);

    public static readonly Court EWHC_Chancery_IntellectualProperty =
        GetByCode(EwhcChanceryIntellectualPropertyCourtCode);

    public static readonly Court EWHC_Chancery_PropertyTrustsProbate =
        GetByCode(EwhcChanceryPropertyTrustsProbateCourtCode);

    public static readonly Court EWHC_Chancery_Patents = GetByCode(EwhcChanceryPatentsCourtCode);
    public static readonly Court EWHC_Chancery_IPEC = GetByCode(EwhcChanceryIpecCourtCode);
    public static readonly Court EWHC_Chancery_Appeals = GetByCode(EwhcChanceryAppealsCourtCode);

    public static readonly Court EWHC_SeniorCourtsCosts = GetByCode("EWHC-SeniorCourtsCosts");


    /* Business and Property Division */

    public const string EwhcBpdAdmiraltyCourtCode = "EWHC-BPD-Admiralty";
    public const string EwhcBpdAppealsCourtCode = "EWHC-BPD-Appeals";
    public const string EwhcBpdBusinessCourtCode = "EWHC-BPD-Business";
    public const string EwhcBpdCommercialCircuitCourtCode = "EWHC-BPD-Commercial-Circuit";
    public const string EwhcBpdCommercialCourtCode = "EWHC-BPD-Commercial";
    public const string EwhcBpdCommercialFinancialCourtCode = "EWHC-BPD-Commercial-Financial";
    public const string EwhcBpdCompetitionCourtCode = "EWHC-BPD-Competition";
    public const string EwhcBpdCourtCode = "EWHC-BPD";
    public const string EwhcBpdInsolvencyAndCompaniesCourtCode = "EWHC-BPD-InsolvencyAndCompanies";
    public const string EwhcBpdIntellectualPropertyCourtCode = "EWHC-BPD-IntellectualProperty";
    public const string EwhcBpdIpecCourtCode = "EWHC-BPD-IPEC";
    public const string EwhcBpdPatentsCourtCode = "EWHC-BPD-Patents";
    public const string EwhcBpdPropertyTrustsProbateCourtCode = "EWHC-BPD-PropertyTrustsProbate";
    public const string EwhcBpdRevenueCourtCode = "EWHC-BPD-Revenue";
    public const string EwhcBpdTccCourtCode = "EWHC-BPD-TCC";

    public static readonly Court EwhcBpdAdmiralty = GetByCode(EwhcBpdAdmiraltyCourtCode);
    public static readonly Court EwhcBpdAppeals = GetByCode(EwhcBpdAppealsCourtCode);
    public static readonly Court EwhcBpdBusiness = GetByCode(EwhcBpdBusinessCourtCode);
    public static readonly Court EwhcBpdCommercialCircuit = GetByCode(EwhcBpdCommercialCircuitCourtCode);
    public static readonly Court EwhcBpdCommercial = GetByCode(EwhcBpdCommercialCourtCode);
    public static readonly Court EwhcBpdCommercialFinancial = GetByCode(EwhcBpdCommercialFinancialCourtCode);
    public static readonly Court EwhcBpdCompetition = GetByCode(EwhcBpdCompetitionCourtCode);
    public static readonly Court EwhcBpd = GetByCode(EwhcBpdCourtCode);
    public static readonly Court EwhcBpdInsolvencyAndCompanies = GetByCode(EwhcBpdInsolvencyAndCompaniesCourtCode);
    public static readonly Court EwhcBpdIntellectualProperty = GetByCode(EwhcBpdIntellectualPropertyCourtCode);
    public static readonly Court EwhcBpdIpec = GetByCode(EwhcBpdIpecCourtCode);
    public static readonly Court EwhcBpdPatents = GetByCode(EwhcBpdPatentsCourtCode);
    public static readonly Court EwhcBpdPropertyTrustsProbate = GetByCode(EwhcBpdPropertyTrustsProbateCourtCode);
    public static readonly Court EwhcBpdRevenue = GetByCode(EwhcBpdRevenueCourtCode);
    public static readonly Court EwhcBpdTcc = GetByCode(EwhcBpdTccCourtCode);

    /* other courts */

    public const string EwcopCourtCode = "EWCOP";
    public static readonly Court EWCOP = GetByCode(EwcopCourtCode);
    public static readonly Court EWCOP_T1 = GetByCode("EWCOP-T1");
    public static readonly Court EWCOP_T2 = GetByCode("EWCOP-T2");
    public static readonly Court EWCOP_T3 = GetByCode("EWCOP-T3");

    public const string EwfcCourtCode = "EWFC";
    public static readonly Court EWFC = GetByCode(EwfcCourtCode);
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
    public static readonly Court FirstTierTribunal_ImmigrationAndAsylum = GetByCode("UKFTT-IAC");

    public static readonly Court FirstTierTribunal_GRC = GetByCode("UKFTT-GRC");

    public static readonly Court FirstTierTribunal_PropertyChamber = GetByCode("FTT-PC");

    public static readonly Court InvestigatoryPowersTribunal = GetByCode("UKIPT");

    public const string FirstTierTribunalChamberCodesPattern = "TC|GRC|PC";
    public const string UpperTribunalChamberCodesPattern = "AAC|IAC|LC|TCC";
    public const string EwhcCodesPattern = "Admin|Admlty|BP|Ch|Comm|Costs|Fam|IPEC|KB|Pat|QB|SCCO|TCC";

    [GeneratedRegex(@"^(IN THE )?First-tier Tribunal$", RegexOptions.IgnoreCase, "en-GB")]
    public static partial Regex FirstTierTribunalIdentifierRegex();
}
