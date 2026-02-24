#nullable enable

namespace Backlog.TreMetadata;

internal class Payload
{

    public required string Filename { get; init; }

    public string Xml { get; init; } = "judgment.xml";

    public string Metadata { get; init; } = "bulk-metadata.json";

    public string[] Images { get; init; } = [];

    public string? Log { get; init; } = "parser.log";

}
