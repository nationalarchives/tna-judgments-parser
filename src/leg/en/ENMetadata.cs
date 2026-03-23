using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Common;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.ExplanatoryNotes {

class ENMetadata : DocumentMetadata {

    public DateTime? LastModified { get; init; }

    public string DocumentMainType { get; init; }
    public string EnType { get; init; }
    public string EnDate { get; init; }
    public string LegislationClass { get; init; }
    public int? LegislationYear { get; init; }
    public string LegislationNumber { get; init; }

    internal static ENMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config, string filename) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }

        DateTime? modified = doc.PackageProperties.Modified;
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");

        ENMappingRecord mappingRecord;
        try {
            mappingRecord = ENLegislationMapping.GetMappingRecord(filename);
        } catch (KeyNotFoundException ex) {
            throw new InvalidOperationException($"Cannot process EN file '{filename}': {ex.Message}", ex);
        }

        string shortUri;
        try {
            shortUri = ENLegislationMapping.BuildShortUriComponent(mappingRecord);
        } catch (ArgumentException ex) {
            throw new InvalidOperationException($"Cannot build URI for EN file '{filename}': {ex.Message}", ex);
        }

        return new ENMetadata {
            ShortUriComponent = shortUri,
            ExpressionDate = Builder.FormatDateAndTime(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            LastModified = modified,
            Name = name,
            CSS = css,
            LegislationUri = mappingRecord.LegislationUri,
            DocumentMainType = ENLegislationMapping.NormalizeDocumentMainType(mappingRecord.EnType),
            EnType = mappingRecord.EnType,
            EnDate = mappingRecord.EnDate,
            LegislationClass = mappingRecord.LegislationClass,
            LegislationYear = mappingRecord.LegislationYear,
            LegislationNumber = mappingRecord.LegislationNumber,
            WorkAuthor = Common.LegWorkAuthorMapping.GetWorkAuthorUri(mappingRecord.LegislationClass)
        };
    }

}

}
