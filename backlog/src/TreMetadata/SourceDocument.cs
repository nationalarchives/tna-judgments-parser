#nullable enable

using System.Text.Json.Serialization;

namespace Backlog.TreMetadata;

internal class SourceDocument {

    [JsonPropertyName("format")]
    public required string Format { get; init; }

    [JsonPropertyName("file_hash")]
    public required string Hash { get; init; }

}
