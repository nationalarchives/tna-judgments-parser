#nullable enable
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker;

    class MetadataBuilder
    {

        internal static XmlElement Add(XmlElement parent, string title)
        {
            XmlDocument temp = new();
            temp.LoadXml(MakelXml(title));
            XmlNode imported = parent.OwnerDocument.ImportNode(temp.DocumentElement, true);
            parent.AppendChild(imported);
            imported.Attributes.Remove(imported.Attributes["xmlns"]);
            return (XmlElement)imported;
        }

        private static string MakelXml(string title)
        {
            return $"""
            <meta xmlns="http://docs.oasis-open.org/legaldocml/ns/akn/3.0">
                <identification source="#author">
                    <FRBRWork>
                        <FRBRthis value="#varWork"/>
                        <FRBRuri value="#varWorkUri"/>
                        <FRBRalias name="ttflConfigHistory" value="#varCHOiid"/>
                        <FRBRdate date="9999-01-01" name="draft"/>
                        <FRBRauthor href="#varAuthor"/>
                        <FRBRcountry value="GB-UKM"/>
                        <FRBRsubtype value="#varDocSubType"/>
                        <FRBRnumber value="#varProjectId"/>
                        <FRBRname value="#varBillTitle"/>
                        <FRBRprescriptive value="true"/>
                        <FRBRauthoritative value="false"/>
                    </FRBRWork>
                    <FRBRExpression>
                        <FRBRthis value="#varExpression"/>
                        <FRBRuri value="#varExpressionUri"/>
                        <FRBRalias name="ttflVersionDescription" value="#varVDOiid"/>
                        <FRBRdate date="9999-01-01" name="draft"/>
                        <FRBRauthor href="#varAuthor"/>
                        <FRBRversionNumber value=""/>
                        <FRBRauthoritative value="false"/>
                        <FRBRmasterExpression href="" showAs=""/>
                        <FRBRlanguage language="eng"/>
                    </FRBRExpression>
                    <FRBRManifestation>
                        <FRBRthis value="#varManifestation"/>
                        <FRBRuri value="#varManifestationUri"/>
                        <FRBRdate date="9999-01-01" name="akn_xml"/>
                        <FRBRauthor href="#varAuthor"/>
                        <FRBRformat value="application/akn+xml"/>
                    </FRBRManifestation>
                </identification>
                <references source="#author">
                    <TLCConcept eId="varDocSubType" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varBillTitle" href="#varOntologies" showAs="{title}"/>
                    <TLCProcess eId="varStageVersion" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varProjectId" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varWork" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varWorkUri" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varCHOiid" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varExpression" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varExpressionUri" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varVDOiid" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varManifestation" href="#varOntologies" showAs=""/>
                    <TLCProcess eId="varManifestationUri" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varActYear" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varActTitle" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varIntroDate" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varConsidDate" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varFurthConsidDate" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varBillNoComp" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varBillNo" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varSessionNo" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varBillYear" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varSession" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varAssentDate" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varActNoComp" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varActNo" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varDocType" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varSystem" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varAuthor" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varJurisdiction" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varOntologies" href="https://www.legislation.gov.uk/ontologies/UK-AKN"
                        showAs=""/>
                    <TLCConcept eId="varSovereign" href="#varOntologies" showAs=""/>
                </references>
            </meta>
            """;
        }

    }


/// <summary>
///
/// </summary>
/// <param name="Key"></param>
/// <param name="ShowAs"></param>
/// <param name="Href"></param>
/// <param name="Num"></param>
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