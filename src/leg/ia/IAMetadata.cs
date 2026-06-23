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

    // Additional metadata from CSV mapping (derived from XML metadata files)
    public string DocumentStage { get; init; }
    public string DocumentMainType { get; init; }
    public string Department { get; init; }
    public string IADate { get; init; }

    // Year and number values (for explicit metadata in proprietary section)
    public int? UkiaYear { get; init; }
    public int? UkiaNumber { get; init; }
    
    // Full UKIA URI (e.g., http://www.legislation.gov.uk/id/ukia/2025/17)
    public string UkiaUri { get; init; }

    // The IA filename's series prefix (ukia/ssifia/sdsifia) and its /impacts identity.
    public string IaSeries { get; init; }
    public string ImpactsYear { get; init; }
    public string ImpactsNumber { get; init; }

    /// <summary>
    /// Image filenames use the IA's own identifier ({series}/{impacts-year}/{impacts-number},
    /// e.g. ukia/2025/17 or ssifia/2026/173) rather than the parent legislation path, so
    /// images from different IAs under the same legislation are always distinguishable and
    /// the image name matches the document's own filename scheme.
    /// </summary>
    public override string ImageFileIdentifier =>
        !string.IsNullOrEmpty(IaSeries) && !string.IsNullOrEmpty(ImpactsYear) && !string.IsNullOrEmpty(ImpactsNumber)
            ? $"{IaSeries}/{ImpactsYear}/{ImpactsNumber}"
            : base.ImageFileIdentifier;

    internal static IAMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config, string filename) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }

        DateTime? modified = DocxLastModified.Get(doc);
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");

        // Look up the mapping record by the actual filename (handles every IA
        // jurisdiction: ukia, ssifia, sdsifia) and build the URI from it.
        IAMappingRecord mappingRecord = IALegislationMapping.GetMappingRecord(filename);
        string shortUri = IALegislationMapping.BuildShortUriComponent(mappingRecord);
        string legislationUri = mappingRecord?.LegislationUri;

        return new IAMetadata {
            ShortUriComponent = shortUri,
            IaSeries = mappingRecord?.IaSeries,
            ImpactsYear = mappingRecord?.ImpactsYear,
            ImpactsNumber = mappingRecord?.ImpactsNumber,
            ExpressionDate = Builder.FormatDateOnly(modified),
            ExpressionDateName = modified is null ? null : "lastModified",
            Name = name,
            CSS = css,
            LegislationUri = legislationUri,
            // Populate additional metadata from CSV mapping
            DocumentStage = mappingRecord?.DocumentStage,
            DocumentMainType = mappingRecord?.DocumentMainType,
            Department = mappingRecord?.Department,
            IADate = mappingRecord?.IADate,
            LegislationClass = mappingRecord?.LegislationClass,
            // Year and number values
            UkiaYear = mappingRecord?.UkiaYear,
            UkiaNumber = mappingRecord?.UkiaNumber,
            LegislationYear = mappingRecord?.LegislationYear,
            LegislationNumber = mappingRecord?.LegislationNumber,
            // Full UKIA URI
            UkiaUri = mappingRecord?.UkiaUri,
            WorkAuthor = Common.LegWorkAuthorMapping.GetWorkAuthorUri(mappingRecord?.LegislationClass),
            LegislationTitle = AssociatedDocumentMapping.GetLegislationTitle(filename),
            Publisher = AssociatedDocumentMapping.GetPublisher(filename)
        };
    }

}

}

