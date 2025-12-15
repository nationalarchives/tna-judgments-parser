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

    internal static IAMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config, string filename) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }

        DateTime? modified = doc.PackageProperties.Modified;
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");

        // Parse filename to get year/number for URI construction
        string shortUri = null;
        string legislationUri = null;

        var parsed = IALegislationMapping.ParseFilename(filename);
        if (parsed.HasValue) {
            var (year, number) = parsed.Value;
            shortUri = IALegislationMapping.BuildShortUriComponent(year, number);
            legislationUri = IALegislationMapping.GetLegislationUri(year, number);
        }

        return new IAMetadata {
            ShortUriComponent = shortUri,
            ExpressionDate = Builder.FormatDateAndTime(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            LastModified = modified,
            Name = name,
            CSS = css,
            LegislationUri = legislationUri
        };
    }

}

}

