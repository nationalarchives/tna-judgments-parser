using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Common;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.ImpactAssessments {

/// <summary>
/// Metadata for Impact Assessment documents.
/// Handles IA-specific URI generation based on filename and legislation mapping lookup.
/// </summary>
class IAMetadata : DocumentMetadata {

    public DateTime? LastModified { get; init; }
    
    // Additional metadata from CSV mapping (derived from XML metadata files)
    public string DocumentStage { get; init; }
    public string DocumentMainType { get; init; }
    public string Department { get; init; }
    public string IADate { get; init; }
    public string PDFDate { get; init; }
    public string LegislationClass { get; init; }

    internal static IAMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config, string filename) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }

        DateTime? modified = doc.PackageProperties.Modified;
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");

        // Parse filename to get year/number for URI construction and metadata lookup
        string shortUri = null;
        string legislationUri = null;
        IAMappingRecord mappingRecord = null;

        var parsed = IALegislationMapping.ParseFilename(filename);
        if (parsed.HasValue) {
            var (year, number) = parsed.Value;
            legislationUri = IALegislationMapping.GetLegislationUri(year, number);
            
            // Look up full mapping record for additional metadata
            mappingRecord = IALegislationMapping.GetMappingRecord(year, number);
            
            // Build URI with stage component if available
            shortUri = IALegislationMapping.BuildShortUriComponent(year, number, mappingRecord?.DocumentStage);
        }

        return new IAMetadata {
            ShortUriComponent = shortUri,
            ExpressionDate = Builder.FormatDateAndTime(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            LastModified = modified,
            Name = name,
            CSS = css,
            LegislationUri = legislationUri,
            // Populate additional metadata from CSV mapping
            DocumentStage = mappingRecord?.DocumentStage,
            DocumentMainType = mappingRecord?.DocumentMainType,
            Department = mappingRecord?.Department,
            IADate = mappingRecord?.IADate,
            PDFDate = mappingRecord?.PDFDate,
            LegislationClass = mappingRecord?.LegislationClass
        };
    }

}

}

