using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.CaseLaw.Model;

namespace Backlog.Src
{
    internal class ExtendedMetadata : IMetadataExtended
    {
        internal JudgmentType Type { get; init; }

        public Court? Court { get; init; }

        public IEnumerable<IDocJurisdiction> Jurisdictions { get; init; }

        public int? Year { get; init; }

        public int? Number { get; init; }

        public string Cite { get; init; }

        public string ShortUriComponent { get; init; }

        public string WorkThis { get => WorkURI; }

        public string WorkURI { get; init; }

        public string ExpressionThis { get => ExpressionUri; }

        public string ExpressionUri { get; init; }

        public string ManifestationThis { get => ManifestationUri; }

        public string ManifestationUri { get; init; }

        public INamedDate Date { get; init; }

        public string Name { get; init; }

        public IEnumerable<IExternalAttachment> ExternalAttachments { get; init; }

        public List<string> CaseNumbers { get; init; }

        public IEnumerable<string> CaseNos() => CaseNumbers;

        public Dictionary<string, Dictionary<string, string>> CSSStyles() => [];

        public List<UK.Gov.NationalArchives.CaseLaw.Model.Party> Parties { get; init; } = [];

        public string SourceFormat { get; init; }

        public List<ICategory> Categories { get; init; } = [];
        
        public string NCN { get; init; }
        
        public string WebArchivingLink { get; init; }
    }
}
