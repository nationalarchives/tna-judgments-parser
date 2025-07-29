
using System.Collections.Generic;
using System.IO;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src.Batch.One
{
    class Helper
    {

        internal string PathToCourtMetadataFile { get; init; }

        internal string PathToDataFolder { get; init; }

        internal List<Metadata.Line> FindLines(uint id)
        {
            List<Metadata.Line> lines = Metadata.Read(PathToCourtMetadataFile);
            return Metadata.FindLines(lines, id);
        }

        internal Bundle GenerateBundle(Metadata.Line line, bool autoPublish = false) {
            if (line.Extension.ToLower() == ".pdf")
                return MakePdfBundle(line, autoPublish);
            else
                return MakeDocxBundle(line, autoPublish);
        }

        private List<Bundle.CustomField> CreateCustomFields(Metadata.Line line, string courtCode)
        {
            List<Bundle.CustomField> custom = [];
            if (!string.IsNullOrWhiteSpace(line.headnote_summary))
            {
                custom.Add(new Bundle.CustomField
                {
                    Name = "headnote_summary",
                    Source = courtCode,
                    Value = line.headnote_summary
                });
            }
            return custom;
        }

        private Bundle.Source CreateSource(string fileName, byte[] content, string mimeType)
        {
            return new Bundle.Source
            {
                Filename = Path.GetFileName(fileName),
                Content = content,
                MimeType = mimeType
            };
        }

        internal Bundle MakePdfBundle(Metadata.Line line, bool autoPublish) {
            var meta = Metadata.MakeMetadata(line);
            var pdf = Files.ReadFile(PathToDataFolder, line);
            var stub = Stub.Make(meta);
            
            Api.Meta meta2 = new() {
                DocumentType = "decision",
                Court = meta.Court?.Code,
                Date = line.DecisionDate,
                Name = line.claimants + " v " + line.respondent
            };
            
            Api.Response resp2 = new() {
                Xml = stub.Serialize(),
                Meta = meta2
            };

            var source = CreateSource(line.FilePath, pdf, "application/pdf");
            var custom = CreateCustomFields(line, meta.Court?.Code);
            
            return Bundle.Make(source, resp2, custom, autoPublish);
        }

        internal Bundle MakeDocxBundle(Metadata.Line line, bool autoPublish) {
            var meta = Metadata.MakeMetadata(line);
            var docx = Files.ReadFile(PathToDataFolder, line);

            Api.Meta meta2 = new()
            {
                DocumentType = "decision",
                Court = meta.Court?.Code,
                Date = meta.Date?.Date,
                Name = meta.Name,
                Extensions = new() {
                    SourceFormat = meta.SourceFormat,
                    CaseNumbers = meta.CaseNumbers,
                    Parties = meta.Parties,
                    Categories = meta.Categories
                }
            };
            
            Api.Request request = new() {
                Meta = meta2,
                Hint = Api.Hint.UKUT,
                Content = docx
            };
            Api.Response resp2 = Api.Parser.Parse(request);

            var source = CreateSource(line.FilePath, docx, meta.SourceFormat);
            var custom = CreateCustomFields(line, meta.Court?.Code);
            
            return Bundle.Make(source, resp2, custom, autoPublish);
        }

    }

}
