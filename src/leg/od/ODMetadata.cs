using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Common;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.OtherDocuments {

/// <summary>
/// Metadata for Other Document (ukm:OtherDocument) entries.
/// Handles OD-specific URI generation based on filename and legislation mapping lookup.
/// </summary>
class ODMetadata : DocumentMetadata {

    public DateTime? LastModified { get; init; }

    public string DocumentMainType { get; init; }
    public string Department { get; init; }
    public string OdDate { get; init; }
    public string ModifiedDate { get; init; }
    public string OdType { get; init; }

    public int? OdYear { get; init; }
    public int OdVersion { get; init; }

    internal static ODMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config, string filename) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }

        DateTime? modified = doc.PackageProperties.Modified;
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");

        string shortUri;
        string legislationUri;
        ODMappingRecord mappingRecord;

        try {
            mappingRecord = ODLegislationMapping.GetMappingRecord(filename);
        } catch (KeyNotFoundException ex) {
            throw new InvalidOperationException($"Cannot process OD file '{filename}': {ex.Message}", ex);
        }

        legislationUri = mappingRecord.LegislationUri;

        try {
            shortUri = ODLegislationMapping.BuildShortUriComponent(mappingRecord);
        } catch (ArgumentException ex) {
            throw new InvalidOperationException($"Cannot build URI for OD file '{filename}': {ex.Message}", ex);
        }

        return new ODMetadata {
            ShortUriComponent = shortUri,
            ExpressionDate = Builder.FormatDateOnly(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            LastModified = modified,
            Name = name,
            CSS = css,
            LegislationUri = legislationUri,
            DocumentMainType = ODLegislationMapping.NormalizeDocumentMainType(mappingRecord.OdType),
            Department = mappingRecord.Department,
            OdDate = mappingRecord.OdDate,
            ModifiedDate = mappingRecord.ModifiedDate,
            LegislationClass = mappingRecord.LegislationClass,
            OdType = mappingRecord.OdType,
            OdYear = mappingRecord.OdYear,
            OdVersion = mappingRecord.OdVersion,
            LegislationYear = mappingRecord.LegislationYear,
            LegislationNumber = mappingRecord.LegislationNumber,
            WorkAuthor = Common.LegWorkAuthorMapping.GetWorkAuthorUri(mappingRecord.LegislationClass),
            LegislationTitle = mappingRecord.LegislationTitle,
            Publisher = AssociatedDocumentMapping.GetPublisher(filename)
        };
    }

}

}
