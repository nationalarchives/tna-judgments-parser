
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
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public string ParentFolder { get; set; }
            public string Extension { get; set; }
            public string SizeInMB { get; set; }
            public string AppellantName { get; set; }
            public string FileLastEditTime { get; set; }
            public string RespondentName { get => "Office of Fair Trading"; }

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
                throw new Exception(line.Extension);
            
            Court court = Courts.XXX; 
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
