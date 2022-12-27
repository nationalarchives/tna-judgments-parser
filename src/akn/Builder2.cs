
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using UK.Gov.NationalArchives.CaseLaw;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class DocBuilder : Builder {

    protected override string MakeDivisionId(IDivision div) {
        return null;
    }

    public static XmlDocument Build(IAknDocument document) {
        DocBuilder builder = new DocBuilder();
        builder.Build1(document);
        AddHash(builder.doc);
        return builder.doc;
    }

    private void Build1(IAknDocument document) {
        XmlElement akomaNtoso = CreateAndAppend("akomaNtoso", doc);
        akomaNtoso.SetAttribute("xmlns:uk", Metadata.ukns);
        XmlElement main = CreateAndAppend("doc", akomaNtoso);
        main.SetAttribute("name", Enum.GetName(typeof(DocType), document.Type).ToLower());
        MetadataBuilder.Build(main, document.Metadata);
        AddBody(main, document.Body);
    }

    private void AddBody(XmlElement main, IEnumerable<IBlock> contents) {
        XmlElement body = CreateAndAppend("mainBody", main);
        blocks(body, contents);
    }

}

class MetadataBuilder {

    private static XmlElement CreateAndAppend(string name, XmlNode parent) {
        const string ns = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
        return CreateAndAppend(name, parent, ns);
    }
    private static XmlElement CreateAndAppend(string name, XmlNode parent, string ns) {
        XmlElement e = parent.OwnerDocument.CreateElement(name, ns);
        parent.AppendChild(e);
        return e;
    }

    internal static void Build(XmlElement main, IAknMetadata metadata) {
        XmlElement meta = CreateAndAppend("meta", main);
        Identification(meta, metadata);
        References(meta, metadata);
        Proprietary(meta, metadata);
        Presentation(meta, metadata);
    }

    private static void Identification(XmlElement meta, IAknMetadata metadata) {
        XmlElement identification = CreateAndAppend("identification", meta);
        identification.SetAttribute("source", "#" + metadata.Source.ID);
        Work(identification, metadata);
        Expression(identification, metadata);
        Manifestation(identification, metadata);
    }

    private static void Work(XmlElement identification, IAknMetadata metadata) {
        XmlElement work = CreateAndAppend("FRBRWork", identification);
        XmlElement that = CreateAndAppend("FRBRthis", work);
        that.SetAttribute("value", metadata.WorkURI);
        XmlElement uri = CreateAndAppend("FRBRuri", work);
        uri.SetAttribute("value", metadata.WorkURI);
        XmlElement date = CreateAndAppend("FRBRdate", work);
        date.SetAttribute("date", metadata.Date.Date);
        date.SetAttribute("name", metadata.Date.Name);
        XmlElement author = CreateAndAppend("FRBRauthor", work);
        author.SetAttribute("href", "#" + metadata.Author.ID);
        XmlElement country = CreateAndAppend("FRBRcountry", work);
        country.SetAttribute("value", "GB-UKM");
        if (metadata.Name is not null) {
            XmlElement name = CreateAndAppend("FRBRname", work);
            name.SetAttribute("value", metadata.Name);
        }
    }

    private static void Expression(XmlElement identification, IAknMetadata metadata) {
        XmlElement expression = CreateAndAppend("FRBRExpression", identification);
        XmlElement that = CreateAndAppend("FRBRthis", expression);
        that.SetAttribute("value", metadata.ExpressionURI);
        XmlElement uri = CreateAndAppend("FRBRuri", expression);
        uri.SetAttribute("value", metadata.ExpressionURI);
        XmlElement date = CreateAndAppend("FRBRdate", expression);
        date.SetAttribute("date", metadata.Date.Date);
        date.SetAttribute("name", metadata.Date.Name);
        XmlElement author = CreateAndAppend("FRBRauthor", expression);
        author.SetAttribute("href", "#" + metadata.Author.ID);
        // XmlElement expAuthoritative = append(doc, expression, "FRBRauthoritative");
        // expAuthoritative.SetAttribute("value", "true");
        XmlElement language = CreateAndAppend("FRBRlanguage", expression);
        language.SetAttribute("language", "eng");
    }

    private static void Manifestation(XmlElement identification, IAknMetadata metadata) {
        XmlElement manifestation = CreateAndAppend("FRBRManifestation", identification);
        XmlElement that = CreateAndAppend("FRBRthis", manifestation);
        that.SetAttribute("value", metadata.ManifestationURI);
        XmlElement uri = CreateAndAppend("FRBRuri", manifestation);
        uri.SetAttribute("value", metadata.ManifestationURI);
        XmlElement date = CreateAndAppend("FRBRdate", manifestation);
        date.SetAttribute("date", DateTime.UtcNow.ToString("s"));
        date.SetAttribute("name", "transform");
        XmlElement author = CreateAndAppend("FRBRauthor", manifestation);
        author.SetAttribute("href", "#tna");
        XmlElement format = CreateAndAppend("FRBRformat", manifestation);
        format.SetAttribute("value", "application/xml");
    }

    private static void References(XmlElement meta, IAknMetadata metadata) {
        XmlElement references = CreateAndAppend("references", meta);
        references.SetAttribute("source", "#" + metadata.Source.ID);
        Reference(references, metadata.Source);
        Reference(references, metadata.Author);
    }
    private static void Reference(XmlElement references, IResource resource) {
        string name;
        if (resource.Type == ResourceType.Oranization)
            name = "TLCOrganization";
        else if (resource.Type == ResourceType.Person)
            name = "TLCPerson";
        else if (resource.Type == ResourceType.Role)
            name = "TLCRole";
        else if (resource.Type == ResourceType.Event)
            name = "TLCEvent";
        else
            name = "TLCConcept";
        XmlElement e = CreateAndAppend(name, references);
        e.SetAttribute("eId", resource.ID);
        e.SetAttribute("href", resource.URI);
        if (resource.ShowAs is not null)
            e.SetAttribute("showAs", resource.ShowAs);
        if (resource.ShortForm is not null)
            e.SetAttribute("shortForm", resource.ShortForm);
    }

    private static void Proprietary(XmlElement meta, IAknMetadata metadata) {
        // if (metadata.Proprietary is null)
        //     return;
        // if (!metadata.Proprietary.Any())
        //     return;
        XmlElement prop = CreateAndAppend("proprietary", meta);
        prop.SetAttribute("source", "#" + metadata.Source.ID);
        foreach (var tuple in metadata.Proprietary) {
            string name = tuple.Item1;
            string value = tuple.Item2;
            XmlElement e = CreateAndAppend(name, prop, metadata.ProprietaryNamespace);
            XmlText text = meta.OwnerDocument.CreateTextNode(value);
            e.AppendChild(text);
        }
    }

    private static void Presentation(XmlElement meta, IAknMetadata metadata) {
        IDictionary<string, IDictionary<string, string>> styles = metadata.CSSStyles;
        if (styles is null)
            return;
        XmlElement presentation = CreateAndAppend("presentation", meta);
        presentation.SetAttribute("source", "#");
        XmlElement style = meta.OwnerDocument.CreateElement("style", "http://www.w3.org/1999/xhtml");
        presentation.AppendChild(style);
        style.AppendChild(meta.OwnerDocument.CreateTextNode("\n"));
        // string css = CSS.Serialize(styles);
        // style.AppendChild(meta.OwnerDocument.CreateTextNode(css));
    }

}

}
