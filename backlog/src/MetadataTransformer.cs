using System;
using System.Linq;

using Backlog.Csv;
using Backlog.TreMetadata;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src;

internal static class MetadataTransformer
{
    internal static FullTreMetadata CreateFullTreMetadata(string sourceFilename, string sourceMimeType,
        string contentHash, bool autoPublish, Image[] images, Meta responseMeta,
        List<IMetadataField> externalMetadataFields, bool xmlContainsDocumentText)
    {
        var metadata = new FullTreMetadata
        {
            Parameters = new Parameters
            {
                TRE = new Tre
                {
                    Reference = Guid.NewGuid().ToString(),
                    Payload = new Payload
                    {
                        Filename = sourceFilename,
                        Images = images.Select(i => i.Name).ToArray(),
                        Log = null
                    }
                },
                PARSER = new ParserProcessMetadata
                {
                    Court = responseMeta.Court,
                    Cite = responseMeta.Cite,
                    Date = responseMeta.Date,
                    Name = responseMeta.Name,
                    Extensions = responseMeta.Extensions,
                    Attachments = responseMeta.Attachments ?? [],
                    DocumentType = Enum.Parse<DocumentType>(responseMeta.DocumentType, true),
                    ErrorMessages = [],
                    MetadataFields = externalMetadataFields,
                    PrimarySource = new PrimarySourceFile
                    {
                        Filename = null,
                        Mimetype = null,
                        Route = Route.Bulk,
                        Sha256 = null
                    },
                    XmlContainsDocumentText = xmlContainsDocumentText
                },
                IngestorOptions = new IngestorOptions
                {
                    AutoPublish = autoPublish,
                    Source = new SourceDocument { Format = sourceMimeType, Hash = contentHash }
                }
            }
        };
        return metadata;
    }

    internal static StubMetadata MakeMetadata(CsvLine line)
    {
        StubMetadata meta = new()
        {
            Type = JudgmentType.Decision,
            Court = Courts.GetByCode(line.court),
            Jurisdictions = line.Jurisdictions.Select(jurisdiction => new OutsideJurisdiction { ShortName = jurisdiction }),
            Date = new WNamedDate { Date = line.decision_datetime.ToString("yyyy-MM-dd"), Name = "decision" },
            Name = line.FirstPartyName + " v " + line.respondent,
            CaseNumbers = [line.CaseNo],
            Parties = line.Parties.ToList(),
            Categories = line.Categories.ToList(),
            SourceFormat = GetMimeType(line.Extension),
            NCN = line.ncn,
            WebArchivingLink = line.webarchiving
        };
        return meta;
    }

    public static string GetMimeType(string fileExtension)
    {
        return fileExtension switch
        {
            ".doc" or ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".pdf" => "application/pdf",
            _ => throw new ArgumentOutOfRangeException(nameof(fileExtension), $"Unexpected extension {fileExtension}")
        };
    }
}
