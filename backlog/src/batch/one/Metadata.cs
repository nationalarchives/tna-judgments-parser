
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
            public string created_datetime { get; set; }
            public string publication_datetime { get; set; }
            public string last_updatedtime { get; set; }
            public string decision_datetime { get; set; }
            // public string reported_no_1 { get; set; }
            // public string reported_no_2 { get; set; }
            // public string reported_no_3 { get; set; }
            public string file_no_1 { get; set; }
            public string file_no_2 { get; set; }
            public string file_no_3 { get; set; }
            // public string decision_type { get; set; }
            public string claimants { get; set; }
            public string respondent { get; set; }
            public string headnote_summary { get; set; }
            public string is_published { get; set; }
            public string main_category { get; set; }
            public string main_subcategory { get; set; }
            public string sec_category { get; set; }
            public string sec_subcategory { get; set; }
            public string Name { get; set; }
            public string FilePath { get; set; }
            public string Extension { get; set; }
            public string SizeInMB { get; set; }
            public string FileLastEditTime { get; set; }
            public string Skip { get; set; }

            // private readonly string DateFormat = "yyyy-MM-dd HH:mm:ss";
            private readonly string DateFormat = "M/d/yyyy H:mm";
            internal string DecisionDate { get => System.DateTime.ParseExact(decision_datetime, DateFormat, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"); }

            internal string CaseNo { get => string.Join('/', file_no_1, file_no_2, file_no_3); }

            internal bool ShouldSkip() { return Skip == "1"; }

        }

        internal static List<Line> Read(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<Line>().ToList();
        }

        internal static List<Line> FindLines(List<Line> lines, uint id)
        {
            return lines.Where(line => line.id == id.ToString()).ToList();
        }

        internal static ExtendedMetadata MakeMetadata(Line line) {
            List<ExtendedMetadata.Category> categories = [];
            categories.Add(new ExtendedMetadata.Category { Name = line.main_category });
            categories.Add(new ExtendedMetadata.Category { Name = line.main_subcategory, Parent = line.main_category });
            if (!string.IsNullOrWhiteSpace(line.sec_category)) {
                categories.Add(new ExtendedMetadata.Category { Name = line.sec_category });
                categories.Add(new ExtendedMetadata.Category { Name = line.sec_subcategory, Parent = line.sec_category });
            }
            string sourceFormat;
            if (line.Extension == ".doc" || line.Extension == ".docx")
                sourceFormat = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            else if (line.Extension == ".pdf")
                sourceFormat = "application/pdf";
            else
                throw new Exception(line.Extension);
            bool old = String.Compare(line.DecisionDate, "2010-01-18") < 0;
            Court court = old ? Courts.OldImmigationServicesTribunal : Courts.FirstTierTribunal_GRC; 
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
