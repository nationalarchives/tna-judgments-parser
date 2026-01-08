#nullable enable
using System;
using System.Collections.Generic;
namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Every legislative document has a document "name".
/// </summary>
/// <remarks>
/// Not to be confused with the title. A name is the broader category of document.
/// e.g. "nipubb" is an Northern Ireland Public Bill<br/>
/// </remarks>
public enum DocName
{
    // enacted
    NIA,
    UKPGA,
    UKCM,
    ASP,
    NISI,
    NISR,
    UKSI,
    SSI,
    UKPUBB,
    UKPRIB,
    UKHYBB,
    UKDCM,
    UKDSI,
    SPPUBB,
    SPPRIB,
    SPHYBB,
    SDSI,
    NIPUBB,
    NIDSI,
    NIDSR,
    WSI,
    WDSI,
    ASC,
    SCPUBB,
    SCPRIB,
    SCHYBB
}

public static class DocNames
{
    public static DocName? GetDocName(string documentName)
    {
        if (String.IsNullOrEmpty(documentName))
        {
            return null;
        }
        DocName docName;
        if (Enum.TryParse<DocName>(documentName.ToUpper(), out docName))
            return docName;
        else
            throw new Exception("unrecognized document type: " + documentName);
    }

    public static DocName ToEnacted(this DocName docName)
    {
        // C# will never be happy with exhaustive switch statements because you can always pass (DocName)20
        return docName switch
        {
            DocName.NIA => DocName.NIA,
            DocName.NIPUBB => DocName.NIA,

            DocName.NISI => DocName.NISI,
            DocName.NIDSI => DocName.NISI,
            DocName.NISR => DocName.NISR,
            DocName.NIDSR => DocName.NISR,

            DocName.UKPGA => DocName.UKPGA,
            DocName.UKPUBB => DocName.UKPGA,
            DocName.UKPRIB => DocName.UKPGA,
            DocName.UKHYBB => DocName.UKPGA,

            DocName.UKDCM => DocName.UKCM,
            DocName.UKCM => DocName.UKCM,

            DocName.UKSI => DocName.UKSI,
            DocName.UKDSI => DocName.UKSI,

            DocName.ASP => DocName.ASP,
            DocName.SPPUBB => DocName.ASP,
            DocName.SPPRIB => DocName.ASP,
            DocName.SPHYBB => DocName.ASP,
            
            DocName.ASC => DocName.ASC,
            DocName.SCPUBB => DocName.ASC,
            DocName.SCPRIB => DocName.ASC,
            DocName.SCHYBB => DocName.ASC,

            DocName.SSI => DocName.SSI,
            DocName.SDSI => DocName.SSI,

            DocName.WSI => DocName.WSI,
            DocName.WDSI => DocName.WSI,
        };
    }

    public static bool IsEnacted(this DocName docName) =>
        docName == docName.ToEnacted();

    public static LegislationType GetLegislationType(this DocName docName)
    {
        return docName switch
        {
            DocName.NIA =>             LegislationType.PRIMARY,
            DocName.NIPUBB =>          LegislationType.PRIMARY,
            DocName.UKPGA =>           LegislationType.PRIMARY,
            DocName.UKPUBB =>          LegislationType.PRIMARY,
            DocName.UKPRIB =>          LegislationType.PRIMARY,
            DocName.UKHYBB =>          LegislationType.PRIMARY,
            DocName.UKCM =>            LegislationType.PRIMARY,
            DocName.UKDCM =>           LegislationType.PRIMARY,
            DocName.ASP =>             LegislationType.PRIMARY,
            DocName.SPPUBB =>          LegislationType.PRIMARY,
            DocName.SPPRIB =>          LegislationType.PRIMARY,
            DocName.SPHYBB =>          LegislationType.PRIMARY,
            DocName.ASC =>             LegislationType.PRIMARY,
            DocName.SCPUBB =>          LegislationType.PRIMARY,
            DocName.SCPRIB =>          LegislationType.PRIMARY,
            DocName.SCHYBB =>          LegislationType.PRIMARY,

            DocName.NISI =>            LegislationType.SECONDARY,
            DocName.NIDSI =>           LegislationType.SECONDARY,
            DocName.NISR =>            LegislationType.SECONDARY,
            DocName.NIDSR =>           LegislationType.SECONDARY,
            DocName.UKSI =>            LegislationType.SECONDARY,
            DocName.UKDSI =>           LegislationType.SECONDARY,
            DocName.SSI =>             LegislationType.SECONDARY,
            DocName.SDSI =>            LegislationType.SECONDARY,
            DocName.WSI =>             LegislationType.SECONDARY,
            DocName.WDSI =>            LegislationType.SECONDARY,
        };
    }



    public static bool IsSecondaryDocName(this DocName docName)
    {
        return docName.GetLegislationType() == LegislationType.SECONDARY;
    }

    public static bool IsScottishPrimary(this DocName docName)
    {
        return docName.ToEnacted().Equals(DocName.ASP);
    }
    
    public static bool IsUKPrimary(this DocName docName)
    {
        return docName.ToEnacted().Equals(DocName.UKPGA) || docName.ToEnacted().Equals(DocName.UKCM);
    }
    
    public static bool IsWelshPrimary(this DocName docName)
    {
        return docName.ToEnacted().Equals(DocName.ASC);
    }

    public static bool IsWelshSecondary(this DocName docName)
    {
        return docName.ToEnacted().Equals(DocName.WSI);
    }
    
    public static bool RequireNumberedProv1Heading(this DocName docName)
    {
        return docName.IsScottishPrimary() || docName.IsUKPrimary() || docName.IsWelshPrimary();
    }
}

public enum LegislationType
{
    PRIMARY,
    SECONDARY,
    // to be filled in...
}

public readonly record struct LegislationClassifier(
    DocName DocName,
    string? SubType,
    // only applicable for secondary doctypes, ideally we'd have a discriminated union between primary and secondary types since
    // they hold different data, but they're not available until C#14
    string? Procedure
)
{

    public Context GetContext()
    {
        return this switch
        {
            { SubType: "reg" }  => Context.REGULATIONS,
            { SubType: "rules" } => Context.RULES,
            { SubType: "order" } => Context.ARTICLES,
            _ => DocName.IsSecondaryDocName() ? Context.ARTICLES : Context.SECTIONS
        };
    }
}
