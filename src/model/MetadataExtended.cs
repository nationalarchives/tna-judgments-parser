
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw.Model
{

    interface IMetadataExtended : IMetadata
    {

        string SourceFormat { get; }

        List<string> CaseNumbers { get; }

        List<UK.Gov.NationalArchives.CaseLaw.Model.Party> Parties { get; }

        List<ICategory> Categories { get; }

        string NCN { get; }

        string WebArchivingLink { get; }
    }

    public class Party {

        public string Name { get; init; }

        public PartyRole Role { get; init; }

    }

    public interface ICategory
    {

        string Name { get; }

        string Parent { get; }

    }

}
