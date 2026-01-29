
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;

using judgments.src.akn;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class Metadata {

    public static string ukns = "https://caselaw.nationalarchives.gov.uk/akn";

    private static readonly string ns = Builder.ns;

    public static readonly string DummyDate = "1000-01-01";

    public static string GetParserVersion() {
        AssemblyInformationalVersionAttribute version = Assembly.GetCallingAssembly()
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).FirstOrDefault() as AssemblyInformationalVersionAttribute;
        return version?.InformationalVersion;
    }

    private static string MakeCourtId(string code) {
        return code?.ToLower();
    }
    internal static string MakeCourtId(Court? court) {
        return MakeCourtId(court?.Code);
    }
    internal static string MakeCourtId(ICourtType court) {
        return MakeCourtId(court.Code);
    }
    internal static string MakeDateId(INamedDate date) {
        return date?.Name.ToLower();
    }

    private static XmlElement append(XmlDocument doc, XmlElement parent, string name) {
        XmlElement e = doc.CreateElement(name, ns);
        parent.AppendChild(e);
        return e;
    }
    private static XmlElement append(XmlElement parent, string name) {
        return append(parent.OwnerDocument, parent, name);
    }
    private static XmlElement append(XmlDocument doc, XmlElement parent, string name, string aName, string aValue) {
        XmlElement e = doc.CreateElement(name, ns);
        parent.AppendChild(e);
        e.SetAttribute(aName, aValue);
        return e;
    }

    public static XmlElement make(XmlDocument doc, IJudgment judgment, IMetadata metadata, bool includeReferences) {

        INamedDate mainDate = metadata.Date;
        Court? court = metadata.Court;

        XmlElement meta = doc.CreateElement("meta", ns);

        XmlElement identification = append(doc, meta, "identification");
        identification.SetAttribute("source", "#tna");

        XmlElement work = append(doc, identification, "FRBRWork");
        XmlElement workThis = append(doc, work, "FRBRthis");
        workThis.SetAttribute("value", metadata.WorkThis);
        XmlElement workURI = append(doc, work, "FRBRuri");
        workURI.SetAttribute("value", metadata.WorkURI);
        XmlElement workDate = append(doc, work, "FRBRdate");
        workDate.SetAttribute("date", mainDate?.Date ?? DummyDate);
        workDate.SetAttribute("name", mainDate is null ? "dummy" : mainDate.Name);
        XmlElement workAuthor = append(doc, work, "FRBRauthor");
        workAuthor.SetAttribute("href", "#" + MakeCourtId(court));
        XmlElement workCountry = append(doc, work, "FRBRcountry");
        workCountry.SetAttribute("value", "GB-UKM");
        if (includeReferences) {
            XmlElement workNumber = append(doc, work, "FRBRnumber");
            workNumber.SetAttribute("value", metadata.Number.ToString());
        }
        if (metadata.Name is not null) {
            XmlElement workName = append(doc, work, "FRBRname");
            workName.SetAttribute("value", metadata.Name);
        }

        XmlElement expression = append(doc, identification, "FRBRExpression");
        XmlElement expThis = append(doc, expression, "FRBRthis");
        expThis.SetAttribute("value", metadata.ExpressionThis);
        XmlElement expURI = append(doc, expression, "FRBRuri");
        expURI.SetAttribute("value", metadata.ExpressionUri);
        XmlElement expDate = append(doc, expression, "FRBRdate");
        expDate.SetAttribute("date", mainDate?.Date ?? DummyDate);
        expDate.SetAttribute("name", mainDate is null ? "dummy" : mainDate.Name);
        XmlElement expAuthor = append(doc, expression, "FRBRauthor");
        expAuthor.SetAttribute("href", "#" + MakeCourtId(court));
        // XmlElement expAuthoritative = append(doc, expression, "FRBRauthoritative");
        // expAuthoritative.SetAttribute("value", "true");
        XmlElement expLanguage = append(doc, expression, "FRBRlanguage");
        expLanguage.SetAttribute("language", "eng");

        XmlElement manifestation = append(doc, identification, "FRBRManifestation");
        XmlElement maniThis = append(doc, manifestation, "FRBRthis");
        maniThis.SetAttribute("value", metadata.ManifestationThis);
        XmlElement maniURI = append(doc, manifestation, "FRBRuri");
        maniURI.SetAttribute("value", metadata.ManifestationUri);
        XmlElement maniDate = append(doc, manifestation, "FRBRdate");
        maniDate.SetAttribute("date", DateTime.UtcNow.ToString("s"));   // , System.Globalization.CultureInfo.InvariantCulture
        maniDate.SetAttribute("name", "transform");
        XmlElement maniAuthor = append(doc, manifestation, "FRBRauthor");
        maniAuthor.SetAttribute("href", "#tna");
        XmlElement maniFormat = append(doc, manifestation, "FRBRformat");
        string formatValue = metadata.ManifestationThis.EndsWith("/data.akn") ? "application/akn+xml" : "application/xml";
        maniFormat.SetAttribute("value", formatValue);

        if (includeReferences) {

            IEnumerable<IDocDate> allDocDates = Util.Descendants<IDocDate>(judgment);

            IEnumerable<IDocDate> uniqueDocDates = allDocDates
                .GroupBy(date => ((IDate)date).Date).Select(x => x.First())
                .OrderBy(date => ((IDate)date).Date);
            if (uniqueDocDates.Any()) {
                XmlElement lifecycle = append(doc, meta, "lifecycle");
                lifecycle.SetAttribute("source", "#");
                foreach (IDocDate d in uniqueDocDates) {
                    XmlElement eventRef = append(doc, lifecycle, "eventRef");
                    eventRef.SetAttribute("date", ((IDate)d).Date);
                    eventRef.SetAttribute("refersTo", "#" + MakeDateId(d));
                    eventRef.SetAttribute("source", "#");
                }
            }
            IEnumerable<IDocDate> uniqueDocDateNames = allDocDates
                .Where(date => !string.IsNullOrEmpty(date.Name))
                .GroupBy(date => date.Name).Select(x => x.First())
                .OrderBy(date => ((IDate)date).Date);

            XmlElement references = append(doc, meta, "references");
            references.SetAttribute("source", "#tna");

            if (court is not null) {
                XmlElement tldOrg = append(doc, references, "TLCOrganization");
                tldOrg.SetAttribute("eId", MakeCourtId(court));
                tldOrg.SetAttribute("href", court.Value.URL);
                tldOrg.SetAttribute("showAs", court.Value.Name);
            }

            XmlElement tna = append(doc, references, "TLCOrganization");
            tna.SetAttribute("eId", "tna");
            tna.SetAttribute("href", "https://www.nationalarchives.gov.uk/");
            tna.SetAttribute("showAs", "The National Archives");

            foreach (IDocDate d in uniqueDocDateNames) {
                XmlElement tlcEvent = append(doc, references, "TLCEvent");
                tlcEvent.SetAttribute("eId", MakeDateId(d));
                tlcEvent.SetAttribute("href", "#");
                tlcEvent.SetAttribute("showAs", d.Name);
            }

            IEnumerable<IParty> parties = Util.Descendants<IParty>(judgment.Header);
            parties = parties.Where(party => !party.Suppress);
            IDictionary<string, IParty> uniqueParties = new Dictionary<string, IParty>();
            foreach (IParty party in parties)
                uniqueParties.TryAdd(party.Id, party);
            foreach (IParty party in uniqueParties.Values) {
                XmlElement tlcPerson = append(doc, references, "TLCPerson");
                tlcPerson.SetAttribute("eId", party.Id);
                tlcPerson.SetAttribute("href", "");
                tlcPerson.SetAttribute("showAs", party.Name);
            }
            ISet<PartyRole> roles = new HashSet<PartyRole>();
            foreach (IParty party in parties)
                if (party.Role.HasValue)
                    roles.Add(party.Role.Value);
            foreach (PartyRole role in roles) {
                XmlElement tlcRole = append(doc, references, "TLCRole");
                tlcRole.SetAttribute("eId", role.EId());
                tlcRole.SetAttribute("href", "");
                tlcRole.SetAttribute("showAs", role.ShowAs());
            }

            IEnumerable<IJudge> judges = Util.Descendants<IJudge>(judgment.Header);
            foreach (IJudge judge in judges) {
                XmlElement tlcPerson = append(doc, references, "TLCPerson");
                tlcPerson.SetAttribute("eId", judge.Id);
                tlcPerson.SetAttribute("href", "/" + judge.Id);
                tlcPerson.SetAttribute("showAs", judge.Name);
            }

            foreach (ILawyer lawyer in Util.Descendants<ILawyer>(judgment.Header)) {
                XmlElement tlcPerson = append(doc, references, "TLCPerson");
                tlcPerson.SetAttribute("eId", lawyer.Id);
                tlcPerson.SetAttribute("href", "/" + lawyer.Id);
                tlcPerson.SetAttribute("showAs", lawyer.Name);
            }

            IEnumerable<ILocation> locations = Util.Descendants<ILocation>(judgment.Header);
            foreach (ILocation loc in locations) {
                XmlElement tlcLocation = append(doc, references, "TLCLocation");
                tlcLocation.SetAttribute("eId", loc.Id);
                tlcLocation.SetAttribute("href", "/" + loc.Id);
                tlcLocation.SetAttribute("showAs", loc.Name);
            }

            foreach (IDocJurisdiction jd in metadata.Jurisdictions.Where(j => !j.Overridden)) {
                XmlElement tlcConcept = append(references, "TLCConcept");
                tlcConcept.SetAttribute("eId", jd.Id);
                tlcConcept.SetAttribute("href", "/" + jd.Id.Replace('-', '/'));
                tlcConcept.SetAttribute("showAs", jd.LongName);
                tlcConcept.SetAttribute("shortForm", jd.ShortName);
            }

            XmlElement proprietary = append(doc, meta, "proprietary");
            proprietary.SetAttribute("source", "#");

            if (court is not null) {
                XmlElement courtt = doc.CreateElement("uk", "court", ukns);
                proprietary.AppendChild(courtt);
                courtt.AppendChild(doc.CreateTextNode(court.Value.Code.ToString()));
            }
            if (metadata.Year is not null) {
                XmlElement year = doc.CreateElement("uk", "year", ukns);
                proprietary.AppendChild(year);
                year.AppendChild(doc.CreateTextNode(metadata.Year.ToString()));
            }
            if (metadata.Number is not null) {
                XmlElement number = doc.CreateElement("uk", "number", ukns);
                proprietary.AppendChild(number);
                number.AppendChild(doc.CreateTextNode(metadata.Number.ToString()));
            }
            if (metadata.Cite is not null) {
                XmlElement cite = doc.CreateElement("uk", "cite", ukns);
                proprietary.AppendChild(cite);
                cite.AppendChild(doc.CreateTextNode(metadata.Cite.ToString()));
            }


            foreach (var caseNo in metadata.CaseNos()) {
                MetadataExtensions.AddProprietaryField(proprietary, "caseNumber", caseNo);
            }

            foreach (IDocJurisdiction jd in metadata.Jurisdictions) {
                XmlElement juris = doc.CreateElement("uk", "jurisdiction", ukns);
                proprietary.AppendChild(juris);
                juris.AppendChild(doc.CreateTextNode(jd.ShortName));
            }

            if (metadata is IMetadataExtended extended)
                MetadataExtensions.AddProprietaryFields(proprietary, extended);

            if (true) {
                XmlElement parser = doc.CreateElement("uk", "parser", ukns);
                proprietary.AppendChild(parser);
                parser.AppendChild(doc.CreateTextNode(GetParserVersion()));
            }

            if (judgment.Metadata.ExternalAttachments is not null)
            foreach (var tuple in judgment.Metadata.ExternalAttachments.Select((attachment, i) => new { i, attachment })) {
                XmlElement hasAttachment = append(doc, references, "hasAttachment");
                var href = tuple.attachment.Link ?? "/" + metadata.ShortUriComponent + "/attachment/" + ( tuple.i + 1 ) + ".pdf";
                hasAttachment.SetAttribute("href", href);
                hasAttachment.SetAttribute("showAs", tuple.attachment.Type);
            }

        }

        Dictionary<string, Dictionary<string, string>> styles = metadata.CSSStyles();
        if (styles is not null) {
            XmlElement presentation = append(doc, meta, "presentation");
            presentation.SetAttribute("source", "#");
            XmlElement style = doc.CreateElement("style", "http://www.w3.org/1999/xhtml");
            presentation.AppendChild(style);
            style.AppendChild(doc.CreateTextNode("\n"));
            string css = CSS.Serialize(styles);
            style.AppendChild(doc.CreateTextNode(css));
        }

        return meta;
   }

}

