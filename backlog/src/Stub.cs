#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;

using Backlog.Src;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace Backlog;

internal class Stub
{
    private const string AknNamespace = "http://docs.oasis-open.org/legaldocml/ns/akn/3.0";
    private const string UkNamespace = "https://caselaw.nationalarchives.gov.uk/akn";

    private readonly XmlDocument document = new();
    private readonly StubMetadata stubMetadata;

    private Stub(StubMetadata stubMetadata)
    {
        this.stubMetadata = stubMetadata;

        var root = CreateAndAppend("akomaNtoso", document);

        var main = CreateAndAppend("judgment", root);
        main.SetAttribute("name", stubMetadata.Type.ToString().ToLower());

        Meta(main);
        Body(main);
    }

    internal static Stub Make(StubMetadata data)
    {
        return new Stub(data);
    }

    private XmlElement CreateAndAppend(string name, XmlNode parent)
    {
        var e = document.CreateElement(name, AknNamespace);
        parent.AppendChild(e);
        return e;
    }

    private void Meta(XmlElement judgment)
    {
        var meta = CreateAndAppend("meta", judgment);

        Identification(meta);
        Lifecycle(meta);
        References(meta);
        Proprietary(meta);
    }

    private void Identification(XmlElement meta)
    {
        var identification = CreateAndAppend("identification", meta);
        identification.SetAttribute("source", "#tna");

        Work(identification);
        Expression(identification);
        Manifestation(identification);
    }

    private void Work(XmlElement identification)
    {
        var work = CreateAndAppend("FRBRWork", identification);

        var ths = CreateAndAppend("FRBRthis", work);
        ths.SetAttribute("value", stubMetadata.WorkThis);

        var uri = CreateAndAppend("FRBRuri", work);
        uri.SetAttribute("value", stubMetadata.WorkURI);

        var date = CreateAndAppend("FRBRdate", work);
        date.SetAttribute("date", stubMetadata.Date?.Date);
        if (stubMetadata.Date?.Date == "1000-01-01")
        {
            date.SetAttribute("name", "dummy");
        }
        else
        {
            date.SetAttribute("name", stubMetadata.Date?.Name);
        }

        var author = CreateAndAppend("FRBRauthor", work);
        author.SetAttribute("href", "#" + Metadata.MakeCourtId(stubMetadata.Court));

        var country = CreateAndAppend("FRBRcountry", work);
        country.SetAttribute("value", "GB-UKM");

        if (stubMetadata.Name is not null)
        {
            var name = CreateAndAppend("FRBRname", work);
            name.SetAttribute("value", stubMetadata.Name);
        }
    }

    private void Expression(XmlElement identification)
    {
        var expr = CreateAndAppend("FRBRExpression", identification);

        var ths = CreateAndAppend("FRBRthis", expr);
        ths.SetAttribute("value", stubMetadata.ExpressionThis);

        var uri = CreateAndAppend("FRBRuri", expr);
        uri.SetAttribute("value", stubMetadata.ExpressionUri);

        var date = CreateAndAppend("FRBRdate", expr);
        date.SetAttribute("date", stubMetadata.Date?.Date);
        date.SetAttribute("name", stubMetadata.Date?.Name);

        var author = CreateAndAppend("FRBRauthor", expr);
        author.SetAttribute("href", "#" + Metadata.MakeCourtId(stubMetadata.Court));

        var lang = CreateAndAppend("FRBRlanguage", expr);
        lang.SetAttribute("language", "eng");
    }

    private void Manifestation(XmlElement identification)
    {
        var mani = CreateAndAppend("FRBRManifestation", identification);

        var ths = CreateAndAppend("FRBRthis", mani);
        ths.SetAttribute("value", stubMetadata.ManifestationThis);

        var uri = CreateAndAppend("FRBRuri", mani);
        uri.SetAttribute("value", stubMetadata.ManifestationUri);

        var date = CreateAndAppend("FRBRdate", mani);
        date.SetAttribute("date", DateTime.UtcNow.ToString("s"));
        date.SetAttribute("name", "transform");

        var author = CreateAndAppend("FRBRauthor", mani);
        author.SetAttribute("href", "#tna");

        var format = CreateAndAppend("FRBRformat", mani);
        format.SetAttribute("value", "application/xml");
    }

