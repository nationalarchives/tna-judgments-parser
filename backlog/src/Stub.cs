
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace Backlog.Src
{

    internal class Stub
    {
        internal static Stub Make(ExtendedMetadata data)
        {
            return new(data);
        }

        private static readonly string AKNNS = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
        private static readonly string UKNS = "https://caselaw.nationalarchives.gov.uk/akn";

        private readonly XmlDocument Document = new();
        private readonly IMetadataExtended Data;

        private XmlElement CreateAndAppend(string name, XmlNode parent)
        {
            XmlElement e = Document.CreateElement(name, AKNNS);
            parent.AppendChild(e);
            return e;
        }

        private XmlElement CreateAndAppendUK(string name, XmlNode parent)
        {
            XmlElement e = Document.CreateElement("uk", name, UKNS);
            parent.AppendChild(e);
            return e;
        }
        private Stub(ExtendedMetadata data)
        {
            Data = data;
            XmlElement root = CreateAndAppend("akomaNtoso", Document);
            XmlElement main = CreateAndAppend("judgment", root);
            main.SetAttribute("name", Enum.GetName(typeof(JudgmentType), data.Type).ToLower());
            Meta(main);
            Body(main);
        }

        private void Meta(XmlElement judgment)
        {
            XmlElement meta = CreateAndAppend("meta", judgment);
            Identification(meta);
            Lifecycle(meta);
            References(meta);
            Proprietary(meta);
        }

        private void Identification(XmlElement meta)
        {
            XmlElement identification = CreateAndAppend("identification", meta);
            identification.SetAttribute("source", "#tna");
            Work(identification);
            Expression(identification);
            Manifestation(identification);
        }

        private void Work(XmlElement identification)
        {
            XmlElement work = CreateAndAppend("FRBRWork", identification);
            XmlElement ths = CreateAndAppend("FRBRthis", work);
            ths.SetAttribute("value", Data.WorkThis);
            XmlElement uri = CreateAndAppend("FRBRuri", work);
            uri.SetAttribute("value", Data.WorkURI);
            XmlElement date = CreateAndAppend("FRBRdate", work);
            date.SetAttribute("date", Data.Date?.Date);
            date.SetAttribute("name", Data.Date?.Name);
            XmlElement author = CreateAndAppend("FRBRauthor", work);
            author.SetAttribute("href", "#" + UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.MakeCourtId(Data.Court));
            XmlElement country = CreateAndAppend("FRBRcountry", work);
            country.SetAttribute("value", "GB-UKM");
            if (Data.Name is not null)
            {
                XmlElement name = CreateAndAppend("FRBRname", work);
                name.SetAttribute("value", Data.Name);
            }
        }

        private void Expression(XmlElement identification)
        {
            XmlElement expr = CreateAndAppend("FRBRExpression", identification);
            XmlElement ths = CreateAndAppend("FRBRthis", expr);
            ths.SetAttribute("value", Data.ExpressionThis);
            XmlElement uri = CreateAndAppend("FRBRuri", expr);
            uri.SetAttribute("value", Data.ExpressionUri);
            XmlElement date = CreateAndAppend("FRBRdate", expr);
            date.SetAttribute("date", Data.Date?.Date);
            date.SetAttribute("name", Data.Date?.Name);
            XmlElement author = CreateAndAppend("FRBRauthor", expr);
            author.SetAttribute("href", "#" + UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.MakeCourtId(Data.Court));
            XmlElement lang = CreateAndAppend("FRBRlanguage", expr);
            lang.SetAttribute("language", "eng");
        }

        private void Manifestation(XmlElement identification)
        {
            XmlElement mani = CreateAndAppend("FRBRManifestation", identification);
            XmlElement ths = CreateAndAppend("FRBRthis", mani);
            ths.SetAttribute("value", Data.ManifestationThis);
            XmlElement uri = CreateAndAppend("FRBRuri", mani);
            uri.SetAttribute("value", Data.ManifestationUri);
            XmlElement date = CreateAndAppend("FRBRdate", mani);
            date.SetAttribute("date", DateTime.UtcNow.ToString("s"));
            date.SetAttribute("name", "transform");
            XmlElement author = CreateAndAppend("FRBRauthor", mani);
            author.SetAttribute("href", "#tna");
            XmlElement format = CreateAndAppend("FRBRformat", mani);
            format.SetAttribute("value", "application/xml");
        }

        private void Lifecycle(XmlElement meta)
        {
            XmlElement lifecycle = CreateAndAppend("lifecycle", meta);
            lifecycle.SetAttribute("source", "#");
            XmlElement eventRef = CreateAndAppend("eventRef", lifecycle);
            eventRef.SetAttribute("date", Data.Date.Date);
            eventRef.SetAttribute("refersTo", "#" + UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.MakeDateId(Data.Date));
            eventRef.SetAttribute("source", "#");
        }

        private void References(XmlElement meta)
        {
            XmlElement references = CreateAndAppend("references", meta);
            references.SetAttribute("source", "#tna");
            XmlElement tna = CreateAndAppend("TLCOrganization", references);
            tna.SetAttribute("eId", "tna");
            tna.SetAttribute("href", "https://www.nationalarchives.gov.uk/");
            tna.SetAttribute("showAs", "The National Archives");
            if (Data.Court is not null)
            {
                XmlElement tldOrg = CreateAndAppend("TLCOrganization", references);
                tldOrg.SetAttribute("eId", UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.MakeCourtId(Data.Court));
                tldOrg.SetAttribute("href", Data.Court.Value.URL);
                tldOrg.SetAttribute("showAs", Data.Court.Value.Name);
            }
            XmlElement tlcEvent = CreateAndAppend("TLCEvent", references);
            tlcEvent.SetAttribute("eId", UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.MakeDateId(Data.Date));
            tlcEvent.SetAttribute("href", "#");
            tlcEvent.SetAttribute("showAs", Data.Date.Name);
        }

        private void Proprietary(XmlElement meta)
        {
            XmlElement proprietary = CreateAndAppend("proprietary", meta);
            proprietary.SetAttribute("xmlns:uk", UKNS);
            proprietary.SetAttribute("source", "#");
            if (Data.Court is not null)
            {
                XmlElement court = CreateAndAppendUK("court", proprietary);
                proprietary.AppendChild(court);
                court.AppendChild(Document.CreateTextNode(Data.Court.Value.Code.ToString()));
            }

            foreach (var jurisdiction in Data.Jurisdictions)
            {
                XmlElement jurisdictionNode = CreateAndAppendUK("jurisdiction", proprietary);
                proprietary.AppendChild(jurisdictionNode);
                jurisdictionNode.AppendChild(Document.CreateTextNode(jurisdiction.ShortName));
            }
            
            XmlElement year = CreateAndAppendUK("year", proprietary);
            proprietary.AppendChild(year);
            year.AppendChild(Document.CreateTextNode(Data.Date.Date[..4]));
            foreach (var caseNo in Data.CaseNumbers)
            {
                XmlElement number = CreateAndAppendUK("caseNumber", proprietary);
                proprietary.AppendChild(number);
                number.AppendChild(Document.CreateTextNode(caseNo));

            }
            foreach (UK.Gov.NationalArchives.CaseLaw.Model.Party pty in Data.Parties)
            {
                XmlElement party = CreateAndAppendUK("party", proprietary);
                proprietary.AppendChild(party);
                party.SetAttribute("role", pty.Role.ShowAs());
                party.AppendChild(Document.CreateTextNode(pty.Name));
            }
            foreach (var cat in Data.Categories)
            {
                AddCategory(proprietary, cat);
            }
            if (!string.IsNullOrWhiteSpace(Data.NCN))
            {
                XmlElement cite = CreateAndAppendUK("cite", proprietary);
                proprietary.AppendChild(cite);
                cite.AppendChild(Document.CreateTextNode(Data.NCN));
            }
            if (!string.IsNullOrWhiteSpace(Data.WebArchivingLink))
            {
                XmlElement webarchivingNode = CreateAndAppendUK("webarchiving", proprietary);
                proprietary.AppendChild(webarchivingNode);
                webarchivingNode.AppendChild(Document.CreateTextNode(Data.WebArchivingLink));
            }
            XmlElement sourceFormat = CreateAndAppendUK("sourceFormat", proprietary);
            proprietary.AppendChild(sourceFormat);
            sourceFormat.AppendChild(Document.CreateTextNode(Data.SourceFormat));

            XmlElement parser = CreateAndAppendUK("parser", proprietary);
            proprietary.AppendChild(parser);
            parser.AppendChild(Document.CreateTextNode(UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.GetParserVersion()));
        }

        internal static XmlElement AddProprietaryField(XmlElement proprietary, string name, string value) {
            XmlElement proprietaryField = proprietary.OwnerDocument.CreateElement("uk", name, UKNS);
            proprietary.AppendChild(proprietaryField);
            var text = proprietary.OwnerDocument.CreateTextNode(value);
            proprietaryField.AppendChild(text);
            return proprietaryField;
        }

        internal static void AddCategory(XmlElement proprietary, ICategory value) {
            var proprietaryWithCategory = AddProprietaryField(proprietary, "category", value.Name);
            if (value.Parent is not null)
                proprietaryWithCategory.SetAttribute("parent", value.Parent);
        }

        /* body */

        private void Body(XmlElement judgment)
        {
            XmlElement header = CreateAndAppend("header", judgment);
            XmlElement body = CreateAndAppend("judgmentBody", judgment);
            XmlElement decision = CreateAndAppend("decision", body);
            XmlElement p = CreateAndAppend("p", decision);
        }

        public List<ValidationEventArgs> Validate()
        {
            UK.Gov.Legislation.Judgments.AkomaNtoso.Validator validator = new();
            return validator.Validate(Document);
        }

        public void Serialize(Stream stream)
        {
            Serializer.Serialize(Document, stream);
        }

        public string Serialize() {
            using MemoryStream stream = new ();
            Serialize(stream);
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

    }

}
