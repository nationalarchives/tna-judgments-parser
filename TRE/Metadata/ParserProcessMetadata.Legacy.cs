#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TRE.Metadata;

/// <summary>
///     Properties to be moved to metadata fields and then removed from the rest of the system
/// </summary>
public partial record ParserProcessMetadata
{
    [JsonPropertyName("uri")] public string? Uri { get; init; }
    [JsonPropertyName("court")] public required string Court { get; init; }
    [JsonPropertyName("cite")] public required string Cite { get; init; }
    [JsonPropertyName("date")] public required string Date { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("extensions")]
    public required UK.Gov.NationalArchives.Judgments.Api.Extensions Extensions { get; init; }

    [JsonPropertyName("attachments")]
    public required IEnumerable<UK.Gov.NationalArchives.Judgments.Api.ExternalAttachment> Attachments { get; init; }
}
