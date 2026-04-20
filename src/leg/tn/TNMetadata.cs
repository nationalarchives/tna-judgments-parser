using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Common;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.TranspositionNotes {

/// <summary>
/// Metadata for Transposition Note documents.
/// Handles TN-specific URI generation based on filename and legislation mapping lookup.
/// </summary>
class TNMetadata : DocumentMetadata {

    public DateTime? LastModified { get; init; }

    // Additional metadata from CSV mapping
    public string DocumentMainType { get; init; }
    public string Department { get; init; }
    public string TnDate { get; init; }
    public string ModifiedDate { get; init; }
    public string TnType { get; init; }

    public int? TnYear { get; init; }

    internal static TNMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config, string filename) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }

        DateTime? modified = doc.PackageProperties.Modified;
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");

        string shortUri;
        string legislationUri;
        TNMappingRecord mappingRecord;

        try {
            mappingRecord = TNLegislationMapping.GetMappingRecord(filename);
        } catch (KeyNotFoundException ex) {
            throw new InvalidOperationException($"Cannot process TN file '{filename}': {ex.Message}", ex);
        }

        legislationUri = mappingRecord.LegislationUri;

        try {
            shortUri = TNLegislationMapping.BuildShortUriComponent(mappingRecord);
        } catch (ArgumentException ex) {
            throw new InvalidOperationException($"Cannot build URI for TN file '{filename}': {ex.Message}", ex);
        }

        return new TNMetadata {
            ShortUriComponent = shortUri,
            ExpressionDate = Builder.FormatDateOnly(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            LastModified = modified,
            Name = name,
            CSS = css,
            LegislationUri = legislationUri,
            DocumentMainType = TNLegislationMapping.NormalizeDocumentMainType(mappingRecord.TnType),
            Department = mappingRecord.Department,
            TnDate = mappingRecord.TnDate,
            ModifiedDate = mappingRecord.ModifiedDate,
            LegislationClass = mappingRecord.LegislationClass,
            TnType = mappingRecord.TnType,
            TnYear = mappingRecord.TnYear,
            LegislationYear = mappingRecord.LegislationYear,
            LegislationNumber = mappingRecord.LegislationNumber,
            WorkAuthor = Common.LegWorkAuthorMapping.GetWorkAuthorUri(mappingRecord.LegislationClass),
            LegislationTitle = mappingRecord.LegislationTitle,
            Publisher = AssociatedDocumentMapping.GetPublisher(filename)
        };
    }

}

}
