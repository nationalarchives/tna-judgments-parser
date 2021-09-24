
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IMetadata {

    Court? Court();

    int? Year { get; }

    int? Number { get; }

    string DocumentId();

    string Date();

    IEnumerable<string> CaseNos();

    Dictionary<string, Dictionary<string, string>> CSSStyles();

}

interface IComponentMetadata : IMetadata {

    string ComponentId { get; }

}

}
