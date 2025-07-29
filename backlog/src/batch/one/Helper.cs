
using System;
using System.Collections.Generic;
using System.IO;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using ExtendedMetadata = Backlog.Src.ExtendedMetadata;

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

        private Api.Meta CreateMetadata(Metadata.Line line, ExtendedMetadata meta, bool isPdf)
        {
            var baseMeta = new Api.Meta
            {
                DocumentType = "decision",
                Court = meta.Court?.Code
            };

            if (isPdf)
            {
                baseMeta.Date = line.DecisionDate;
                baseMeta.Name = line.claimants + " v " + line.respondent;
            }
            else
            {
                baseMeta.Date = meta.Date?.Date;
                baseMeta.Name = meta.Name;
                baseMeta.Extensions = new()
                {
                    SourceFormat = meta.SourceFormat,
                    CaseNumbers = meta.CaseNumbers,
                    Parties = meta.Parties,
                    Categories = meta.Categories
                };
            }

            return baseMeta;
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

        internal Bundle GenerateBundle(Metadata.Line line, bool autoPublish = false)
        {
            var isPdf = line.Extension.ToLower() == ".pdf";
            var meta = Metadata.MakeMetadata(line);
            var content = Files.ReadFile(PathToDataFolder, line);
            var metadata = CreateMetadata(line, meta, isPdf);

            Api.Response response;
            
            if (isPdf)
            {
                var stub = Stub.Make(meta);
                response = new Api.Response { Xml = stub.Serialize(), Meta = metadata };
            }
            else
            {
                var request = new Api.Request
                {
                    Meta = metadata,
                    Hint = Api.Hint.UKUT,
                    Content = content
                };
                response = Api.Parser.Parse(request);
            }
            
            var source = new Bundle.Source
            {
                Filename = Path.GetFileName(line.FilePath),
                Content = content,
                MimeType = meta.SourceFormat
            };
            
            var custom = CreateCustomFields(line, meta.Court?.Code);
            
            return Bundle.Make(source, response, custom, autoPublish);
        }
    }
}
