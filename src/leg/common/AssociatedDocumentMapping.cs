
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Provides lookup for fields from the combined associated document mapping CSV.
/// Currently used to supplement existing mapping classes with legislation_title and publisher.
/// </summary>
internal static class AssociatedDocumentMapping {

    private static readonly ILogger logger = Logging.Factory.CreateLogger(typeof(AssociatedDocumentMapping));
    private static readonly Dictionary<string, AssociatedDocumentRecord> _records = LoadRecords();

    private static string NormalizeFilename(string filename) {
        if (string.IsNullOrWhiteSpace(filename)) return filename ?? "";
        string name = Path.GetFileNameWithoutExtension(filename.Trim());
        return name.Trim();
    }

    private static Dictionary<string, AssociatedDocumentRecord> LoadRecords() {
        var records = new Dictionary<string, AssociatedDocumentRecord>(StringComparer.OrdinalIgnoreCase);

        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream("leg.associated_document_mapping.csv");

        if (stream is null) {
            logger.LogWarning("Could not load associated document mapping CSV resource");
            return records;
        }

        using StreamReader reader = new(stream);

        // Header: document_type,document_uri,filename,document_title,document_date,
        //         document_year,document_number,department,publisher,modified_date,
        //         legislation_uri,legislation_class,legislation_year,legislation_number,
        //         legislation_title,document_stage,pdf_date
        string header = reader.ReadLine();
        if (header is null) {
            logger.LogWarning("Associated document mapping CSV is empty");
            return records;
        }

        int lineNumber = 1;
        while (reader.ReadLine() is string line) {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = ParseCsvLine(line);
            if (parts.Length < 15) {
                logger.LogTrace("Skipping malformed line {LineNumber} (expected 17 columns, got {Count})", lineNumber, parts.Length);
                continue;
            }

            string filename = NormalizeFilename(parts[2]?.Trim() ?? "");
            if (string.IsNullOrEmpty(filename))
                continue;

            if (records.ContainsKey(filename))
                continue;

            records[filename] = new AssociatedDocumentRecord {
                LegislationTitle = Unquote(parts[14]?.Trim() ?? ""),
                Publisher = Unquote(parts[8]?.Trim() ?? "")
            };
        }

        logger.LogInformation("Loaded {Count} associated document mapping records", records.Count);
        return records;
    }

    /// <summary>
    /// Gets the legislation title for the given filename.
    /// Returns null if no mapping found or title is empty.
    /// </summary>
    public static string GetLegislationTitle(string filename) {
        string key = NormalizeFilename(filename);
        if (_records.TryGetValue(key, out var record) && !string.IsNullOrEmpty(record.LegislationTitle))
            return record.LegislationTitle;
        return null;
    }

    /// <summary>
    /// Gets the publisher for the given filename.
    /// Returns null if no mapping found or publisher is empty.
    /// </summary>
    public static string GetPublisher(string filename) {
        string key = NormalizeFilename(filename);
        if (_records.TryGetValue(key, out var record) && !string.IsNullOrEmpty(record.Publisher))
            return record.Publisher;
        return null;
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
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1].Replace("\"\"", "\"");
        return value;
    }

}

internal class AssociatedDocumentRecord {
    public string LegislationTitle { get; init; }
    public string Publisher { get; init; }
}

}
