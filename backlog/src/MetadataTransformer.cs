using System;
using System.Collections.Generic;
using System.Linq;

using Backlog.Csv;

using TRE.Metadata.MetadataFieldTypes;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace Backlog.Src;

internal static class MetadataTransformer
{
    internal static ExtendedMetadata MakeMetadata(CsvLine line)
    {
        // Validation is now handled during CSV reading
        List<Category> categories = [];

        // Only add categories if they exist and are not empty
        if (!string.IsNullOrWhiteSpace(line.main_category))
        {
            categories.Add(new Category { Name = line.main_category });

            if (!string.IsNullOrWhiteSpace(line.main_subcategory))
            {
                categories.Add(new Category
                {
                    Name = line.main_subcategory, Parent = line.main_category
                });
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

        var court = Courts.GetByCode(line.court);

        var jurisdictions = line.Jurisdictions
                                .Where(jurisdiction => !string.IsNullOrWhiteSpace(jurisdiction))
                                .Select(jurisdiction => new OutsideJurisdiction { ShortName = jurisdiction });

        string webArchivingLink;
        if (!string.IsNullOrWhiteSpace(line.webarchiving))
        {
            webArchivingLink = line.webarchiving;
        }
        else
        {
            webArchivingLink = null;
        }

        ExtendedMetadata meta = new()
        {
            Type = JudgmentType.Decision,
            Court = court,
            Jurisdictions = jurisdictions,
            Date = new WNamedDate { Date = line.decision_datetime.ToString("yyyy-MM-dd"), Name = "decision" },
            Name = line.FirstPartyName + " v " + line.respondent,
            CaseNumbers = [line.CaseNo],
            Parties =
            [
                new UK.Gov.NationalArchives.CaseLaw.Model.Party
                {
                    Name = line.FirstPartyName, Role = line.FirstPartyRole
                },
                new UK.Gov.NationalArchives.CaseLaw.Model.Party { Name = line.respondent, Role = PartyRole.Respondent }
            ],
            Categories = [.. categories],
            SourceFormat = GetMimeType(line.Extension),
            NCN = line.ncn,
            WebArchivingLink = webArchivingLink
        };
        return meta;
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
}
