#nullable enable

using System.Text.Json.Serialization;

namespace Backlog.TreMetadata;

internal class Parameters
{

    [JsonPropertyName("TRE")]
    public required Tre TRE { get; init; }

    [JsonPropertyName("PARSER")]
    public required UK.Gov.NationalArchives.Judgments.Api.Meta PARSER { get; init; }

    [JsonPropertyName("INGESTER_OPTIONS")]
    public required IngestorOptions IngestorOptions { get; init; }
}
