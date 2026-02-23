#nullable enable

using System.Text.Json.Serialization;

namespace Backlog.TreMetadata;

internal class IngestorOptions
{

    [JsonPropertyName("auto_publish")]
    public bool AutoPublish { get; set; }


    [JsonPropertyName("source_document")]
    public SourceDocument Source { get; set; }
}
