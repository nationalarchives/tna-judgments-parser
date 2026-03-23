using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

internal static partial class ENLegislationMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(ENLegislationMapping));
    private static readonly Dictionary<string, ENMappingRecord> _records = LoadRecords();

    // Matches EN filename patterns: ukpgaen_20200007_en, ukpga_20180015_en, aspen_20250001_en, etc.
    // Groups: prefix (ukpgaen, ukpga, aspen, niaen), year, number, optional suffix (_edit, _001, etc.)
    [GeneratedRegex(@"^(ukpgaen|ukpga|aspen|niaen|aniaen)[_](\d{4})[_]?(\d+)[_]en(?:[_].+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex FilenamePartsRegex();

    /// <summary>
    /// Normalizes an EN filename to the canonical CSV format: {type}en_{YYYY}{NNNN}_en.
    /// Handles variations like missing leading zeros, extra underscores, _edit suffixes,
    /// and ukpga vs ukpgaen prefix differences.
    /// </summary>
    public static string NormalizeFilename(string filename) {
        if (string.IsNullOrWhiteSpace(filename)) return filename ?? "";
        string name = Path.GetFileNameWithoutExtension(filename.Trim());

        // Try to normalize to the CSV canonical format: {type}en_{YYYY}{NNNN}_en
        var match = FilenamePartsRegex().Match(name);
        if (match.Success) {
            string prefix = match.Groups[1].Value.ToLowerInvariant();
            string year = match.Groups[2].Value;
            string number = match.Groups[3].Value.PadLeft(4, '0');

            // Normalize prefix: ukpga -> ukpgaen
            if (prefix == "ukpga") prefix = "ukpgaen";

            return $"{prefix}_{year}{number}_en";
        }

        return name.Trim();
    }

    private static Dictionary<string, ENMappingRecord> LoadRecords() {
        var records = new Dictionary<string, ENMappingRecord>(StringComparer.OrdinalIgnoreCase);

        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream("leg.en_to_legislation_mapping.csv");

        if (stream is null) {
            logger.LogWarning("Could not load EN to legislation mapping CSV resource");
            return records;
        }

        using StreamReader reader = new(stream);

        // Header: en_uri,filename,en_type,en_title,en_date,legislation_uri,legislation_class,legislation_year,legislation_number,legislation_title
        string header = reader.ReadLine();
        if (header is null) {
            logger.LogWarning("EN to legislation mapping CSV is empty");
            return records;
        }

        int lineNumber = 1;
        while (reader.ReadLine() is string line) {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = ParseCsvLine(line);
            if (parts.Length < 10) {
                logger.LogTrace("Skipping malformed line {LineNumber} in EN mapping CSV (expected 10 columns, got {Count})", lineNumber, parts.Length);
                continue;
            }

            string filename = NormalizeFilename(parts[1]?.Trim() ?? "");
            if (string.IsNullOrEmpty(filename)) {
                logger.LogTrace("Skipping line {LineNumber} - empty filename", lineNumber);
                continue;
            }

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

            var record = new ENMappingRecord {
                EnUri = parts[0]?.Trim() ?? "",
                Filename = filename,
                EnType = parts[2]?.Trim() ?? "",
                EnTitle = Unquote(parts[3]?.Trim() ?? ""),
                EnDate = parts[4]?.Trim() ?? "",
                LegislationUri = parts[5]?.Trim() ?? "",
                LegislationClass = parts[6]?.Trim() ?? "",
                LegislationYear = legYear,
                LegislationNumber = legNumber,
                LegislationTitle = Unquote(parts[9]?.Trim() ?? "")
            };

            records[filename] = record;
        }

        logger.LogInformation("Loaded {Count} EN mapping records", records.Count);
        return records;
    }

    private static string[] ParseCsvLine(string line) {
        var parts = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++) {
            char c = line[i];

            if (c == '"') {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') {
                    currentField += '"';
                    i++;
                } else {
                    inQuotes = !inQuotes;
                }
            } else if (c == ',' && !inQuotes) {
                parts.Add(currentField);
                currentField = "";
            } else {
                currentField += c;
            }
        }

        parts.Add(currentField);

        return parts.ToArray();
    }

    private static string Unquote(string value) {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
            return value[1..^1].Replace("\"\"", "\"");
        }

        return value;
    }

    public static ENMappingRecord GetMappingRecord(string filename) {
        string key = NormalizeFilename(filename);
        if (_records.TryGetValue(key, out ENMappingRecord record)) {
            return record;
        }
        throw new KeyNotFoundException($"No CSV mapping found for EN filename '{key}'. All ENs must have a CSV entry.");
    }

    public static string BuildShortUriComponent(ENMappingRecord record) {
        if (record is null)
            throw new ArgumentNullException(nameof(record));

        if (string.IsNullOrEmpty(record.LegislationUri)) {
            throw new ArgumentException($"EN has no legislation URI in CSV mapping");
        }

        var components = ParseLegislationUri(record.LegislationUri);
        if (!components.HasValue) {
            throw new ArgumentException($"Failed to parse legislation URI for EN: {record.LegislationUri}");
        }

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
