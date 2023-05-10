
#nullable enable

using System;
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw {

enum DocType { PressSummary }

interface IAknDocument {

    DocType Type { get; }

    IAknMetadata Metadata { get; }

    IEnumerable<IBlock> Body { get; }

    IEnumerable<IImage> Images { get; }

}

interface IAknMetadata {

    IResource Source { get; }

    IResource Author { get; }

    string WorkURI { get; }

    string ExpressionURI { get; }

    string ManifestationURI { get => ExpressionURI + "/data.xml"; }

    INamedDate Date { get; }

    string? Name { get; }

    string? ProprietaryNamespace { get; }

    IList<Tuple<String, String>> Proprietary { get; }

    Dictionary<string, Dictionary<string, string>> CSSStyles { get; }

}

enum ResourceType { Oranization, Event, Person, Role, Concept }

interface IResource {

    ResourceType Type { get; }

    string ID { get; }

    string URI { get; }

    string? ShowAs { get; }

    string? ShortForm { get; }

}

}
