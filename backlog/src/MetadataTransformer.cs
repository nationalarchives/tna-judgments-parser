#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Backlog.Csv;
using Backlog.TreMetadata;

using TRE.Metadata;
using TRE.Metadata.Enums;
using TRE.Metadata.MetadataFieldTypes;

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
                        Filename = sourceFilename,
                        Mimetype = sourceMimeType,
                        Route = Route.Bulk,
                        Sha256 = contentHash
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

    public static List<IMetadataField> CsvLineToMetadataFields(CsvLine csvLine)
    {
        List<IMetadataField> metadataFields =
        [
            CreateExternalMetadataField(MetadataFieldName.CsvMetadataFileProperties, new CsvProperties(csvLine.CsvProperties.Name, csvLine.CsvProperties.Hash, csvLine.FullCsvLineContents)),
            CreateExternalMetadataField(MetadataFieldName.CaseNumber, csvLine.CaseNo),
            .. CreateExternalMetadataFields(MetadataFieldName.Category, () => csvLine.Categories),
            CreateExternalMetadataField(MetadataFieldName.Court, csvLine.court),
            CreateExternalMetadataField(MetadataFieldName.Date, csvLine.decision_datetime),
            .. CreateExternalMetadataFields(MetadataFieldName.Jurisdiction, () => csvLine.Jurisdictions),
            .. CreateExternalMetadataFields(MetadataFieldName.Party, () => csvLine.Parties)
        ];

        if (csvLine.ncn is not null)
        {
            metadataFields.Add(CreateExternalMetadataField(MetadataFieldName.Ncn, csvLine.ncn));
        }

        if (csvLine.headnote_summary is not null)
        {
            metadataFields.Add(CreateExternalMetadataField(MetadataFieldName.HeadnoteSummary,
                csvLine.headnote_summary));
        }

        if (csvLine.webarchiving is not null)
        {
            metadataFields.Add(CreateExternalMetadataField(MetadataFieldName.WebArchivingLink, csvLine.webarchiving));
        }

        return metadataFields;
    }

    private static IEnumerable<MetadataField<T>> CreateExternalMetadataFields<T>(MetadataFieldName metadataFieldName,
        Func<IEnumerable<T>> values)
    {
        return values().Select(item => CreateExternalMetadataField(metadataFieldName, item));
    }

    private static MetadataField<T> CreateExternalMetadataField<T>(MetadataFieldName metadataFieldName, T value)
    {
        return new MetadataField<T>
        {
            Id = Guid.NewGuid(),
            Name = metadataFieldName,
            Value = value,
            Source = MetadataSource.External,
            Timestamp = DateTime.UtcNow
        };
    }

    public static string GetFileName(string csvLineFilePath)
    {
        var pathSeparator = csvLineFilePath.Contains('/') ? '/' : '\\';

        var pathParts = csvLineFilePath.Split(pathSeparator);

        return pathParts[^1];
    }
}
