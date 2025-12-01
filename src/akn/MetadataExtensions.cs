#nullable enable

using System.Xml;

using UK.Gov.Legislation.Judgments.AkomaNtoso;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace judgments.src.akn;

internal class MetadataExtensions
{
    internal static void AddProprietaryFields(XmlElement proprietary, IMetadataExtended meta)
    {
        if (meta.Parties is not null)
        {
            foreach (var party in meta.Parties)
            {
                if (party is not null)
                {
                    AddParty(proprietary, party);
                }
            }
        }

        if (meta.Categories is not null)
        {
            foreach (var cat in meta.Categories)
            {
                if (cat is not null)
                {
                    AddCategory(proprietary, cat);
                }
            }
        }

        if (meta.SourceFormat is not null)
        {
            AddProprietaryField(proprietary, "sourceFormat", meta.SourceFormat);
        }

        if (meta.WebArchivingLink is not null)
        {
            AddProprietaryField(proprietary, "webarchiving", meta.WebArchivingLink);
        }
    }

    private static void AddParty(XmlElement proprietary, Party party)
    {
        var e = AddProprietaryField(proprietary, "party", party.Name);
        e.SetAttribute("role", party.Role.ShowAs());
    }

    private static void AddCategory(XmlElement proprietary, ICategory cat)
    {
        var e = AddProprietaryField(proprietary, "category", cat.Name);
        if (cat.Parent is not null)
        {
            e.SetAttribute("parent", cat.Parent);
        }
    }

    internal static XmlElement AddProprietaryField(XmlElement proprietary, string name, string value)
    {
        var e = proprietary.OwnerDocument.CreateElement("uk", name, Metadata.ukns);
        proprietary.AppendChild(e);
        var text = proprietary.OwnerDocument.CreateTextNode(value);
        e.AppendChild(text);
        return e;
    }
}
