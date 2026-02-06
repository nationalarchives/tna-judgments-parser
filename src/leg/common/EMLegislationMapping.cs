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
    private static readonly Dictionary<string, EMMappingRecord> _records = LoadRecords();

    /// <summary>
    /// Normalizes a filename for lookup: strip extension and trim.
    /// </summary>
    private static string NormalizeFilename(string filename) {
        if (string.IsNullOrWhiteSpace(filename)) return filename ?? "";
        string name = Path.GetFileNameWithoutExtension(filename.Trim());
        return name.Trim();
    }

    /// <summary>
    /// Loads full EM mapping records from the embedded CSV resource.
    /// Key is the filename column (without extension) for direct lookup.
    /// </summary>
    private static Dictionary<string, EMMappingRecord> LoadRecords() {
        var records = new Dictionary<string, EMMappingRecord>(StringComparer.OrdinalIgnoreCase);
        
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream("leg.em_to_legislation_mapping.csv");
        
        if (stream is null) {
            logger.LogWarning("Could not load EM to legislation mapping CSV resource");
            return records;
        }

        using StreamReader reader = new(stream);
        
        // Header: em_uri,filename,em_type,em_title,em_date,legislation_uri,legislation_class,legislation_year,legislation_number,legislation_title,department,modified_date,version,em_year
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

            var parts = ParseCsvLine(line);
            if (parts.Length < 14) {
                logger.LogTrace("Skipping malformed line {LineNumber} in EM mapping CSV (expected 14 columns, got {Count})", lineNumber, parts.Length);
                continue;
            }

            string filename = NormalizeFilename(parts[1]?.Trim() ?? "");
            if (string.IsNullOrEmpty(filename)) {
                logger.LogTrace("Skipping line {LineNumber} - empty filename", lineNumber);
                continue;
            }

            string emUri = parts[0]?.Trim() ?? "";

            int version = 1;
            if (!string.IsNullOrWhiteSpace(parts[12])) {
                int.TryParse(parts[12].Trim(), out version);
            }

            int? emYear = null;
            if (!string.IsNullOrWhiteSpace(parts[13])) {
                if (int.TryParse(parts[13].Trim(), out int parsedEmYear)) {
                    emYear = parsedEmYear;
                }
            }

            string emDate = parts[4]?.Trim() ?? "";

            int? legYear = null;
            string legNumber = null;
            if (!string.IsNullOrWhiteSpace(parts[7])) {
                if (double.TryParse(parts[7].Trim(), out double legYearDouble)) {
                    legYear = (int)legYearDouble;
                }
            }
            if (!string.IsNullOrWhiteSpace(parts[8])) {
                if (double.TryParse(parts[8].Trim(), out double legNumberDouble)) {
                    legNumber = ((long)legNumberDouble).ToString();
                } else {
                    legNumber = parts[8].Trim();
                }
            }

            var record = new EMMappingRecord {
                EmUri = emUri,
                EmType = parts[2]?.Trim() ?? "",
                EmTitle = Unquote(parts[3]?.Trim() ?? ""),
                EmDate = emDate,
                EmYear = emYear,
                EmVersion = version,
                LegislationUri = parts[5]?.Trim() ?? "",
                LegislationClass = parts[6]?.Trim() ?? "",
                LegislationYear = legYear,
                LegislationNumber = legNumber,
                LegislationTitle = Unquote(parts[9]?.Trim() ?? ""),
                Department = Unquote(parts[10]?.Trim() ?? ""),
                ModifiedDate = parts[11]?.Trim() ?? ""
            };

            records[filename] = record;
        }

        logger.LogInformation("Loaded {Count} EM mapping records", records.Count);
        return records;
    }

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
    /// Gets the full mapping record for the given filename.
    /// The filename can include an extension (e.g. .docx); it is normalized for lookup against the CSV filename column.
    /// </summary>
    /// <param name="filename">The EM filename (e.g. uksiem_20241017_en_002.docx)</param>
    /// <returns>The mapping record</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no CSV row has a matching filename</exception>
    public static EMMappingRecord GetMappingRecord(string filename) {
        string key = NormalizeFilename(filename);
        if (_records.TryGetValue(key, out EMMappingRecord record)) {
            return record;
        }
        throw new KeyNotFoundException($"No CSV mapping found for EM filename '{key}'. All EMs must have a CSV entry.");
    }

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
