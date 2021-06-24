
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IMetadata {

    Court? Court();

    string DocumentId();

    string Date();

    string CaseNo();

    Dictionary<string, Dictionary<string, string>> CSSStyles();

}

interface IComponentMetadata : IMetadata {

    string ComponentId { get; }

}

}
