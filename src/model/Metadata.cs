
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IMetadata {

    string DocumentId();

    string Date();

    Dictionary<string, Dictionary<string, string>> CSSStyles();

}

interface IComponentMetadata : IMetadata {

    string ComponentId { get; }

}

}
