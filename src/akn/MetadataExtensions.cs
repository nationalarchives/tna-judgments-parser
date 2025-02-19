
using System.Xml;

using UK.Gov.Legislation.Judgments.AkomaNtoso;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace judgments.src.akn
{

    class MetadataExtensions
    {

        internal static void AddProprietaryFields(XmlElement proprietary, IMetadataExtended meta) {
            foreach (var party in meta.Parties)
                AddParty(proprietary, party);
            foreach (var cat in meta.Categories)
                AddCategory(proprietary, cat);
            AddSourceFormat(proprietary, meta.SourceFormat);
        }

        static void AddParty(XmlElement proprietary, UK.Gov.NationalArchives.CaseLaw.Model.Party party)
        {
            XmlElement e = AddProprietaryField(proprietary, "party", party.Name);
            e.SetAttribute("role", party.Role.ShowAs());
        }

        static void AddSourceFormat(XmlElement proprietary, string sourceFormat)
        {
            AddProprietaryField(proprietary, "sourceFormat", sourceFormat);
        }

        static void AddCategory(XmlElement proprietary, ICategory cat)
        {
            var e = AddProprietaryField(proprietary, "category", cat.Name);
            if (cat.Parent is not null)
                e.SetAttribute("parent", cat.Parent);
        }

        internal static XmlElement AddProprietaryField(XmlElement proprietary, string name, string value)
        {
            XmlElement e = proprietary.OwnerDocument.CreateElement("uk", name, Metadata.ukns);
            proprietary.AppendChild(e);
            var text = proprietary.OwnerDocument.CreateTextNode(value);
            e.AppendChild(text);
            return e;
        }

    }

}
