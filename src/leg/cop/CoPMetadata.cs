using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Common;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.CodesOfPractice {

/// <summary>
/// Metadata for Code of Practice documents.
/// Handles CoP-specific URI generation based on filename and legislation mapping lookup.
/// </summary>
class CoPMetadata : DocumentMetadata {

    public DateTime? LastModified { get; init; }

    public string DocumentMainType { get; init; }
    public string Department { get; init; }
    public string CopDate { get; init; }
    public string ModifiedDate { get; init; }
    public string CopType { get; init; }

    public int? CopYear { get; init; }
    public int CopVersion { get; init; }

    internal static CoPMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config, string filename) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }

        DateTime? modified = doc.PackageProperties.Modified;
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");

        string shortUri;
        string legislationUri;
        CoPMappingRecord mappingRecord;

        try {
            mappingRecord = CoPLegislationMapping.GetMappingRecord(filename);
        } catch (KeyNotFoundException ex) {
            throw new InvalidOperationException($"Cannot process CoP file '{filename}': {ex.Message}", ex);
        }

        legislationUri = mappingRecord.LegislationUri;

        try {
            shortUri = CoPLegislationMapping.BuildShortUriComponent(mappingRecord);
        } catch (ArgumentException ex) {
            throw new InvalidOperationException($"Cannot build URI for CoP file '{filename}': {ex.Message}", ex);
        }

        return new CoPMetadata {
            ShortUriComponent = shortUri,
            ExpressionDate = Builder.FormatDateOnly(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            LastModified = modified,
            Name = name,
            CSS = css,
            LegislationUri = legislationUri,
            DocumentMainType = CoPLegislationMapping.NormalizeDocumentMainType(mappingRecord.CopType),
            Department = mappingRecord.Department,
            CopDate = mappingRecord.CopDate,
            ModifiedDate = mappingRecord.ModifiedDate,
            LegislationClass = mappingRecord.LegislationClass,
            CopType = mappingRecord.CopType,
            CopYear = mappingRecord.CopYear,
            CopVersion = mappingRecord.CopVersion,
            LegislationYear = mappingRecord.LegislationYear,
            LegislationNumber = mappingRecord.LegislationNumber,
            WorkAuthor = Common.LegWorkAuthorMapping.GetWorkAuthorUri(mappingRecord.LegislationClass),
            LegislationTitle = mappingRecord.LegislationTitle,
            Publisher = AssociatedDocumentMapping.GetPublisher(filename)
        };
    }

}

}
