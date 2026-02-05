#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

using TRE.Metadata.Enums;

namespace TRE.Metadata;

/// <summary>
///     Metadata about a document or its processing which has been generated or collated as a
///     result of the Find Case Law parsing process.
/// </summary>
public partial record ParserProcessMetadata
{
    [JsonPropertyName("documentType")] public DocumentType DocumentType { get; init; }

    /// <summary>
    ///     A list of error messages raised whilst parsing this document.
    /// </summary>
    [JsonPropertyName("error-messages")]
    public object[] ErrorMessages { get; init; } = [];

    /// <summary>
    ///     A list of additional metadata fields, either extracted from the document or sourced from
    ///     a supplementary file.
    /// </summary>
    [JsonPropertyName("metadata_fields")]
    public List<IMetadataField> MetadataFields { get; init; } = [];

    /// <summary>
    ///     Information about the primary source file which was parsed. This should usually be
    ///     describing a document file such as a `.docx` or a `.pdf`, and not a container format such
    ///     as `.zip`.
    /// </summary>
    [JsonPropertyName("primary_source")]
    public required PrimarySourceFile PrimarySource { get; init; }

    /// <summary>
    ///     An indicator of if the XML of the document contains body text which is renderable for
    ///     human consumption, instead of only being a stub containing metadata for a static asset.
    /// </summary>
    [JsonPropertyName("xml_contains_document_text")]
    public bool XmlContainsDocumentText { get; init; }
}
