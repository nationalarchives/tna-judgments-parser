
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Provides lookup functionality for Codes of Practice to legislation mappings.
/// Reads from the combined associated document mapping CSV.
/// </summary>
internal static partial class CoPLegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(CoPLegislationMapping));

    /// <summary>
    /// Gets the full mapping record for the given filename.
    /// </summary>
    public static CoPMappingRecord GetMappingRecord(string filename) {
        var record = AssociatedDocumentMapping.GetRecord(filename);
        if (record is null)
            throw new KeyNotFoundException($"No CSV mapping found for CoP filename '{filename}'. All CoPs must have a CSV entry.");

        int version = ParseVersionFromFilename(filename);

        return new CoPMappingRecord {
            CopUri = record.DocumentUri,
            CopType = record.DocumentType,
            CopTitle = record.DocumentTitle,
            CopDate = record.DocumentDate,
            CopYear = record.DocumentYear,
            CopVersion = version,
            LegislationUri = record.LegislationUri,
            LegislationClass = record.LegislationClass,
            LegislationYear = record.LegislationYear,
            LegislationNumber = record.LegislationNumber,
            LegislationTitle = record.LegislationTitle,
            Department = record.Department,
            ModifiedDate = record.ModifiedDate
        };
    }

    /// <summary>
    /// Parses version number from filename suffix (e.g., _002 -> 2, _003 -> 3).
    /// Returns 1 if no suffix present.
    /// </summary>
    private static int ParseVersionFromFilename(string filename) {
        string name = System.IO.Path.GetFileNameWithoutExtension(filename?.Trim() ?? "");
        var match = Regex.Match(name, @"_en_(\d+)$", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int version))
            return version;
        return 1;
    }

    /// <summary>
    /// Builds the short URI component for a Code of Practice.
    /// Format: {legislation-type}/{leg-year}/{leg-number}/code-of-practice/{version}
    /// </summary>
    public static string BuildShortUriComponent(CoPMappingRecord record) {
        if (record is null)
            throw new ArgumentNullException(nameof(record));

        if (string.IsNullOrEmpty(record.LegislationUri))
            throw new ArgumentException($"CoP (version={record.CopVersion}) has no legislation URI in CSV mapping");

        var components = ParseLegislationUri(record.LegislationUri);
        if (!components.HasValue)
            throw new ArgumentException($"Failed to parse legislation URI for CoP: {record.LegislationUri}");

        var (type, legYear, legNumber) = components.Value;
        return $"{type}/{legYear}/{legNumber}/code-of-practice/{record.CopVersion}";
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
    /// Normalizes the CoP type string to a document main type value.
    /// </summary>
    public static string NormalizeDocumentMainType(string copType) {
        if (string.IsNullOrEmpty(copType))
            return "CodeOfPractice";

        string typeLower = copType.ToLowerInvariant();

        if (typeLower.Contains("uk draft si"))
            return "UnitedKingdomDraftCodeOfPractice";
        if (typeLower.Contains("uk si"))
            return "UnitedKingdomCodeOfPractice";
        if (typeLower.Contains("scottish") && typeLower.Contains("draft"))
            return "ScottishDraftCodeOfPractice";
        if (typeLower.Contains("scottish"))
            return "ScottishCodeOfPractice";
        if (typeLower.Contains("ni") && typeLower.Contains("draft"))
            return "NorthernIrelandDraftCodeOfPractice";
        if (typeLower.Contains("ni"))
            return "NorthernIrelandCodeOfPractice";

        return "CodeOfPractice";
    }

}

}
