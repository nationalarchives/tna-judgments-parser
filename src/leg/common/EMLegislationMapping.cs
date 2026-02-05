using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Provides lookup functionality for Explanatory Memoranda to Legislation mappings.
/// Loads mapping data from an embedded CSV resource.
/// </summary>
internal static partial class EMLegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(EMLegislationMapping));
    private static readonly Dictionary<(int year, int number), EMMappingRecord> _records = LoadRecords();

    /// <summary>
    /// Loads full EM mapping records from the embedded CSV resource.
    /// </summary>
    private static Dictionary<(int year, int number), EMMappingRecord> LoadRecords() {
        var records = new Dictionary<(int year, int number), EMMappingRecord>();
        
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream("leg.em_to_legislation_mapping.csv");
        
        if (stream is null) {
            logger.LogWarning("Could not load EM to legislation mapping CSV resource");
            return records;
        }

        using StreamReader reader = new(stream);
        
        // Skip header line: em_uri,em_type,em_title,em_date,legislation_uri,legislation_class,legislation_year,legislation_number,legislation_title,department,modified_date,version,em_year
        string header = reader.ReadLine();
        if (header is null) {
            logger.LogWarning("EM to legislation mapping CSV is empty");
            return records;
        }

        int lineNumber = 1;
        while (reader.ReadLine() is string line) {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Parse CSV line - handle quoted fields
            var parts = ParseCsvLine(line);
            if (parts.Length < 13) {
                logger.LogTrace("Skipping malformed line {LineNumber} in EM mapping CSV (expected 13 columns, got {Count})", lineNumber, parts.Length);
                continue;
            }

            // Extract year and number from em_uri (PDF filename)
            // Example: http://www.legislation.gov.uk/uksi/2013/2911/pdfs/uksiem_20132911_en.pdf
            // Or with version suffix: http://www.legislation.gov.uk/uksi/2023/532/pdfs/uksiem_20230532_en_001.pdf
            string emUri = parts[0]?.Trim() ?? "";
            var parsed = ExtractYearNumberFromUri(emUri);
            if (!parsed.HasValue) {
                logger.LogTrace("Skipping line {LineNumber} - could not extract year/number from em_uri: {Uri}", lineNumber, emUri);
                continue;
            }

            var (year, number) = parsed.Value;

            // Parse version from column 11
            int version = 1;
            if (!string.IsNullOrWhiteSpace(parts[11])) {
                if (int.TryParse(parts[11].Trim(), out int parsedVersion)) {
                    version = parsedVersion;
                }
            }

            // Parse em_year from column 12
            int? emYear = null;
            if (!string.IsNullOrWhiteSpace(parts[12])) {
                if (int.TryParse(parts[12].Trim(), out int parsedEmYear)) {
                    emYear = parsedEmYear;
                }
            }

            string emDate = parts[3]?.Trim() ?? "";

            // Parse legislation year and number (may be empty or decimal)
            int? legYear = null;
            string legNumber = null;
            if (!string.IsNullOrWhiteSpace(parts[6])) {
                if (double.TryParse(parts[6].Trim(), out double legYearDouble)) {
                    legYear = (int)legYearDouble;
                }
            }
            if (!string.IsNullOrWhiteSpace(parts[7])) {
                // Parse as double and convert to clean string (removes .0 from CSV export)
                if (double.TryParse(parts[7].Trim(), out double legNumberDouble)) {
                    legNumber = ((long)legNumberDouble).ToString();
                } else {
                    legNumber = parts[7].Trim();
                }
            }

            var record = new EMMappingRecord {
                EmUri = emUri,
                EmType = parts[1]?.Trim() ?? "",
                EmTitle = Unquote(parts[2]?.Trim() ?? ""),
                EmDate = emDate,
                EmYear = emYear,
                EmVersion = version,
                LegislationUri = parts[4]?.Trim() ?? "",
                LegislationClass = parts[5]?.Trim() ?? "",
                LegislationYear = legYear,
                LegislationNumber = legNumber,
                LegislationTitle = Unquote(parts[8]?.Trim() ?? ""),
                Department = Unquote(parts[9]?.Trim() ?? ""),
                ModifiedDate = parts[10]?.Trim() ?? ""
            };

            records[(year, number)] = record;
        }

        logger.LogInformation("Loaded {Count} EM mapping records", records.Count);
        return records;
    }

    /// <summary>
    /// Extracts year and number from an EM URI.
    /// Example: http://www.legislation.gov.uk/uksi/2013/2911/pdfs/uksiem_20132911_en.pdf
    /// Or with version suffix: http://www.legislation.gov.uk/uksi/2023/532/pdfs/uksiem_20230532_en_001.pdf
    /// Extracts: year=2013, number=2911
    /// </summary>
    private static (int year, int number)? ExtractYearNumberFromUri(string emUri) {
        if (string.IsNullOrEmpty(emUri))
            return null;

        // Match pattern: [prefix]em_YYYYNNNN_en or [prefix]em_YYYYNNNN_en_NNN
        // Handles variations like uksiem, ssipn, nisrem, etc.
        Match match = EmUriRegex().Match(emUri);
        if (!match.Success) {
            return null;
        }

        string yearStr = match.Groups[1].Value;
        string numberStr = match.Groups[2].Value;

        if (!int.TryParse(yearStr, out int year) || !int.TryParse(numberStr, out int number)) {
            return null;
        }

        return (year, number);
    }

    [GeneratedRegex(@"em_(\d{4})(\d+)_en(?:_\d+)?\.pdf", RegexOptions.IgnoreCase)]
    private static partial Regex EmUriRegex();

    /// <summary>
    /// Parses a CSV line, handling quoted fields that may contain commas.
    /// </summary>
    private static string[] ParseCsvLine(string line) {
        var parts = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++) {
            char c = line[i];
            
            if (c == '"') {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') {
                    // Escaped quote
                    currentField += '"';
                    i++; // Skip next quote
                } else {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                }
            } else if (c == ',' && !inQuotes) {
                // Field separator
                parts.Add(currentField);
                currentField = "";
            } else {
                currentField += c;
            }
        }
        
        // Add last field
        parts.Add(currentField);
        
        return parts.ToArray();
    }

    /// <summary>
    /// Removes surrounding quotes from a string if present.
    /// </summary>
    private static string Unquote(string value) {
        if (string.IsNullOrEmpty(value))
            return value;
        
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
            return value[1..^1].Replace("\"\"", "\"");
        }
        
        return value;
    }

    /// <summary>
    /// Gets the full mapping record for a given year and number.
    /// Throws an exception if not found (EMs must have CSV entries).
    /// </summary>
    /// <param name="year">The EM year (from filename)</param>
    /// <param name="number">The EM number (from filename)</param>
    /// <returns>The mapping record</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no mapping exists for the given year/number</exception>
    public static EMMappingRecord GetMappingRecord(int year, int number) {
        if (_records.TryGetValue((year, number), out EMMappingRecord record)) {
            return record;
        }
        
        throw new KeyNotFoundException($"No CSV mapping found for EM year={year}, number={number}. All EMs must have a CSV entry.");
    }

    /// <summary>
    /// Parses an EM filename to extract year and number.
    /// Expected format: uksiem_YYYYNNNN_en or uksiem_YYYYNNNN_en_NNN (e.g., uksiem_20132911_en, uksiem_20230532_en_001)
    /// The number part can be variable length (e.g., 2911, or 0198).
    /// </summary>
    /// <param name="filename">The filename (with or without extension)</param>
    /// <returns>A tuple of (year, number) if parsing succeeds, otherwise null</returns>
    public static (int year, int number)? ParseFilename(string filename) {
        if (string.IsNullOrEmpty(filename))
            return null;

        // Remove extension if present
        string name = Path.GetFileNameWithoutExtension(filename);

        // Match pattern: [prefix]em_YYYYNNNN_en or [prefix]em_YYYYNNNN_en_NNN
        // Prefix can be: uksi, ssi, nisr, ukdsi, sdsi, nidsr, etc.
        Match match = FilenameRegex().Match(name);
        if (!match.Success) {
            logger.LogWarning("Filename '{Filename}' does not match expected pattern [prefix]em_YYYYNNNN_en[_NNN]", filename);
            return null;
        }

        string yearStr = match.Groups[1].Value;
        string numberStr = match.Groups[2].Value;

        if (!int.TryParse(yearStr, out int year) || !int.TryParse(numberStr, out int number)) {
            logger.LogWarning("Filename '{Filename}' has non-numeric year/number", filename);
            return null;
        }

        return (year, number);
    }

    [GeneratedRegex(@"em_(\d{4})(\d+)_en(?:_\d+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex FilenameRegex();

    /// <summary>
    /// Determines the URI path segment based on the EM type.
    /// Maps document types to their corresponding URL segments.
    /// </summary>
    /// <param name="emType">The em_type value from CSV</param>
    /// <returns>The URI path segment (e.g., 'memoranda', 'executive-note', 'policy-note')</returns>
    /// <exception cref="ArgumentException">Thrown when the EM type is unrecognized</exception>
    public static string GetUriPathSegment(string emType) {
        if (string.IsNullOrEmpty(emType))
            throw new ArgumentException("EM type is required");
        
        string typeLower = emType.ToLowerInvariant();
        
        // Any type containing 'memorandum' uses 'memoranda' (plural)
        // Includes: UK SI Explanatory Memorandum, UK Draft SI Explanatory Memorandum, 
        //          NI Statutory Rule Explanatory Memorandum, etc.
        if (typeLower.Contains("memorandum"))
            return "memoranda";
        
        // Executive notes (Scottish)
        if (typeLower.Contains("executive note"))
            return "executive-note";
        
        // Policy notes (Scottish)
        if (typeLower.Contains("policy note"))
            return "policy-note";
        
        throw new ArgumentException($"Unknown EM type: {emType}");
    }

    /// <summary>
    /// Builds the short URI component for an Explanatory Memorandum based on its linked legislation.
    /// Format: {legislation-type}/{leg-year}/{leg-number}/{path-segment}/{version}
    /// Example: uksi/2013/2911/memoranda/1
    /// </summary>
    /// <param name="record">The EM mapping record</param>
    /// <returns>The short URI component</returns>
    /// <exception cref="ArgumentException">Thrown when the record lacks required data</exception>
    public static string BuildShortUriComponent(EMMappingRecord record) {
        if (record is null)
            throw new ArgumentNullException(nameof(record));

        if (string.IsNullOrEmpty(record.LegislationUri)) {
            throw new ArgumentException($"EM (version={record.EmVersion}) has no legislation URI in CSV mapping");
        }

        var components = ParseLegislationUri(record.LegislationUri);
        if (!components.HasValue) {
            throw new ArgumentException($"Failed to parse legislation URI for EM: {record.LegislationUri}");
        }

        var (type, legYear, legNumber) = components.Value;
        string pathSegment = GetUriPathSegment(record.EmType);
        
        return $"{type}/{legYear}/{legNumber}/{pathSegment}/{record.EmVersion}";
    }

    /// <summary>
    /// Parses a legislation URI to extract type, year, and number.
    /// </summary>
    /// <param name="legislationUri">The full legislation URI (e.g., http://www.legislation.gov.uk/id/uksi/2013/2911)</param>
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
    /// Normalizes the EM type to a document main type value for proprietary metadata.
    /// Maps CSV em_type values to AKN-compliant type identifiers.
    /// </summary>
    /// <param name="emType">The em_type value from CSV</param>
    /// <returns>Normalized document main type (e.g., 'UnitedKingdomExplanatoryMemorandum')</returns>
    public static string NormalizeDocumentMainType(string emType) {
        if (string.IsNullOrEmpty(emType))
            return "ExplanatoryMemorandum";
        
        string typeLower = emType.ToLowerInvariant();
        
        // UK types
        if (typeLower.Contains("uk draft si"))
            return "UnitedKingdomDraftExplanatoryMemorandum";
        if (typeLower.Contains("uk si") && typeLower.Contains("memorandum"))
            return "UnitedKingdomExplanatoryMemorandum";
        
        // Scottish types
        if (typeLower.Contains("scottish") && typeLower.Contains("draft") && typeLower.Contains("policy note"))
            return "ScottishDraftPolicyNote";
        if (typeLower.Contains("scottish") && typeLower.Contains("policy note"))
            return "ScottishPolicyNote";
        if (typeLower.Contains("scottish") && typeLower.Contains("executive note"))
            return "ScottishExecutiveNote";
        
        // Northern Ireland types
        if (typeLower.Contains("ni") && typeLower.Contains("draft") && typeLower.Contains("memorandum"))
            return "NorthernIrelandDraftExplanatoryMemorandum";
        if (typeLower.Contains("ni") && typeLower.Contains("memorandum"))
            return "NorthernIrelandExplanatoryMemorandum";
        
        // Fallback
        return "ExplanatoryMemorandum";
    }

}

}
