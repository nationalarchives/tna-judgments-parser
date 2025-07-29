
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

        private Api.Meta CreateMetadata(string court, string date, string name, ExtendedMetadata meta = null)
        {
            var metadata = new Api.Meta
            {
                DocumentType = "decision",
                Court = court,
                Date = date,
                Name = name
            };

            if (meta != null)
            {
                metadata.Extensions = new()
                {
                    SourceFormat = meta.SourceFormat,
                    CaseNumbers = meta.CaseNumbers,
                    Parties = meta.Parties,
                    Categories = meta.Categories
                };
            }

            return metadata;
        }

        private List<Bundle.CustomField> CreateCustomFields(Metadata.Line line, string courtCode)
        {
            List<Bundle.CustomField> customFields = [];
            if (!string.IsNullOrWhiteSpace(line.headnote_summary))
            {
                customFields.Add(new Bundle.CustomField
                {
                    Name = "headnote_summary",
                    Source = courtCode,
                    Value = line.headnote_summary
                });
            }
            return customFields;
        }

        private Api.Response CreateResponse(ExtendedMetadata meta, Metadata.Line line, byte[] content, bool isPdf)
        {
            if (isPdf)
            {
                var metadata = CreateMetadata(
                    meta.Court?.Code,
                    line.DecisionDate?.ToString(),
                    line.claimants + " v " + line.respondent
                );

                var stub = Stub.Make(meta);
                var response = new Api.Response { Xml = stub.Serialize(), Meta = metadata };
                return response;
            }
            else
            {
                var metadata = CreateMetadata(
                    meta.Court?.Code,
                    meta.Date?.Date.ToString(),
                    meta.Name,
                    meta
                );

                var request = new Api.Request
                {
                    Meta = metadata,
                    Hint = Api.Hint.UKUT,
                    Content = content
                };
                var response = Api.Parser.Parse(request);
                return response;
            }
        }

        internal Bundle GenerateBundle(Metadata.Line line, bool autoPublish = false)
        {
            var isPdf = line.Extension.ToLower() == ".pdf";
            var meta = Metadata.MakeMetadata(line);
            var content = Files.ReadFile(PathToDataFolder, line);
            
            var response = CreateResponse(meta, line, content, isPdf);
            
            var source = new Bundle.Source
            {
                Filename = Path.GetFileName(line.FilePath),
                Content = content,
                MimeType = meta.SourceFormat
            };

            var customFields = CreateCustomFields(line, meta.Court?.Code);
            
            return Bundle.Make(source, response, customFields, autoPublish);
        }
    }
}
