#nullable enable
using System;
namespace UK.Gov.Legislation.Lawmaker;

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
    NIDSR

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

    public static DocName ToEnacted(DocName docName)
    {
        // C# will never be happy with exhaustive switch statements because you can always pass (DocName)20
        return docName switch
        {
            DocName.NIA => DocName.NIA,
            DocName.UKPGA => DocName.UKPGA,
            DocName.UKCM => DocName.UKCM,
            DocName.ASP => DocName.ASP,
            DocName.NISI => DocName.NISI,
            DocName.NISR => DocName.NISR,
            DocName.UKSI => DocName.UKSI,
            DocName.SSI => DocName.SSI,
            DocName.UKPUBB => DocName.UKPGA,
            DocName.UKPRIB => DocName.UKPGA,
            DocName.UKHYBB => DocName.UKPGA,
            DocName.UKDCM => DocName.UKCM,
            DocName.UKDSI => DocName.UKDSI,
            DocName.SPPUBB => DocName.ASP,
            DocName.SPPRIB => DocName.ASP,
            DocName.SPHYBB => DocName.ASP,
            DocName.SDSI => DocName.SSI,
            DocName.NIPUBB => DocName.NIA,
            DocName.NIDSI => DocName.NISI,
            DocName.NIDSR => DocName.NISR,
        };
    }

    public static LegislationType GetLegislationType(DocName docName)
    {
        return docName switch
        {
            DocName.NIA =>             LegislationType.PRIMARY,
            DocName.UKPGA =>           LegislationType.PRIMARY,
            DocName.UKCM =>            LegislationType.PRIMARY,
            DocName.ASP =>             LegislationType.PRIMARY,
            DocName.UKPUBB =>          LegislationType.PRIMARY,
            DocName.UKPRIB =>          LegislationType.PRIMARY,
            DocName.UKHYBB =>          LegislationType.PRIMARY,
            DocName.UKDCM =>           LegislationType.PRIMARY,
            DocName.UKDSI =>           LegislationType.PRIMARY,
            DocName.SPPUBB =>          LegislationType.PRIMARY,
            DocName.SPPRIB =>          LegislationType.PRIMARY,
            DocName.SPHYBB =>          LegislationType.PRIMARY,

            DocName.NISI =>            LegislationType.SECONDARY,
            DocName.NISR =>            LegislationType.SECONDARY,
            DocName.UKSI =>            LegislationType.SECONDARY,
            DocName.SSI =>             LegislationType.SECONDARY,
            DocName.SDSI =>            LegislationType.SECONDARY,
            DocName.NIPUBB =>          LegislationType.SECONDARY,
            DocName.NIDSI =>           LegislationType.SECONDARY,
            DocName.NIDSR =>           LegislationType.SECONDARY,
        };
    }

    public static bool IsSecondaryDocName(DocName docName)
    {
        return DocNames.GetLegislationType(docName) == LegislationType.SECONDARY;
    }
}

public enum LegislationType
{
    PRIMARY,
    SECONDARY,
    // to be filled in...
}

public readonly record struct LegislationClassifier(
    // Every legislation document has a document "name". Not to be confused with the title. A name is the broader category of document.
    // e.g. "nipubb" is an Northern Ireland Public Bill
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
            _ => DocNames.IsSecondaryDocName(DocName) ? Context.ARTICLES : Context.SECTIONS
        };
    }
}