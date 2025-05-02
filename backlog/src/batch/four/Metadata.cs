
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using CsvHelper;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace Backlog.Src.Batch.Four
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

        internal static ExtendedMetadata MakeMetadata(Line line) {
            string sourceFormat;
            sourceFormat = "application/pdf";
            
            Court court = Courts.EstateAgentsTribunal;

            ExtendedMetadata meta = new()
            {
                Type = JudgmentType.Decision,
                Court = court,
                //Date = new WNamedDate { Date = line.DecisionDate, Name = "decision" },
                Name = line.AppellantName + " v " + line.RespondentName,
                //CaseNumbers = [line.CaseNo],
                Parties = [
                    new UK.Gov.NationalArchives.CaseLaw.Model.Party { Name = line.AppellantName, Role = PartyRole.Appellant },
                    new UK.Gov.NationalArchives.CaseLaw.Model.Party { Name = line.RespondentName, Role = PartyRole.Respondent }
                ],
                SourceFormat = sourceFormat,
            };
            return meta;
        }

    }

}
