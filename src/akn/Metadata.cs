
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Metadata {

    public static string ukns = "https:/judgments.gov.uk/";

    private static readonly string ns = Builder.ns;

    public static readonly string DummyDate = "1000-01-01";

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
        IParty party1 = null;
        IParty party2 = null;
        if (includeReferences) {
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
            party1 = parties.FirstOrDefault();
            party2 = parties.Where(party => party.Role != party1.Role).FirstOrDefault();
            if (party2 is null && parties.Count() == 2 && !parties.Last().Role.HasValue)
                party2 = parties.Last();
        }

        XmlElement work = append(doc, identification, "FRBRWork");
        XmlElement workThis = append(doc, work, "FRBRthis");
        workThis.SetAttribute("value", compId);
        XmlElement workURI = append(doc, work, "FRBRuri");
        workURI.SetAttribute("value", docId);
        XmlElement workDate = append(doc, work, "FRBRdate");
        workDate.SetAttribute("date", date ?? DummyDate);
        workDate.SetAttribute("name", date is null ? "unknown" : "judgment");
        XmlElement workAuthor = append(doc, work, "FRBRauthor");
        workAuthor.SetAttribute("href", "#" + court?.Code?.ToLower());
        XmlElement workCountry = append(doc, work, "FRBRcountry");
        workCountry.SetAttribute("value", "GB-UKM");
        if (includeReferences) {
            XmlElement workNumber = append(doc, work, "FRBRnumber");
            workNumber.SetAttribute("value", metadata.Number.ToString());
            string caseName = metadata.CaseName;
            if (caseName is not null) {
                XmlElement workName = append(doc, work, "FRBRname");
                workName.SetAttribute("value", caseName);
            }
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

            IEnumerable<ILocation> locations = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<ILocation>();
            foreach (ILocation loc in locations) {
                XmlElement org = append(doc, references, "TLCLocation");
                org.SetAttribute("eId", loc.Id);
                org.SetAttribute("href", "/" + loc.Id);
                org.SetAttribute("showAs", loc.Name);
            }

            XmlElement proprietary = append(doc, meta, "proprietary");
            proprietary.SetAttribute("source", docId + "/eng/docx");
            proprietary.SetAttribute("xmlns:uk", ukns);
            if (court is not null) {
                XmlElement courtt = doc.CreateElement("court", ukns);
                proprietary.AppendChild(courtt);
                courtt.AppendChild(doc.CreateTextNode(court.Value.Code.ToString()));
            }
            if (metadata.Year is not null) {
                XmlElement year = doc.CreateElement("year", ukns);
                proprietary.AppendChild(year);
                year.AppendChild(doc.CreateTextNode(metadata.Year.ToString()));
            }
            if (metadata.Number is not null) {
                XmlElement number = doc.CreateElement("number", ukns);
                proprietary.AppendChild(number);
                number.AppendChild(doc.CreateTextNode(metadata.Number.ToString()));
            }
            if (metadata.Cite is not null) {
                XmlElement cite = doc.CreateElement("cite", ukns);
                proprietary.AppendChild(cite);
                cite.AppendChild(doc.CreateTextNode(metadata.Cite.ToString()));
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

            foreach (var tuple in judgment.Metadata.ExternalAttachments.Select((attachment, i) => new { i, attachment })) {
                XmlElement hasAttachment = append(doc, references, "hasAttachment");
                hasAttachment.SetAttribute("href", "/" + docId + "/attachment/" + ( tuple.i + 1 ) + ".pdf");
                hasAttachment.SetAttribute("showAs", tuple.attachment.Type);
            }

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

    public int? Year { get => prototype.Year; }

    public int? Number { get => prototype.Number; }

    public string Cite { get => prototype.Cite; }

    public string DocumentId() { return prototype.DocumentId(); }

    public string ComponentId {
        get { return prototype.DocumentId() + "/annex/" + n; }
    }

    public string Date() { return prototype.Date(); }

    public string CaseName { get => prototype.CaseName; }

    public IEnumerable<string> CaseNos() => Enumerable.Empty<string>();

    public Dictionary<string, Dictionary<string, string>> CSSStyles() => null;

    public IEnumerable<IExternalAttachment> ExternalAttachments { get => Enumerable.Empty<IExternalAttachment>(); }

}

}