#nullable enable

using System.Text.Json.Serialization;

using TRE.Metadata.Enums;

namespace TRE.Metadata;

/// <summary>
///     Information about the primary source file which was parsed. This should usually be
///     describing a document file such as a `.docx` or a `.pdf`, and not a container format such
///     as `.zip`.
/// </summary>
public record PrimarySourceFile
{
    /// <summary>
    ///     The filename (including extension) of the file which was parsed.
    /// </summary>
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    /// <summary>
    ///     The MIME type of the file.
    /// </summary>
    [JsonPropertyName("mimetype")]
    public required string Mimetype { get; init; }

    /// <summary>
    ///     The route which the file took to reach the parser.
    /// </summary>
    [JsonPropertyName("route")]
    public Route Route { get; init; }

    /// <summary>
    ///     The SHA256 hash of the file.
    /// </summary>
    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}
