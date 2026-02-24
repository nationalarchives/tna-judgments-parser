#nullable enable

using System.Text.Json.Serialization;

namespace Backlog.TreMetadata;

internal class IngestorOptions
{

    [JsonPropertyName("auto_publish")]
    public bool AutoPublish { get; init; }


    [JsonPropertyName("source_document")]
    public required SourceDocument Source { get; init; }
}
