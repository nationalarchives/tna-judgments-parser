
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Metadata {

    private static readonly string ns = Builder.ns;

    private static XmlElement append(XmlDocument doc, XmlElement parent, string name) {
        XmlElement e = doc.CreateElement(name, ns);
        parent.AppendChild(e);
        return e;
    }
    private static XmlElement append(XmlDocument doc, XmlElement parent, string name, string aName, string aValue) {
        XmlElement e = doc.CreateElement(name, ns);
        parent.AppendChild(e);
        e.SetAttribute(aName, aValue);
        return e;
    }

    public static XmlElement make(XmlDocument doc, IJudgment judgment, IMetadata metadata, bool includeReferences) {

        string docId = metadata.DocumentId();
        string compId = metadata is IComponentMetadata c ? c.ComponentId : docId;
        string date = metadata.Date();

        Court? court = metadata.Court();

        XmlElement meta = doc.CreateElement("meta", ns);

        XmlElement identification = append(doc, meta, "identification");
        identification.SetAttribute("source", "#tna");

        List<IParty> parties = new List<IParty>();
        List<IDocTitle> docTitle = new List<IDocTitle>();
        foreach (IBlock block in judgment.Header) {
            if (block is ILine line) {
                parties.AddRange(line.Contents.OfType<IParty>());
                docTitle.AddRange(line.Contents.OfType<IDocTitle>());
            }
            if (block is ITable table)
                foreach (IRow row in table.Rows)
                    foreach (ICell cell in row.Cells)
                        foreach (ILine line2 in cell.Contents.OfType<ILine>()) {
                            parties.AddRange(line2.Contents.OfType<IParty>());
                            docTitle.AddRange(line2.Contents.OfType<IDocTitle>());
                        }
        }
        IParty party1 = parties.FirstOrDefault();
        IParty party2 = parties.Where(party => party.Role != party1.Role).FirstOrDefault();
        if (party2 is null && parties.Count() == 2 && !parties.Last().Role.HasValue)
            party2 = parties.Last();
        // IEnumerable<IDocTitle> docTitle = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<IDocTitle>();

        XmlElement work = append(doc, identification, "FRBRWork");
        XmlElement workThis = append(doc, work, "FRBRthis");
        workThis.SetAttribute("value", compId);
        XmlElement workURI = append(doc, work, "FRBRuri");
        workURI.SetAttribute("value", docId);
        XmlElement workDate = append(doc, work, "FRBRdate");
        workDate.SetAttribute("date", date ?? "1000-01-01");
        workDate.SetAttribute("name", date is null ? "unknown" : "judgment");
        XmlElement workAuthor = append(doc, work, "FRBRauthor");
        workAuthor.SetAttribute("href", "#" + court?.Code?.ToLower());
        XmlElement workCountry = append(doc, work, "FRBRcountry");
        workCountry.SetAttribute("value", "GB-UKM");
        foreach (string caseNumber in metadata.CaseNos()) {
            XmlElement workNumber = append(doc, work, "FRBRnumber");
            workNumber.SetAttribute("value", caseNumber);
        }
        if (party2 is not null) {
            XmlElement workName = append(doc, work, "FRBRname");
            workName.SetAttribute("value", party1.Name + " v. " + party2.Name);
        } else if (docTitle.Any()) {
            XmlElement workName = append(doc, work, "FRBRname");
            string value = string.Join(" ", docTitle.Select(dt => dt.Text));
            workName.SetAttribute("value", value);
        }

        XmlElement expression = append(doc, identification, "FRBRExpression");
        XmlElement expThis = append(doc, expression, "FRBRthis");
        expThis.SetAttribute("value", compId + "/eng");
        XmlElement expURI = append(doc, expression, "FRBRuri");
        expURI.SetAttribute("value", docId + "/eng");
        XmlElement expDate = append(doc, expression, "FRBRdate");
        expDate.SetAttribute("date", date ?? "1000-01-01");
        expDate.SetAttribute("name", date is null ? "unknown" : "judgment");
        XmlElement expAuthor = append(doc, expression, "FRBRauthor");
        expAuthor.SetAttribute("href", "#" + court?.Code?.ToLower());
        // XmlElement expAuthoritative = append(doc, expression, "FRBRauthoritative");
        // expAuthoritative.SetAttribute("value", "true");
        XmlElement expLanguage = append(doc, expression, "FRBRlanguage");
        expLanguage.SetAttribute("language", "eng");

        XmlElement manifestation = append(doc, identification, "FRBRManifestation");
        XmlElement maniThis = append(doc, manifestation, "FRBRthis");
        maniThis.SetAttribute("value", compId + "/eng/akn");
        XmlElement maniURI = append(doc, manifestation, "FRBRuri");
        maniURI.SetAttribute("value", docId + "/eng/akn");
        XmlElement maniDate = append(doc, manifestation, "FRBRdate");
        maniDate.SetAttribute("date", DateTime.UtcNow.ToString("s"));   // , System.Globalization.CultureInfo.InvariantCulture
        maniDate.SetAttribute("name", "transform");
        XmlElement maniAuthor = append(doc, manifestation, "FRBRauthor");
        maniAuthor.SetAttribute("href", "#tna");
        XmlElement maniFormat = append(doc, manifestation, "FRBRformat");
        maniFormat.SetAttribute("value", "application/xml");

        if (includeReferences) {
            XmlElement references = append(doc, meta, "references");
            references.SetAttribute("source", "#tna");

            if (court is not null) {
                XmlElement org = append(doc, references, "TLCOrganization");
                org.SetAttribute("eId", court?.Code.ToLower());
                org.SetAttribute("href", court?.URL);
                org.SetAttribute("showAs", court?.LongName);
                if (court?.ShortName is not null)
                    org.SetAttribute("shortForm", court?.ShortName);
            }

            XmlElement tna = append(doc, references, "TLCOrganization");
            tna.SetAttribute("eId", "tna");
            tna.SetAttribute("href", "https://www.nationalarchives.gov.uk/");
            tna.SetAttribute("showAs", "The National Archives");

            IDictionary<string, IParty> uniqueParies = new Dictionary<string, IParty>();
            foreach (IParty party in parties)
                uniqueParies.TryAdd(party.Id, party);

            ISet<PartyRole> roles = new HashSet<PartyRole>();
            foreach (IParty party in uniqueParies.Values) {
                XmlElement org = append(doc, references, "TLCPerson");
                org.SetAttribute("eId", party.Id);
                org.SetAttribute("href", "");
                org.SetAttribute("showAs", party.Name);
                if (party.Role.HasValue)
                    roles.Add(party.Role.Value);
            }
            foreach (PartyRole role in roles) {
                XmlElement org = append(doc, references, "TLCRole");
                org.SetAttribute("eId", role.EId());
                org.SetAttribute("href", "");
                org.SetAttribute("showAs", role.ShowAs());
            }

            IEnumerable<IJudge> judges = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<IJudge>();
            foreach (IJudge judge in judges) {
                XmlElement org = append(doc, references, "TLCPerson");
                org.SetAttribute("eId", judge.Id);
                org.SetAttribute("href", "/" + judge.Id);
                org.SetAttribute("showAs", judge.Name);
            }

            foreach (ILawyer lawyer in judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<ILawyer>()) {
                XmlElement org = append(doc, references, "TLCPerson");
                org.SetAttribute("eId", lawyer.Id);
                org.SetAttribute("href", "/" + lawyer.Id);
                org.SetAttribute("showAs", lawyer.Name);
            }

        }

        Dictionary<string, Dictionary<string, string>> styles = metadata.CSSStyles();
        if (styles is not null) {
            XmlElement presentation = append(doc, meta, "presentation");
            presentation.SetAttribute("source", docId + "/eng/docx");
            XmlElement style = doc.CreateElement("style", "http://www.w3.org/1999/xhtml");
            presentation.AppendChild(style);
            style.AppendChild(doc.CreateTextNode("\n"));
            string css = CSS.Serialize(styles);
            style.AppendChild(doc.CreateTextNode(css));
        }

        return meta;
   }

}

class AttachmentMetadata : IComponentMetadata {

    private readonly IMetadata prototype;

    private readonly int n;

    internal AttachmentMetadata(IMetadata prototype, int n) {

        this.prototype = prototype;
        this.n = n;
    }

    public Court? Court() { return prototype.Court(); }

    public string DocumentId() { return prototype.DocumentId(); }

    public string ComponentId {
        get { return prototype.DocumentId() + "/annex/" + n; }
    }

    public string Date() { return prototype.Date(); }

    public IEnumerable<string> CaseNos() => Enumerable.Empty<string>();

    public Dictionary<string, Dictionary<string, string>> CSSStyles() => null;

}

}