class AttachmentMetadata : IMetadata {

    AttachmentType Type { get; init;}

    private readonly IMetadata prototype;

    private readonly int n;

    internal AttachmentMetadata(AttachmentType type, IMetadata prototype, int n) {
        Type = type;
        this.prototype = prototype;
        this.n = n;
    }

    public Court? Court => prototype.Court;

    public IEnumerable<IDocJurisdiction> Jurisdictions => prototype.Jurisdictions;
    
    public int? Year { get => prototype.Year; }

    public int? Number { get => prototype.Number; }

    public string Cite { get => prototype.Cite; }

    private string Extension { get =>  "/" + Enum.GetName(typeof(AttachmentType), Type).ToLower() + "/" + n; }

    public string ShortUriComponent { get => prototype.ShortUriComponent + Extension; }

    public string WorkThis { get => prototype.WorkURI + Extension; }
    public string WorkURI { get => prototype.WorkURI; }

    public string ExpressionThis { get => prototype.ExpressionUri + Extension; }
    public string ExpressionUri { get => prototype.ExpressionUri; }

    public string ManifestationThis { get => prototype.ExpressionUri + Extension + "/data.xml"; }
    public string ManifestationUri { get => prototype.ManifestationUri; }

    public INamedDate Date { get => prototype.Date; }

    public string Name { get => null; }

    public IEnumerable<string> CaseNos() => Enumerable.Empty<string>();

    internal Dictionary<string, Dictionary<string, string>> Styles { private get; init; }

    public Dictionary<string, Dictionary<string, string>> CSSStyles() => this.Styles;

    public IEnumerable<IExternalAttachment> ExternalAttachments { get => Enumerable.Empty<IExternalAttachment>(); }

}

}
