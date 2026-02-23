#nullable enable

using System.Text.Json.Serialization;

namespace Backlog.TreMetadata;

internal class SourceDocument {

    [JsonPropertyName("format")]
    public string Format { get; set; }

    [JsonPropertyName("file_hash")]
    public string Hash { get; set; }

}
