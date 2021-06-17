
using System;
using System.Collections.Generic;
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

    public static XmlElement make(XmlDocument doc, IMetadata metadata, bool includeReferences) {

        string docId = metadata.DocumentId();
        string compId = metadata is IComponentMetadata c ? c.ComponentId : docId;
        string date = metadata.Date() ?? "9999-01-01";

        XmlElement meta = doc.CreateElement("meta", ns);

        XmlElement identification = append(doc, meta, "identification");
        identification.SetAttribute("source", "-");

        XmlElement work = append(doc, identification, "FRBRWork");
        XmlElement workThis = append(doc, work, "FRBRthis");
        workThis.SetAttribute("value", compId);
        XmlElement workURI = append(doc, work, "FRBRuri");
        workURI.SetAttribute("value", docId);
        XmlElement workDate = append(doc, work, "FRBRdate");
        workDate.SetAttribute("date", date);
        workDate.SetAttribute("name", "-");
        XmlElement workAuthor = append(doc, work, "FRBRauthor");
        workAuthor.SetAttribute("href", "-");
        XmlElement workCountry = append(doc, work, "FRBRcountry");
        workCountry.SetAttribute("value", "GB-UKM");

        XmlElement expression = append(doc, identification, "FRBRExpression");
        XmlElement expThis = append(doc, expression, "FRBRthis");
        expThis.SetAttribute("value", compId + "/eng");
        XmlElement expURI = append(doc, expression, "FRBRuri");
        expURI.SetAttribute("value", docId + "/eng");
        XmlElement expDate = append(doc, expression, "FRBRdate");
        expDate.SetAttribute("date", date);
        expDate.SetAttribute("name", "-");
        XmlElement expAuthor = append(doc, expression, "FRBRauthor");
        expAuthor.SetAttribute("href", "-");
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
            references.SetAttribute("source", "-");
            XmlElement tna = append(doc, references, "TLCOrganization");
            tna.SetAttribute("eId", "tna");
            tna.SetAttribute("href", "https://www.nationalarchives.gov.uk/");
            tna.SetAttribute("showAs", "The National Archives");
        }

        Dictionary<string, Dictionary<string, string>> styles = metadata.CSSStyles();
        if (styles is not null) {
            XmlElement presentation = append(doc, meta, "presentation");
            presentation.SetAttribute("source", "-");
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

    public string DocumentId() { return prototype.DocumentId(); }

    public string ComponentId {
        get { return prototype.DocumentId() + "/annex/" + n; }
    }

    public string Date() { return prototype.Date(); }

    public Dictionary<string, Dictionary<string, string>> CSSStyles() => null;

}

}