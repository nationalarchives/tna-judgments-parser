
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CsvHelper;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Office2010.CustomUI;
using DocumentFormat.OpenXml.Wordprocessing;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace Backlog.Src.Batch.One
{

    class Metadata
    {

        class Line
        {
            public string id { get; set; }
            public string created_datetime { get; set; }
            public string publication_datetime { get; set; }
            public string last_updatedtime { get; set; }
            public string decision_datetime { get; set; }
            public string reported_no_1 { get; set; }
            public string reported_no_2 { get; set; }
            public string reported_no_3 { get; set; }
            public string file_no_1 { get; set; }
            public string file_no_2 { get; set; }
            public string file_no_3 { get; set; }
            public string decision_type { get; set; }
            public string claimants { get; set; }
            public string respondent { get; set; }
            public string main_subcategory_id { get; set; }
            public string sec_subcategory_id { get; set; }
            public string headnote_summary { get; set; }
            public string is_published { get; set; }

            private readonly string DateFormat = "M/d/yyyy H:mm";
            internal string DecisionDate { get => System.DateTime.ParseExact(decision_datetime, DateFormat, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"); }

            internal string CaseNo { get => string.Join('/', file_no_1, file_no_2, file_no_3); }
        }

        internal static void Read()
        {
            string path = @"C:\Users\Administrator\TDR-2024-CG6F_converted\imset-judgments_RowsCleanedHL.csv";
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var lines = csv.GetRecords<Line>();
            foreach (var line in lines)
            {
                if (line.id.EndsWith('*'))
                    continue;
                if (line.is_published != "1")
                    continue;
                System.Console.WriteLine(line.id);
                var file = Files.GetPdf(line.id);
                if (file is null)
                    continue;

                MakeAndSaveBundle(line, file, 0);

                // Stub stub = GenerateStub(line);
                // using var output = System.Console.OpenStandardOutput();
                // stub.Serialize(output);
                break;
            }
            System.Console.WriteLine("done.");
        }

        internal static byte[] FindLineAndMakeBundle(uint id, uint bulkNum)
        {
            string path = @"C:\Users\Administrator\TDR-2024-CG6F_converted\imset-judgments_RowsCleanedHL.csv";
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var lines = csv.GetRecords<Line>();
            foreach (var line in lines)
            {
                if (line.id != id.ToString())
                    continue;
                var file = Files.GetPdf(line.id);
                if (file is null)
                    throw new Exception(file);

                return MakeAndSaveBundle(line, file, bulkNum);
            }
            throw new Exception(id.ToString());
        }

        private static byte[] MakeAndSaveBundle(Line line, string file, uint bulkNum) {
            Stub stub = GenerateStub(line, "application/pdf");
            Bundle.Source source = new() {
                Filename = Path.GetFileName(file),
                Content = File.ReadAllBytes(file),
                MimeType = "application/pdf"
            };
            UK.Gov.NationalArchives.Judgments.Api.Meta meta2 = new() {
                DocumentType = "judgment",
                Court = Courts.FirstTierTribunal_GRC.Code,
                Date = line.DecisionDate,
                Name = line.claimants + " v " + line.respondent,
                Attachments = []
            };
            UK.Gov.NationalArchives.Judgments.Api.Response resp2 = new() {
                Xml = stub.Serialize(),
                Meta = meta2
            };
            byte[] bundle = Bundle.Make(source, resp2, bulkNum);
            var tarPath = Regex.Replace(file, @"\.pdf$", ".tar.gz");
            if (tarPath == file)
                throw new Exception();
            File.WriteAllBytes(tarPath, bundle);
            return bundle;
        }

        internal static ExtendedMetadata GetMetadata(uint id, string sourceFormat) {
            string path = @"C:\Users\Administrator\TDR-2024-CG6F_converted\imset-judgments_RowsCleanedHL.csv";
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var lines = csv.GetRecords<Line>();
            foreach (var line in lines)
            {
                if (line.id != id.ToString())
                    continue;
                return GetMetadata(line, sourceFormat);
            }
            throw new Exception(id.ToString());
        }

        private static ExtendedMetadata GetMetadata(Line line, string sourceFormat) {
            ISet<string> parents = new HashSet<string>();
            List<ExtendedMetadata.Category> categories = [];
            if (!string.IsNullOrWhiteSpace(line.main_subcategory_id)) {
                var cat = Categories.Get(int.Parse(line.main_subcategory_id));
                if (parents.Add(cat.Category))
                    categories.Add(new ExtendedMetadata.Category { Name = cat.Category });
                categories.Add(new ExtendedMetadata.Category { Name = cat.Subcategory, Parent = cat.Category });
            }
            if (!string.IsNullOrWhiteSpace(line.sec_subcategory_id)) {
                try {
                    var cat = Categories.Get(int.Parse(line.sec_subcategory_id));
                if (parents.Add(cat.Category))
                    categories.Add(new ExtendedMetadata.Category { Name = cat.Category });
                categories.Add(new ExtendedMetadata.Category { Name = cat.Subcategory, Parent = cat.Category });
                } catch (System.FormatException) { }
            }
            ExtendedMetadata meta = new()
            {
                Type = JudgmentType.Decision,
                Court = Courts.FirstTierTribunal_GRC,
                Date = new WNamedDate { Date = line.DecisionDate, Name = "decision" },
                Name = line.claimants + " v " + line.respondent,
                CaseNumbers = [line.CaseNo],
                Parties = [
                    // new WParty(line.claimants, null) { Role = PartyRole.Claimant },
                    // new WParty(line.respondent, null) { Role = PartyRole.Respondent }
                    new UK.Gov.NationalArchives.CaseLaw.Model.Party() { Name = line.claimants, Role = PartyRole.Claimant },
                    new UK.Gov.NationalArchives.CaseLaw.Model.Party() { Name = line.respondent, Role = PartyRole.Respondent }
                ],
                SourceFormat = sourceFormat,
                Categories = [.. categories]
            };
            return meta;
        }

        private static Stub GenerateStub(Line line, string sourceFormat)
        {
            ExtendedMetadata meta = GetMetadata(line, sourceFormat);
            Stub stub = Stub.Make(meta);
            var errors = stub.Validate();
            if (errors.Count > 0)
                throw new System.Exception(errors[0].Message, errors[0].Exception);
            return stub;
        }

    }

}
