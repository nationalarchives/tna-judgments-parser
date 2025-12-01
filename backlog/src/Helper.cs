using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src
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

        private Api.Response CreateResponse(ExtendedMetadata meta, byte[] content)
        {
            var isPdf = meta.SourceFormat.ToLower() == "application/pdf";
            if (isPdf)
            {
                var metadata = new Api.Meta
                {
                    DocumentType = "decision",
                    Court = meta.Court?.Code,
                    Date = meta.Date?.Date.ToString(),
                    Name = meta.Name,
                };

                var stub = Stub.Make(meta);

                var response = new Api.Response { Xml = stub.Serialize(), Meta = metadata };
                return response;
            }
            else
            {
                var metadata = new Api.Meta
                {
                    DocumentType = "decision",
                    Court = meta.Court?.Code,
                    Date = meta.Date?.Date,
                    Name = meta.Name,
                    JurisdictionShortNames = meta.Jurisdictions.Select(j => j.ShortName).ToList(),
                    Extensions = new()
                    {
                        SourceFormat = meta.SourceFormat,
                        CaseNumbers = meta.CaseNumbers,
                        Parties = meta.Parties,
                        Categories = meta.Categories,
                        WebArchivingLink = meta.WebArchivingLink
                    }
                };

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

        internal Bundle GenerateBundle(Metadata.Line line, string judgmentsFilePath, string hmctsFilePath, bool autoPublish = false)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            if (string.IsNullOrWhiteSpace(line.FilePath))
                throw new ArgumentException("FilePath cannot be empty", nameof(line));

            if (string.IsNullOrWhiteSpace(line.Extension))
                throw new ArgumentException("Extension cannot be empty", nameof(line));

            var meta = Metadata.MakeMetadata(line);

            var content = Files.ReadFile(PathToDataFolder, line, judgmentsFilePath, hmctsFilePath);

            var response = CreateResponse(meta, content);

            var source = new Bundle.Source
            {
                Filename = Path.GetFileName(line.FilePath),
                Content = content,
                MimeType = meta.SourceFormat
            };
            var customFields = CreateCustomFields(line, meta.Court?.Code);
            System.Console.WriteLine($"Creating bundle with source: {source.Filename}");
            System.Console.WriteLine($"Creating bundle with content: {source.Content.Length} bytes");
            return Bundle.Make(source, response, customFields, autoPublish);
        }
    }
}
