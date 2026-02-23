#nullable enable

using System.Text.Json.Serialization;

namespace Backlog.TreMetadata;

internal class Parameters
{

    [JsonPropertyName("TRE")]
    public Backlog.Src.TRE.Metadata TRE { get; set; }

    [JsonPropertyName("PARSER")]
    public UK.Gov.NationalArchives.Judgments.Api.Meta PARSER { get; set; }

    [JsonPropertyName("INGESTER_OPTIONS")]
    public IngestorOptions IngestorOptions { get; set; }
}
