using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Provides lookup functionality for Impact Assessment to Legislation mappings.
/// Loads mapping data from an embedded CSV resource.
/// </summary>
internal static partial class IALegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(IALegislationMapping));
    private static readonly Dictionary<string, string> _mapping = LoadMapping();

    /// <summary>
    /// Loads the IA to legislation mapping from the embedded CSV resource.
    /// </summary>
    private static Dictionary<string, string> LoadMapping() {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream("leg.ia_to_legislation_mapping.csv");
        
        if (stream is null) {
            logger.LogWarning("Could not load IA to legislation mapping CSV resource");
            return mapping;
        }

        using StreamReader reader = new(stream);
        
        // Skip header line
        string header = reader.ReadLine();
        if (header is null) {
            logger.LogWarning("IA to legislation mapping CSV is empty");
            return mapping;
        }

        int lineNumber = 1;
        while (reader.ReadLine() is string line) {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] parts = line.Split(',');
            if (parts.Length < 4) {
                logger.LogTrace("Skipping malformed line {LineNumber} in IA mapping CSV", lineNumber);
                continue;
            }

            string ukiaUri = parts[0].Trim();
            string legislationUri = parts[3].Trim();

            if (!string.IsNullOrEmpty(ukiaUri) && !string.IsNullOrEmpty(legislationUri)) {
                mapping[ukiaUri] = legislationUri;
            }
        }

        logger.LogInformation("Loaded {Count} IA to legislation mappings", mapping.Count);
        return mapping;
    }

    /// <summary>
    /// Gets the legislation URI for a given UKIA URI.
    /// </summary>
    /// <param name="ukiaUri">The full UKIA URI (e.g., http://www.legislation.gov.uk/id/ukia/2025/1)</param>
    /// <returns>The legislation URI if found, otherwise null</returns>
    public static string GetLegislationUri(string ukiaUri) {
        if (string.IsNullOrEmpty(ukiaUri))
            return null;
        
        return _mapping.TryGetValue(ukiaUri, out string legislationUri) ? legislationUri : null;
    }

    /// <summary>
    /// Gets the legislation URI for a given year and number.
    /// </summary>
    /// <param name="year">The UKIA year</param>
    /// <param name="number">The UKIA number</param>
    /// <returns>The legislation URI if found, otherwise null</returns>
    public static string GetLegislationUri(int year, int number) {
        string ukiaUri = BuildUkiaUri(year, number);
        return GetLegislationUri(ukiaUri);
    }

    /// <summary>
    /// Builds a UKIA URI from year and number.
    /// </summary>
    public static string BuildUkiaUri(int year, int number) {
        return $"http://www.legislation.gov.uk/id/ukia/{year}/{number}";
    }

    /// <summary>
    /// Builds the short URI component for an Impact Assessment based on its linked legislation.
    /// Format: {legislation-type}/{year}/{number}/impact-assessment
    /// Falls back to ukia/{year}/{number}/impact-assessment if no legislation mapping exists.
    /// </summary>
    /// <param name="year">The UKIA year</param>
    /// <param name="number">The UKIA number</param>
    /// <returns>The short URI component (e.g., 'uksi/2018/1149/impact-assessment')</returns>
    public static string BuildShortUriComponent(int year, int number) {
        string legislationUri = GetLegislationUri(year, number);
        
        if (!string.IsNullOrEmpty(legislationUri)) {
            var components = ParseLegislationUri(legislationUri);
            if (components.HasValue) {
                var (type, legYear, legNumber) = components.Value;
                return $"{type}/{legYear}/{legNumber}/impact-assessment";
            }
        }
        
        // Fallback: use ukia-based URI if no legislation mapping exists
        logger.LogWarning("No legislation mapping found for UKIA {Year}/{Number}, using fallback URI", year, number);
        return $"ukia/{year}/{number}/impact-assessment";
    }

    /// <summary>
    /// Parses a legislation URI to extract type, year, and number.
    /// </summary>
    /// <param name="legislationUri">The full legislation URI (e.g., http://www.legislation.gov.uk/id/uksi/2018/1149)</param>
    /// <returns>A tuple of (type, year, number) if parsing succeeds, otherwise null</returns>
    public static (string type, int year, string number)? ParseLegislationUri(string legislationUri) {
        if (string.IsNullOrEmpty(legislationUri))
            return null;

        // Match pattern: http://www.legislation.gov.uk/id/{type}/{year}/{number}
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
    /// Expected format: ukia_YYYYNNNN_en (e.g., ukia_20250001_en)
    /// </summary>
    /// <param name="filename">The filename (with or without extension)</param>
    /// <returns>A tuple of (year, number) if parsing succeeds, otherwise null</returns>
    public static (int year, int number)? ParseFilename(string filename) {
        if (string.IsNullOrEmpty(filename))
            return null;

        // Remove extension if present
        string name = Path.GetFileNameWithoutExtension(filename);

        // Match pattern: ukia_YYYYNNNN_en
        Match match = FilenameRegex().Match(name);
        if (!match.Success) {
            logger.LogWarning("Filename '{Filename}' does not match expected pattern ukia_YYYYNNNN_en", filename);
            return null;
        }

        string combined = match.Groups[1].Value;
        
        // First 4 digits are year, rest is number
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

