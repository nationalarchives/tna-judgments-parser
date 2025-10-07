
using System.Xml;

namespace UK.Gov.Legislation.Lawmaker
{

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
                    <TLCConcept eId="varDocSubType" href="#varOntologies" showAs="Executive Bill"/>
                    <TLCConcept eId="varBillTitle" href="#varOntologies" showAs="{title}"/>
                    <TLCProcess eId="varStageVersion" href="#varOntologies" showAs="Pre-Introduction"/>
                    <TLCProcess eId="varProjectId" href="#varOntologies" showAs="NI000127"/>
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
                    <TLCConcept eId="varBillNo" href="#varOntologies" showAs="NIA Bill X"/>
                    <TLCConcept eId="varSessionNo" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varBillYear" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varSession" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varAssentDate" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varActNoComp" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varActNo" href="#varOntologies" showAs=""/>
                    <TLCConcept eId="varDocType" href="#varOntologies" showAs="NI Bill"/>
                    <TLCConcept eId="varSystem" href="#varOntologies" showAs="LDAPP"/>
                    <TLCConcept eId="varAuthor" href="#varOntologies" showAs="Northern Ireland Assembly"/>
                    <TLCConcept eId="varJurisdiction" href="#varOntologies" showAs="Northern Ireland"/>
                    <TLCConcept eId="varOntologies" href="https://www.legislation.gov.uk/ontologies/UK-AKN"
                        showAs="https://www.legislation.gov.uk/ontologies/UK-AKN"/>
                    <TLCConcept eId="varSovereign" href="#varOntologies" showAs="Charles III"/>
                </references>
            </meta>
            """;
        }

    }

}