    private void Lifecycle(XmlElement meta)
    {
        var lifecycle = CreateAndAppend("lifecycle", meta);
        lifecycle.SetAttribute("source", "#");

        var eventRef = CreateAndAppend("eventRef", lifecycle);
        eventRef.SetAttribute("date", stubMetadata.Date.Date);
        eventRef.SetAttribute("refersTo", "#" + Metadata.MakeDateId(stubMetadata.Date));
        eventRef.SetAttribute("source", "#");
    }

    private void References(XmlElement meta)
    {
        var references = CreateAndAppend("references", meta);
        references.SetAttribute("source", "#tna");

        var tna = CreateAndAppend("TLCOrganization", references);
        tna.SetAttribute("eId", "tna");
        tna.SetAttribute("href", "https://www.nationalarchives.gov.uk/");
        tna.SetAttribute("showAs", "The National Archives");

        if (stubMetadata.Court is not null)
        {
            var tldOrg = CreateAndAppend("TLCOrganization", references);
            tldOrg.SetAttribute("eId", Metadata.MakeCourtId(stubMetadata.Court));
            tldOrg.SetAttribute("href", stubMetadata.Court.URL);
            tldOrg.SetAttribute("showAs", stubMetadata.Court.Name);
        }

        var tlcEvent = CreateAndAppend("TLCEvent", references);
        tlcEvent.SetAttribute("eId", Metadata.MakeDateId(stubMetadata.Date));
        tlcEvent.SetAttribute("href", "#");
        tlcEvent.SetAttribute("showAs", stubMetadata.Date.Name);
    }

    private void Proprietary(XmlElement meta)
    {
        var proprietary = CreateAndAppend("proprietary", meta);
        proprietary.SetAttribute("xmlns:uk", UkNamespace);
        proprietary.SetAttribute("source", "#");

        if (stubMetadata.Court is not null)
        {
            CreateAndAppendUk(proprietary, "court", stubMetadata.Court.Code);
        }

        foreach (var jurisdiction in stubMetadata.Jurisdictions)
        {
            CreateAndAppendUk(proprietary, "jurisdiction", jurisdiction.ShortName);
        }

        CreateAndAppendUk(proprietary, "year", stubMetadata.Date.Date[..4]);

        foreach (var caseNo in stubMetadata.CaseNumbers)
        {
            CreateAndAppendUk(proprietary, "caseNumber", caseNo);
        }

        foreach (var pty in stubMetadata.Parties)
        {
            var party = CreateAndAppendUk(proprietary, "party", pty.Name);
            party.SetAttribute("role", pty.Role.ShowAs());
        }

        foreach (var cat in stubMetadata.Categories)
        {
            var category = CreateAndAppendUk(proprietary, "category", cat.Name);
            if (cat.Parent is not null)
            {
                category.SetAttribute("parent", cat.Parent);
            }
        }

        if (!string.IsNullOrWhiteSpace(stubMetadata.Cite))
        {
            CreateAndAppendUk(proprietary, "cite", stubMetadata.Cite);
        }

        if (!string.IsNullOrWhiteSpace(stubMetadata.WebArchivingLink))
        {
            CreateAndAppendUk(proprietary, "webarchiving", stubMetadata.WebArchivingLink);
        }

        CreateAndAppendUk(proprietary, "sourceFormat", stubMetadata.SourceFormat);
        CreateAndAppendUk(proprietary, "parser", Metadata.GetParserVersion());
    }

    private XmlElement CreateAndAppendUk(XmlElement proprietary, string name, string value)
    {
        var e = document.CreateElement("uk", name, UkNamespace);
        proprietary.AppendChild(e);

        e.AppendChild(document.CreateTextNode(value));
        return e;
    }

    private void Body(XmlElement judgment)
    {
        CreateAndAppend("header", judgment);
        var body = CreateAndAppend("judgmentBody", judgment);
        var decision = CreateAndAppend("decision", body);
        CreateAndAppend("p", decision);
    }

    public List<ValidationEventArgs> Validate()
    {
        Validator validator = new();
        return validator.Validate(document);
    }

    public string Serialize()
    {
        using MemoryStream stream = new();
        Serializer.Serialize(document, stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
