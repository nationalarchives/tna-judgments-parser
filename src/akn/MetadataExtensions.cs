#nullable enable

using System.Xml;

using UK.Gov.Legislation.Judgments.AkomaNtoso;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace judgments.src.akn;

internal static class MetadataExtensions
{
    internal static void AddProprietaryFields(this XmlElement proprietary, IMetadataExtended meta)
    {
        foreach (var party in meta.Parties)
        {
            proprietary.AddParty(party);
        }

        foreach (var cat in meta.Categories)
        {
            proprietary.AddCategory(cat);
        }

        if (meta.SourceFormat is not null)
        {
            proprietary.AddProprietaryField("sourceFormat", meta.SourceFormat);
        }

        if (meta.WebArchivingLink is not null)
        {
            proprietary.AddProprietaryField("webarchiving", meta.WebArchivingLink);
        }
    }

    private static void AddParty(this XmlElement proprietary, Party party)
    {
        var e = proprietary.AddProprietaryField("party", party.Name);
        e.SetAttribute("role", party.Role.ShowAs());
    }

    private static void AddCategory(this XmlElement proprietary, ICategory cat)
    {
        var e = proprietary.AddProprietaryField("category", cat.Name);
        if (cat.Parent is not null)
        {
            e.SetAttribute("parent", cat.Parent);
        }
    }

    internal static XmlElement AddProprietaryField(this XmlElement proprietary, string name, string value)
    {
        var e = proprietary.OwnerDocument.CreateElement("uk", name, Metadata.ukns);
        proprietary.AppendChild(e);
        var text = proprietary.OwnerDocument.CreateTextNode(value);
        e.AppendChild(text);
        return e;
    }
}
