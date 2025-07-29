
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

        private record FormatSpecificData(string MimeType, Func<Api.Meta, byte[], Api.Response> ProcessContent);

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

        private Bundle.Source CreateSource(string fileName, byte[] content, string mimeType)
        {
            return new Bundle.Source
            {
                Filename = Path.GetFileName(fileName),
                Content = content,
                MimeType = mimeType
            };
        }

        private FormatSpecificData GetFormatData(bool isPdf, ExtendedMetadata metadata)
        {
            if (isPdf)
            {
                return new FormatSpecificData(
                    "application/pdf",
                    (meta, content) =>
                    {
                        var stub = Stub.Make(metadata);
                        return new Api.Response { Xml = stub.Serialize(), Meta = meta };
                    }
                );
            }

            return new FormatSpecificData(
                metadata.SourceFormat,
                (meta, content) =>
                {
                    var request = new Api.Request
                    {
                        Meta = meta,
                        Hint = Api.Hint.UKUT,
                        Content = content
                    };
                    return Api.Parser.Parse(request);
                }
            );
        }

        internal Bundle GenerateBundle(Metadata.Line line, bool autoPublish = false)
        {
            var isPdf = line.Extension.ToLower() == ".pdf";
            var meta = Metadata.MakeMetadata(line);
            var content = Files.ReadFile(PathToDataFolder, line);
            var formatData = GetFormatData(isPdf, meta);
            
            var meta2 = CreateMetadata(line, meta, isPdf);
            var resp2 = formatData.ProcessContent(meta2, content);
            
            var source = CreateSource(line.FilePath, content, formatData.MimeType);
            var custom = CreateCustomFields(line, meta.Court?.Code);
            
            return Bundle.Make(source, resp2, custom, autoPublish);
        }
    }
}
