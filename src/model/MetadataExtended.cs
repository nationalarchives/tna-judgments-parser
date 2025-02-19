
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw.Model
{

    interface IMetadataExtended : IMetadata
    {

        public string SourceFormat { get; }

        public List<string> CaseNumbers { get; }

        public List<UK.Gov.NationalArchives.CaseLaw.Model.Party> Parties { get; }

        public List<ICategory> Categories { get; }

    }

    public class Party {

        public string Name { get; init; }

        public PartyRole Role { get; init; }

    }

    public interface ICategory
    {

        public string Name { get; }

        public string Parent { get; }

    }

}
