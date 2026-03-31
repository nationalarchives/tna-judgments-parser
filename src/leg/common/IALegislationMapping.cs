
using System;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Provides lookup functionality for Impact Assessment to Legislation mappings.
/// Reads from the combined associated document mapping CSV.
/// </summary>
internal static partial class IALegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(IALegislationMapping));

    /// <summary>
    /// Gets the legislation URI for a given year and number.
    /// </summary>
    public static string GetLegislationUri(int year, int number) {
        var record = GetMappingRecord(year, number);
        return !string.IsNullOrEmpty(record?.LegislationUri) ? record.LegislationUri : null;
    }

    /// <summary>
    /// Gets the full mapping record for a given year and number.
    /// </summary>
    public static IAMappingRecord GetMappingRecord(int year, int number) {
        string filename = BuildFilename(year, number);
        var record = AssociatedDocumentMapping.GetRecord(filename);
        if (record is null)
            return null;

        return new IAMappingRecord {
            UkiaUri = record.DocumentUri,
            UkiaYear = year,
            UkiaNumber = number,
            Title = record.DocumentTitle,
            IADate = record.DocumentDate,
            DocumentStage = record.DocumentStage,
            DocumentMainType = record.DocumentType,
            Department = record.Department,
            ModifiedDate = record.ModifiedDate,
            PDFDate = record.PdfDate,
            LegislationUri = record.LegislationUri,
            LegislationClass = record.LegislationClass,
            LegislationYear = record.LegislationYear,
            LegislationNumber = record.LegislationNumber
        };
    }

    /// <summary>
    /// Builds a filename from year and number for CSV lookup.
    /// Format: ukia_YYYYNNNN_en (with zero-padded number)
    /// </summary>
    private static string BuildFilename(int year, int number) {
        return $"ukia_{year:D4}{number:D4}_en";
    }

    /// <summary>
    /// Builds a UKIA URI from year and number.
    /// </summary>
    public static string BuildUkiaUri(int year, int number) {
        return $"http://www.legislation.gov.uk/id/ukia/{year}/{number}";
    }

    /// <summary>
    /// Normalizes a document stage value to URL-safe lowercase format.
    /// </summary>
    public static string NormalizeStage(string stage) {
        if (string.IsNullOrWhiteSpace(stage))
            return null;

        string normalized = stage.Trim().ToLowerInvariant();

        return normalized switch {
            "final" => "final",
            "enactment" => "enactment",
            "consultation" => "consultation",
            "development" => "development",
            "implementation" => "implementation",
            "options" => "options",
            "post-implementation" or "postimplementation" or "post implementation" => "post-implementation",
            _ => null
        };
    }

    /// <summary>
    /// Builds the short URI component for an Impact Assessment.
    /// Format: {legislation-type}/{year}/{number}/impacts/{ukia-year}/{ukia-number}
    /// Falls back to ukia/{year}/{number} if no legislation mapping exists.
    /// </summary>
    public static string BuildShortUriComponent(int year, int number, string stage = null) {
        string legislationUri = GetLegislationUri(year, number);

        if (!string.IsNullOrEmpty(legislationUri)) {
            var components = ParseLegislationUri(legislationUri);
            if (components.HasValue) {
                var (type, legYear, legNumber) = components.Value;
                return $"{type}/{legYear}/{legNumber}/impacts/{year}/{number}";
            } else {
                logger.LogWarning("Failed to parse legislation URI for UKIA {Year}/{Number}, using fallback URI", year, number);
                return $"ukia/{year}/{number}";
            }
        } else {
            logger.LogWarning("No legislation mapping found for UKIA {Year}/{Number}, using fallback URI", year, number);
            return $"ukia/{year}/{number}";
        }
    }

    /// <summary>
    /// Parses a legislation URI to extract type, year, and number.
    /// </summary>
    public static (string type, int year, string number)? ParseLegislationUri(string legislationUri) {
        if (string.IsNullOrEmpty(legislationUri))
            return null;

        Match match = LegislationUriRegex().Match(legislationUri);
        if (!match.Success) {
            logger.LogWarning("Legislation URI '{Uri}' does not match expected pattern", legislationUri);
            return null;
        }

        string type = match.Groups[1].Value;
        string yearStr = match.Groups[2].Value;
        string number = match.Groups[3].Value;

        if (!int.TryParse(yearStr, out int year)) {
            logger.LogWarning("Legislation URI '{Uri}' has non-numeric year", legislationUri);
            return null;
        }

        return (type, year, number);
    }

    [GeneratedRegex(@"^https?://www\.legislation\.gov\.uk/id/([^/]+)/(\d{4})/(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LegislationUriRegex();

    /// <summary>
    /// Parses an IA filename to extract year and number.
    /// Expected format: ukia_YYYYNNNN_en
    /// </summary>
    public static (int year, int number)? ParseFilename(string filename) {
        if (string.IsNullOrEmpty(filename))
            return null;

        string name = Path.GetFileNameWithoutExtension(filename);

        Match match = FilenameRegex().Match(name);
        if (!match.Success) {
            logger.LogWarning("Filename '{Filename}' does not match expected pattern ukia_YYYYNNNN_en", filename);
            return null;
        }

        string combined = match.Groups[1].Value;

        if (combined.Length < 5) {
            logger.LogWarning("Filename '{Filename}' has invalid year/number format", filename);
            return null;
        }

        string yearStr = combined[..4];
        string numberStr = combined[4..];

        if (!int.TryParse(yearStr, out int year) || !int.TryParse(numberStr, out int number)) {
            logger.LogWarning("Filename '{Filename}' has non-numeric year/number", filename);
            return null;
        }

        return (year, number);
    }

    [GeneratedRegex(@"^ukia_(\d+)_en$", RegexOptions.IgnoreCase)]
    private static partial Regex FilenameRegex();

}

}
