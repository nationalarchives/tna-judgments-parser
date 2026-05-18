#nullable enable

using System.Text.Json.Serialization;

namespace Backlog.TreMetadata;

internal class IngestorOptions
{

    [JsonPropertyName("auto_publish")]
    public bool AutoPublish { get; init; }

    [JsonPropertyName("error_on_existing_document")]
    public bool ErrorOnExistingDocument { get; init; }

    [JsonPropertyName("source_document")]
    public required SourceDocument Source { get; init; }
}
