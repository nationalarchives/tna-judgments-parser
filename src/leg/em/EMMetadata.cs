using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Common;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.ExplanatoryMemoranda {

/// <summary>
/// Metadata for Explanatory Memoranda documents.
/// Handles EM-specific URI generation based on filename and legislation mapping lookup.
/// </summary>
class EMMetadata : DocumentMetadata {

    public DateTime? LastModified { get; init; }
    
    // Additional metadata from CSV mapping
    public string DocumentMainType { get; init; }
    public string Department { get; init; }
    public string EmDate { get; init; }
    public string ModifiedDate { get; init; }
    public string LegislationClass { get; init; }
    public string EmType { get; init; }
    
    // Year and version values (for explicit metadata in proprietary section)
    public int? EmYear { get; init; }
    public int EmVersion { get; init; }
    public int? LegislationYear { get; init; }
    public string LegislationNumber { get; init; }

    internal static EMMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config, string filename) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }

        DateTime? modified = doc.PackageProperties.Modified;
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");

        // Look up metadata by filename (CSV has a filename column)
        string shortUri = null;
        string legislationUri = null;
        EMMappingRecord mappingRecord;

        try {
            mappingRecord = EMLegislationMapping.GetMappingRecord(filename);
        } catch (KeyNotFoundException ex) {
            throw new InvalidOperationException($"Cannot process EM file '{filename}': {ex.Message}", ex);
        }
        
        legislationUri = mappingRecord.LegislationUri;
        
        // Build URI component
        try {
            shortUri = EMLegislationMapping.BuildShortUriComponent(mappingRecord);
        } catch (ArgumentException ex) {
            throw new InvalidOperationException($"Cannot build URI for EM file '{filename}': {ex.Message}", ex);
        }

        return new EMMetadata {
            ShortUriComponent = shortUri,
            ExpressionDate = Builder.FormatDateAndTime(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            LastModified = modified,
            Name = name,
            CSS = css,
            LegislationUri = legislationUri,
            // Populate additional metadata from CSV mapping
            DocumentMainType = EMLegislationMapping.NormalizeDocumentMainType(mappingRecord.EmType),
            Department = mappingRecord.Department,
            EmDate = mappingRecord.EmDate,
            ModifiedDate = mappingRecord.ModifiedDate,
            LegislationClass = mappingRecord.LegislationClass,
            EmType = mappingRecord.EmType,
            // Year and version values
            EmYear = mappingRecord.EmYear,
            EmVersion = mappingRecord.EmVersion,
            LegislationYear = mappingRecord.LegislationYear,
            LegislationNumber = mappingRecord.LegislationNumber
        };
    }

}

}
