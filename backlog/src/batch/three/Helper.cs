
using System.Collections.Generic;
using System.IO;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src.Batch.One
{
    class Helper
    {

        internal string PathToCourtMetadataFile { get; init; }

        internal string PathDoDataFolder { get; init; }

        internal List<Metadata.Line> FindLines(uint id)
        {
            List<Metadata.Line> lines = Metadata.Read(PathToCourtMetadataFile);
            return Metadata.FindLines(lines, id);
        }

        internal Bundle GenerateBundle(Metadata.Line line, bool autoPublish = false) {
            if (line.Extension == ".pdf")
                return MakePdfBundle(line, autoPublish);
            else
                return MakeDocxBundle(line, autoPublish);
        }

        internal Bundle MakePdfBundle(Metadata.Line line, bool autoPublish) {
            var meta = Metadata.MakeMetadata(line);
            var pdf = Files.ReadFile(PathDoDataFolder, line);
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
            Bundle.Source source = new() {
                Filename = Path.GetFileName(line.FilePath),
                Content = pdf,
                MimeType = "application/pdf"
            };
            return Bundle.Make(source, resp2, autoPublish);
        }

        internal Bundle MakeDocxBundle(Metadata.Line line, bool autoPublish) {
            var meta = Metadata.MakeMetadata(line);
            var docx = Files.ReadFile(PathDoDataFolder, line);
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
                Content = docx
            };
            Api.Response resp2 = Api.Parser.Parse(request);
            Bundle.Source source = new() {
                Filename = Path.GetFileName(line.FilePath),
                Content = docx,
                MimeType = meta.SourceFormat
            };
            return Bundle.Make(source, resp2, autoPublish);
        }

    }

}
