
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;
using UK.Gov.Legislation.Common;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.ImpactAssessments {

class IAMetadata : DocumentMetadata {

    public DateTime? LastModified { get; init; }

    internal static IAMetadata Make(List<IBlock> header, WordprocessingDocument doc, LegislativeDocumentConfig config) {
        string name = BaseHeaderSplitter.GetDocumentType(header, config);
        // If header parsing fails to identify document type, use the default
        if (string.IsNullOrEmpty(name)) {
            name = config.DefaultDocumentType;
        }
        string number = BaseHeaderSplitter.GetDocumentNumber(header);
        string uri = number is null ? null : RegulationNumber.MakeURI(number) + config.UriSuffix;
        DateTime? modified = doc.PackageProperties.Modified;
        
        // Extract the docDate from the header
        string docDate = ExtractDocDate(header);
        
        Dictionary<string, Dictionary<string, string>> css = DOCX.CSS.Extract(doc.MainDocumentPart, "#doc");
        
        return new IAMetadata {
            ShortUriComponent = uri,
            WorkDate = docDate,
            WorkDateName = docDate is null ? null : "document",
            ExpressionDate = docDate,
            ExpressionDateName = docDate is null ? null : "document",
            LastModified = modified,
            Name = name,
            CSS = css
        };
    }

    private static string ExtractDocDate(List<IBlock> header) {
        // Look for text like "Date: 02/07/2012" in the header
        foreach (var block in header) {
            string text = block.GetTextContent();
            
            // Match "Date:" followed by date text
            var match = Regex.Match(text, @"\bDate:\s*(.+?)(?:\s*Stage:|$)", RegexOptions.IgnoreCase);
            if (match.Success) {
                string dateValue = match.Groups[1].Value.Trim();
                
                // Try to parse the date
                if (TryParseDateFromValue(dateValue, out string isoDate)) {
                    return isoDate;
                }
            }
        }
        
        return null;
    }

    private static bool TryParseDateFromValue(string dateValue, out string isoDate) {
        isoDate = null;
        
        if (string.IsNullOrWhiteSpace(dateValue)) {
            return false;
        }
        
        // Handle malformed dates with extra leading zeros like "030/9/2015"
        var match = Regex.Match(dateValue, @"^(\d{2,3})/(\d{1,2})/(\d{4})$");
        if (match.Success) {
            if (int.TryParse(match.Groups[1].Value, out int day) && 
                int.TryParse(match.Groups[2].Value, out int month) && 
                int.TryParse(match.Groups[3].Value, out int year)) {
                
                // Handle extra leading zero (e.g., "030" → 30)
                if (day > 31) {
                    day = day % 100;
                }
                
                try {
                    var date = new DateTime(year, month, day);
                    isoDate = date.ToString("yyyy-MM-dd");
                    return true;
                }
                catch {
                    // Fall through to other parsers
                }
            }
        }
        
        // Try UK date format culture first (DD/MM/YYYY) as this is the expected format for UK IA documents
        if (DateTime.TryParse(dateValue, new System.Globalization.CultureInfo("en-GB"), 
            System.Globalization.DateTimeStyles.None, out DateTime parsedDate)) {
            isoDate = parsedDate.ToString("yyyy-MM-dd");
            return true;
        }
        
        // Try to parse using DateTime.TryParse which handles many formats
        if (DateTime.TryParse(dateValue, System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.None, out parsedDate)) {
            isoDate = parsedDate.ToString("yyyy-MM-dd");
            return true;
        }
        
        // Try parsing month-year formats like "Aug 2022" or "July 2022"
        // Default to first day of the month
        if (Regex.IsMatch(dateValue, @"^[A-Za-z]+\s+\d{4}$")) {
            if (DateTime.TryParseExact(dateValue, new[] { "MMMM yyyy", "MMM yyyy" }, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out parsedDate)) {
                isoDate = parsedDate.ToString("yyyy-MM-dd");
                return true;
            }
        }
        
        return false;
    }
}

// Extension method to get text content from IBlock
static class IBlockExtensions {
    public static string GetTextContent(this IBlock block) {
        if (block is ILine line) {
            return string.Join("", line.Contents.OfType<IFormattedText>().Select(t => t.Text));
        }
        if (block is ITable table) {
            var texts = new List<string>();
            foreach (var row in table.Rows) {
                foreach (var cell in row.Cells) {
                    foreach (var cellBlock in cell.Contents) {
                        texts.Add(cellBlock.GetTextContent());
                    }
                }
            }
            return string.Join(" ", texts);
        }
        return "";
    }
}

}

