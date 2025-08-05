
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using CsvHelper;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace Backlog.Src.Batch.One
{

    class Metadata
    {

        internal class Line
        {
            public string id { get; set; }
            public string FilePath { get; set; }
            public string Extension { get; set; }
            public string decision_datetime { get; set; }
            public string file_no_1 { get; set; }
            public string file_no_2 { get; set; }
            public string file_no_3 { get; set; }
            public string claimants { get; set; }
            public string respondent { get; set; }
            public string main_subcategory_description { get; set; }
            public string sec_subcategory_description { get; set; }
            public string headnote_summary { get; set; }
            
            private readonly string DateFormat = "yyyy-MM-dd HH:mm:ss";
            internal string DecisionDate { get => System.DateTime.ParseExact(decision_datetime, DateFormat, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"); }
            internal string CaseNo { get => string.Join('/', file_no_1, file_no_2, file_no_3); }
        }



        internal static List<Line> Read(string path)
        {
            ValidateCsvHeaders(path);
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<Line>().ToList();
        }

        private static void ValidateCsvHeaders(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            
            // Read the header to get column names
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord;

            // Get required columns
            var requiredColumns = new[] { "id", "FilePath", "Extension", "decision_datetime", "file_no_1", "file_no_2", "file_no_3", "claimants", "respondent"};
            var missingColumns = requiredColumns.Where(col => !headers.Contains(col, StringComparer.OrdinalIgnoreCase)).ToList();

            if (missingColumns.Any())
            {
                throw new InvalidOperationException(
                    $"CSV validation failed. Missing required columns: {string.Join(", ", missingColumns)}.\n\n" +
                    $"Found headers:\n{string.Join(", ", headers)}\n\n" +
                    "Please preprocess your CSV to match the expected column names exactly."
                );
            }
        }

        internal static List<Line> FindLines(List<Line> lines, uint id)
        {
            return lines.Where(line => line.id == id.ToString()).ToList();
        }

        internal static ExtendedMetadata MakeMetadata(Line line) {
            List<ExtendedMetadata.Category> categories = [];
            categories.Add(new ExtendedMetadata.Category { Name = line.main_subcategory_description });
            if (!string.IsNullOrWhiteSpace(line.sec_subcategory_description)) {
                categories.Add(new ExtendedMetadata.Category { Name = line.sec_subcategory_description, Parent = line.main_subcategory_description });
            }
            string sourceFormat;
            if (line.Extension == ".doc" || line.Extension == ".docx")
                sourceFormat = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            else if (line.Extension == ".pdf")
                sourceFormat = "application/pdf";
            else
                throw new Exception($"Unexpected extension {line.Extension}");
            bool old = String.Compare(line.DecisionDate, "2010-01-18") < 0;
            Court court = old ? Courts.OldImmigrationServicesTribunal : Courts.FirstTierTribunal_GRC;
            ExtendedMetadata meta = new()
            {
                Type = JudgmentType.Decision,
                Court = court,
                Date = new WNamedDate { Date = line.DecisionDate, Name = "decision" },
                Name = line.claimants + " v " + line.respondent,
                CaseNumbers = [line.CaseNo],
                Parties = [
                    new UK.Gov.NationalArchives.CaseLaw.Model.Party { Name = line.claimants, Role = PartyRole.Claimant },
                    new UK.Gov.NationalArchives.CaseLaw.Model.Party { Name = line.respondent, Role = PartyRole.Respondent }
                ],
                SourceFormat = sourceFormat,
                Categories = [.. categories]
            };
            return meta;
        }

    }

}