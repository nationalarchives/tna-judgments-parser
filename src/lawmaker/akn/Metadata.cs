#nullable enable
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker;


/// <summary>
/// A representation of a metadata reference in a Lawmaker document.
/// </summary>
/// <param name="Key">The eId key for the reference. This is what <c>ref</c>
/// elements use when referencing this metadata value.</param>
/// <param name="ShowAs">The actual value of the reference.</param>
/// <param name="Href">A href element which Lawmaker uses.
/// This is normally a default value and doesn't need to be manually set.</param>
public record Reference(
    ReferenceKey Key,
    string ShowAs = "",
    string Href = "#varOntologies"
) : IBuildable<XNode>
{
    private readonly ReferenceType type = Key.GetReferenceType();

    public uint Num { get; set; }
    public string EId { get => $"{Key}{(Num > 0 ? Num + 1 : "")}"; }

    public XNode? Build(Document _) =>
        new XElement(akn + type.ToString(),
            new XAttribute("eId", EId),
            new XAttribute("href", Href),
            new XAttribute("showAs", ShowAs)
        );
}

public enum ReferenceType
{
    TLCConcept,
    TLCProcess,
    TLCPerson,
}

/// <summary>
/// The possible element ids for Lawmaker document metadata/references.
/// </summary>
/// <remarks>
/// The order of this enum determines the order which the references are
/// written to the document.
/// </remarks>
public enum ReferenceKey
{
    varDocSubType,
    varBillTitle,
    varHouse,
    varStageVersion,
    varProjectId,
    varWork,
    varWorkUri,
    varCHOiid,
    varExpression,
    varExpressionUri,
    varVDOiid,
    varManifestation,
    varManifestationUri,
    varActYear,
    varActTitle,
    varIntroDate,
    varConsidDate,
    varFurthConsidDate,
    varBillNoComp,
    varBillNo,
    varSessionNo,
    varBillYear,
    varSession,
    varAssentDate,
    varActNoComp,
    varActNo,
    varDocType,
    varSystem,
    varAuthor,
    varJurisdiction,
    varOntologies,
    varSovereign,

    // SI only
    varSITitle,
    varProcedureType,
    varSIYear,
    varMadeDate,
    varLaidDate,
    varCommenceDate,
    varOtherDate,
    varSINoComp,
    varSINo,
    varSISubsidiaryNos,
    varISBN,
    varMinistryAgency,
}

static class ReferenceKeys
{
    /// <summary>
    /// Get the reference type for a specific reference key.
    /// </summary>
    /// <param name="key">The key to get the reference for.</param>
    /// <remarks>
    /// Each key has a direct mapping to a type.
    /// The type determines the tag name when built to XML, i.e. ReferenceType.TLCConcept => <c>&lt;TLCConcept ...&gt;</c>
    /// </remarks>
    internal static ReferenceType GetReferenceType(this ReferenceKey key) => key switch
    {

        ReferenceKey.varStageVersion
        or ReferenceKey.varProjectId
        or ReferenceKey.varWork
        or ReferenceKey.varWorkUri
        or ReferenceKey.varCHOiid
        or ReferenceKey.varExpression
        or ReferenceKey.varExpressionUri
        or ReferenceKey.varVDOiid
        or ReferenceKey.varManifestation
        or ReferenceKey.varManifestationUri
        or ReferenceKey.varProcedureType
            => ReferenceType.TLCProcess,
        ReferenceKey.varDocSubType
        or ReferenceKey.varBillTitle
        or ReferenceKey.varHouse
        or ReferenceKey.varActYear
        or ReferenceKey.varActTitle
        or ReferenceKey.varIntroDate
        or ReferenceKey.varConsidDate
        or ReferenceKey.varFurthConsidDate
        or ReferenceKey.varBillNoComp
        or ReferenceKey.varBillNo
        or ReferenceKey.varSessionNo
        or ReferenceKey.varBillYear
        or ReferenceKey.varSession
        or ReferenceKey.varAssentDate
        or ReferenceKey.varActNoComp
        or ReferenceKey.varActNo
        or ReferenceKey.varDocType
        or ReferenceKey.varSystem
        or ReferenceKey.varAuthor
        or ReferenceKey.varJurisdiction
        or ReferenceKey.varOntologies
        or ReferenceKey.varSovereign
        or ReferenceKey.varSITitle
        or ReferenceKey.varSIYear
        or ReferenceKey.varMadeDate
        or ReferenceKey.varLaidDate
        or ReferenceKey.varCommenceDate
        or ReferenceKey.varOtherDate
        or ReferenceKey.varSINoComp
        or ReferenceKey.varSINo
        or ReferenceKey.varSISubsidiaryNos
        or ReferenceKey.varISBN
            => ReferenceType.TLCConcept,
        ReferenceKey.varMinistryAgency
            => ReferenceType.TLCPerson,
    };
}