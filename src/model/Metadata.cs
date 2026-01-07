
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface INamedDate {

    string Date { get; }

    string Name { get; }

}

interface IMetadata {

    Court? Court { get; }

    IEnumerable<IDocJurisdiction> Jurisdictions { get; }

    int? Year { get; }

    int? Number { get; }

    string Cite { get; }

    string ShortUriComponent { get; }

    string WorkThis { get; }
    string WorkURI { get; }

    string ExpressionThis { get; }
    string ExpressionUri { get; }

    string ManifestationThis { get; }
    string ManifestationUri { get; }

    INamedDate Date { get; }

    string Name { get; }

    IEnumerable<string> CaseNos();

    Dictionary<string, Dictionary<string, string>> CSSStyles();

    IEnumerable<IExternalAttachment> ExternalAttachments { get; }

}

}
