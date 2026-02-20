#nullable enable

using System;
using System.Text.Json.Serialization;

using TRE.Metadata.Enums;

namespace TRE.Metadata;

public interface IMetadataField
{
    /// <summary>
    ///     A UUID for this piece of metadata.
    ///     A new UUID should be generated only if the metadata value has changed; if there is an
    ///     existing piece of metadata with the same source and same value the existing `id` should
    ///     be used.
    /// </summary>
    Guid? Id { get; }

    /// <summary>
    ///     The name of this piece of metadata
    /// </summary>
    MetadataFieldName Name { get; }

    /// <summary>
    ///     The origin of this piece of metadata.
    /// </summary>
    MetadataSource Source { get; }

    /// <summary>
    ///     The timestamp this piece of metadata was first detected or added.
    /// </summary>
    DateTime Timestamp { get; }
}

/// <summary>
///     An additional metadata field, either extracted from the document or sourced from a supplementary file.
/// </summary>
public record MetadataField<T> : IMetadataField
{
    /// <summary>
    ///     A UUID for this piece of metadata.
    ///     A new UUID should be generated only if the metadata value has changed; if there is an
    ///     existing piece of metadata with the same source and same value the existing `id` should
    ///     be used.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

    /// <summary>
    ///     The name of this piece of metadata
    /// </summary>
    [JsonPropertyName("name")]
    public MetadataFieldName Name { get; init; }

    /// <summary>
    ///     The origin of this piece of metadata.
    /// </summary>
    [JsonPropertyName("source")]
    public MetadataSource Source { get; init; }

    /// <summary>
    ///     The timestamp this piece of metadata was first detected or added.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     A value for this metadata. May be either a plain string, or a JSON object with additional
    ///     complexity.
    /// </summary>
    [JsonPropertyName("value")]
    public required T Value { get; init; }
}
