#nullable enable

namespace Backlog.TreMetadata;

internal class Tre
{
    public required string Reference { get; init; }

    public required Payload Payload { get; init; }
}
