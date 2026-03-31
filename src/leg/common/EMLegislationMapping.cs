
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Provides lookup functionality for Explanatory Memoranda to Legislation mappings.
/// Reads from the combined associated document mapping CSV.
/// </summary>
internal static partial class EMLegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(EMLegislationMapping));

    /// <summary>
    /// Gets the full mapping record for the given filename.
    /// </summary>
    public static EMMappingRecord GetMappingRecord(string filename) {
        var record = AssociatedDocumentMapping.GetRecord(filename);
        if (record is null)
            throw new KeyNotFoundException($"No CSV mapping found for EM filename '{filename}'. All EMs must have a CSV entry.");

        // Version is encoded in filename suffix: uksiem_20230588_en_003 -> version 3
        int version = ParseVersionFromFilename(filename);

        return new EMMappingRecord {
            EmUri = record.DocumentUri,
            EmType = record.DocumentType,
            EmTitle = record.DocumentTitle,
            EmDate = record.DocumentDate,
            EmYear = record.DocumentYear,
            EmVersion = version,
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
        // Match trailing _NNN after _en
        var match = Regex.Match(name, @"_en_(\d+)$", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int version))
            return version;
        return 1;
    }

    /// <summary>
    /// Determines the URI path segment based on the EM type.
    /// </summary>
    public static string GetUriPathSegment(string emType) {
        if (string.IsNullOrEmpty(emType))
            throw new ArgumentException("EM type is required");

        string typeLower = emType.ToLowerInvariant();

        if (typeLower.Contains("memorandum"))
            return "memorandum";
        if (typeLower.Contains("executive note"))
            return "executive-note";
        if (typeLower.Contains("policy note"))
            return "policy-note";

        throw new ArgumentException($"Unknown EM type: {emType}");
    }

    /// <summary>
    /// Builds the short URI component for an Explanatory Memorandum.
    /// Format: {legislation-type}/{leg-year}/{leg-number}/{path-segment}/{version}
    /// </summary>
    public static string BuildShortUriComponent(EMMappingRecord record) {
        if (record is null)
            throw new ArgumentNullException(nameof(record));

        if (string.IsNullOrEmpty(record.LegislationUri))
            throw new ArgumentException($"EM (version={record.EmVersion}) has no legislation URI in CSV mapping");

        var components = ParseLegislationUri(record.LegislationUri);
        if (!components.HasValue)
            throw new ArgumentException($"Failed to parse legislation URI for EM: {record.LegislationUri}");

        var (type, legYear, legNumber) = components.Value;
        string pathSegment = GetUriPathSegment(record.EmType);

        return $"{type}/{legYear}/{legNumber}/{pathSegment}/{record.EmVersion}";
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
    /// Normalizes the EM type to a document main type value.
    /// </summary>
    public static string NormalizeDocumentMainType(string emType) {
        if (string.IsNullOrEmpty(emType))
            return "ExplanatoryMemorandum";

        string typeLower = emType.ToLowerInvariant();

        if (typeLower.Contains("uk draft si"))
            return "UnitedKingdomDraftExplanatoryMemorandum";
        if (typeLower.Contains("uk si") && typeLower.Contains("memorandum"))
            return "UnitedKingdomExplanatoryMemorandum";
        if (typeLower.Contains("scottish") && typeLower.Contains("draft") && typeLower.Contains("policy note"))
            return "ScottishDraftPolicyNote";
        if (typeLower.Contains("scottish") && typeLower.Contains("policy note"))
            return "ScottishPolicyNote";
        if (typeLower.Contains("scottish") && typeLower.Contains("executive note"))
            return "ScottishExecutiveNote";
        if (typeLower.Contains("ni") && typeLower.Contains("draft") && typeLower.Contains("memorandum"))
            return "NorthernIrelandDraftExplanatoryMemorandum";
        if (typeLower.Contains("ni") && typeLower.Contains("memorandum"))
            return "NorthernIrelandExplanatoryMemorandum";

        return "ExplanatoryMemorandum";
    }

}

}
