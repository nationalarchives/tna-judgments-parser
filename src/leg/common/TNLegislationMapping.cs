
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Provides lookup functionality for Transposition Notes to legislation mappings.
/// Reads from the combined associated document mapping CSV.
/// </summary>
internal static partial class TNLegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(TNLegislationMapping));

    /// <summary>
    /// Gets the full mapping record for the given filename.
    /// </summary>
    public static TNMappingRecord GetMappingRecord(string filename) {
        var record = AssociatedDocumentMapping.GetRecord(filename);
        if (record is null)
            throw new KeyNotFoundException($"No CSV mapping found for TN filename '{filename}'. All TNs must have a CSV entry.");

        return new TNMappingRecord {
            TnUri = record.DocumentUri,
            TnType = record.DocumentType,
            TnTitle = record.DocumentTitle,
            TnDate = record.DocumentDate,
            TnYear = record.DocumentYear,
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
    /// Builds the short URI component for a Transposition Note.
    /// Format: {legislation-type}/{leg-year}/{leg-number}/transposition-note/1
    /// TN is unversioned but the spec-mandated URI still includes a counter segment.
    /// </summary>
    public static string BuildShortUriComponent(TNMappingRecord record) {
        if (record is null)
            throw new ArgumentNullException(nameof(record));

        if (string.IsNullOrEmpty(record.LegislationUri))
            throw new ArgumentException("TN has no legislation URI in CSV mapping");

        var components = ParseLegislationUri(record.LegislationUri);
        if (!components.HasValue)
            throw new ArgumentException($"Failed to parse legislation URI for TN: {record.LegislationUri}");

        var (type, legYear, legNumber) = components.Value;
        return $"{type}/{legYear}/{legNumber}/transposition-note/1";
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
    /// Normalizes the TN type string to a document main type value.
    /// </summary>
    public static string NormalizeDocumentMainType(string tnType) {
        if (string.IsNullOrEmpty(tnType))
            return "TranspositionNote";

        string typeLower = tnType.ToLowerInvariant();

        if (typeLower.Contains("uk draft si"))
            return "UnitedKingdomDraftTranspositionNote";
        if (typeLower.Contains("uk si"))
            return "UnitedKingdomTranspositionNote";
        if (typeLower.Contains("scottish") && typeLower.Contains("draft"))
            return "ScottishDraftTranspositionNote";
        if (typeLower.Contains("scottish"))
            return "ScottishTranspositionNote";
        if (typeLower.Contains("ni") && typeLower.Contains("draft"))
            return "NorthernIrelandDraftTranspositionNote";
        if (typeLower.Contains("ni"))
            return "NorthernIrelandTranspositionNote";

        return "TranspositionNote";
    }

}

}
