
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IMetadata {

    Court? Court();

    int? Year { get; }

    int? Number { get; }

    string Cite { get; }

    string DocumentId();

    string Date();

    string CaseName { get; }

    IEnumerable<string> CaseNos();

    Dictionary<string, Dictionary<string, string>> CSSStyles();

    IEnumerable<IExternalAttachment> ExternalAttachments { get; }

}

interface IComponentMetadata : IMetadata {

    string ComponentId { get; }

}

}
