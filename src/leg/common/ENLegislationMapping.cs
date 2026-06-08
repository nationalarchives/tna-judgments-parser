
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

internal static partial class ENLegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(ENLegislationMapping));

    // Matches EN filename patterns with or without _en suffix:
    // ukpgaen_20200007_en, ukpga_20180015_en, aspen_20250001_en, ukpgaen_20250015, ukpgaen_20180019_en2, etc.
    [GeneratedRegex(@"^(ukpgaen|ukpga|aspen|niaen|aniaen)[_](\d{4})[_]?(\d+)(?:[_]en\d*)?(?:[_].+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex FilenamePartsRegex();

    /// <summary>
    /// Normalizes an EN filename to the canonical CSV format: {type}en_{YYYY}{NNNN}_en.
    /// </summary>
    public static string NormalizeFilename(string filename) {
        if (string.IsNullOrWhiteSpace(filename)) return filename ?? "";
        string name = Path.GetFileNameWithoutExtension(filename.Trim());

        var match = FilenamePartsRegex().Match(name);
        if (match.Success) {
            string prefix = match.Groups[1].Value.ToLowerInvariant();
            string year = match.Groups[2].Value;
            string number = match.Groups[3].Value.PadLeft(4, '0');

            if (prefix == "ukpga") prefix = "ukpgaen";

            return $"{prefix}_{year}{number}_en";
        }

        return name.Trim();
    }

    public static ENMappingRecord GetMappingRecord(string filename) {
        string key = NormalizeFilename(filename);
        var record = AssociatedDocumentMapping.GetRecord(key);
        if (record is null)
            throw new KeyNotFoundException($"No CSV mapping found for EN filename '{key}'. All ENs must have a CSV entry.");

        return new ENMappingRecord {
            EnUri = record.DocumentUri,
            Filename = record.Filename,
            EnType = record.DocumentType,
            EnTitle = record.DocumentTitle,
            EnDate = record.DocumentDate,
            LegislationUri = record.LegislationUri,
            LegislationClass = record.LegislationClass,
            LegislationYear = record.LegislationYear,
            LegislationNumber = record.LegislationNumber,
            LegislationTitle = record.LegislationTitle
        };
    }

    public static string BuildShortUriComponent(ENMappingRecord record) {
        if (record is null)
            throw new ArgumentNullException(nameof(record));

        if (string.IsNullOrEmpty(record.LegislationUri))
            throw new ArgumentException($"EN has no legislation URI in CSV mapping");

        var components = ParseLegislationUri(record.LegislationUri);
        if (!components.HasValue)
            throw new ArgumentException($"Failed to parse legislation URI for EN: {record.LegislationUri}");

        var (type, legYear, legNumber) = components.Value;
        return $"{type}/{legYear}/{legNumber}/notes";
    }

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

    public static string NormalizeDocumentMainType(string enType) {
        if (string.IsNullOrEmpty(enType))
            return "ExplanatoryNotes";

        string typeLower = enType.ToLowerInvariant();

        if (typeLower.Contains("scottish"))
            return "ScottishActExplanatoryNotes";
        if (typeLower.Contains("northern ireland") || typeLower.Contains("ni "))
            return "NorthernIrelandActExplanatoryNotes";
        if (typeLower.Contains("welsh") || typeLower.Contains("wales"))
            return "WelshActExplanatoryNotes";

        return "UnitedKingdomExplanatoryNotes";
    }

}

}
