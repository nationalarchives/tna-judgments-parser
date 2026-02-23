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

using Party = UK.Gov.NationalArchives.CaseLaw.Model.Party;

namespace Backlog.Src;

internal static class MetadataTransformer
{
    internal static FullTreMetadata CreateFullTreMetadata(string sourceFilename, string sourceMimeType, 
        string contentHash, bool autoPublish, Image[] images, Meta responseMeta)
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
                PARSER = responseMeta,
                IngestorOptions = new IngestorOptions
                {
                    AutoPublish = autoPublish,
                    Source = new SourceDocument { Format = sourceMimeType, Hash = contentHash }
                }
            }
        };
        return metadata;
    }

    internal static ExtendedMetadata MakeMetadata(CsvLine line)
    {
        var court = Courts.GetByCode(line.court);

        var jurisdictions = line.Jurisdictions
                                .Where(jurisdiction => !string.IsNullOrWhiteSpace(jurisdiction))
                                .Select(jurisdiction => new OutsideJurisdiction { ShortName = jurisdiction });

        var webArchivingLink = string.IsNullOrWhiteSpace(line.webarchiving) ? null : line.webarchiving;

        ExtendedMetadata meta = new()
        {
            Type = JudgmentType.Decision,
            Court = court,
            Jurisdictions = jurisdictions,
            Date = new WNamedDate { Date = line.decision_datetime.ToString("yyyy-MM-dd"), Name = "decision" },
            Name = line.FirstPartyName + " v " + line.respondent,
            CaseNumbers = [line.CaseNo],
            Parties = GetParties(line),
            Categories = [..GetCategories(line)],
            SourceFormat = GetMimeType(line.Extension),
            NCN = line.ncn,
            WebArchivingLink = webArchivingLink
        };
        return meta;
    }

    private static List<Party> GetParties(CsvLine line)
    {
        return
        [
            new Party { Name = line.FirstPartyName, Role = line.FirstPartyRole },
            new Party { Name = line.respondent, Role = PartyRole.Respondent }
        ];
    }

    private static List<Category> GetCategories(CsvLine line)
    {
        List<Category> categories = [];

        // Only add categories if they exist and are not empty
        if (!string.IsNullOrWhiteSpace(line.main_category))
        {
            categories.Add(new Category { Name = line.main_category });

            if (!string.IsNullOrWhiteSpace(line.main_subcategory))
            {
                categories.Add(new Category { Name = line.main_subcategory, Parent = line.main_category });
            }
        }

        if (!string.IsNullOrWhiteSpace(line.sec_category))
        {
            categories.Add(new Category { Name = line.sec_category });

            if (!string.IsNullOrWhiteSpace(line.sec_subcategory))
            {
                categories.Add(
                    new Category { Name = line.sec_subcategory, Parent = line.sec_category });
            }
        }

        return categories;
    }

    private static string GetMimeType(string fileExtension)
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
            CreateExternalMetadataField(MetadataFieldName.CsvMetadataFileContents, csvLine.FullCsvLineContents),
            CreateExternalMetadataField(MetadataFieldName.CaseNumber, csvLine.CaseNo),
            .. GetCategories(csvLine)
                .Select(category => CreateExternalMetadataField(MetadataFieldName.Category, category)),
            CreateExternalMetadataField(MetadataFieldName.Court, csvLine.court),
            CreateExternalMetadataField(MetadataFieldName.Date, csvLine.decision_datetime),
            .. csvLine.Jurisdictions.Select(jurisdiction =>
                CreateExternalMetadataField(MetadataFieldName.Jurisdiction, jurisdiction)),
            .. GetParties(csvLine).Select(party => CreateExternalMetadataField(MetadataFieldName.Party, party))
        ];

        if (csvLine.ncn is not null)
        {
            metadataFields.Add(CreateExternalMetadataField(MetadataFieldName.Ncn, csvLine.ncn));
        }
        
        if (csvLine.headnote_summary is not null)
        {
            metadataFields.Add(CreateExternalMetadataField(MetadataFieldName.HeadnoteSummary, csvLine.headnote_summary));
        }

        if (csvLine.webarchiving is not null)
        {
            metadataFields.Add(CreateExternalMetadataField(MetadataFieldName.WebArchivingLink, csvLine.webarchiving));
        }

        return metadataFields;
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
}
