using System;
using System.Collections.Generic;
using System.Linq;

using Backlog.Csv;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace Backlog.Src;

internal static class MetadataTransformer
{
    internal static ExtendedMetadata MakeMetadata(CsvLine line)
    {
        // Validation is now handled during CSV reading
        List<ExtendedMetadata.Category> categories = [];

        // Only add categories if they exist and are not empty
        if (!string.IsNullOrWhiteSpace(line.main_category))
        {
            categories.Add(new ExtendedMetadata.Category { Name = line.main_category });

            if (!string.IsNullOrWhiteSpace(line.main_subcategory))
            {
                categories.Add(new ExtendedMetadata.Category
                {
                    Name = line.main_subcategory, Parent = line.main_category
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(line.sec_category))
        {
            categories.Add(new ExtendedMetadata.Category { Name = line.sec_category });

            if (!string.IsNullOrWhiteSpace(line.sec_subcategory))
            {
                categories.Add(
                    new ExtendedMetadata.Category { Name = line.sec_subcategory, Parent = line.sec_category });
            }
        }

        string sourceFormat;
        if (line.Extension == ".doc" || line.Extension == ".docx")
        {
            sourceFormat = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        }
        else if (line.Extension == ".pdf")
        {
            sourceFormat = "application/pdf";
        }
        else
        {
            throw new Exception($"Unexpected extension {line.Extension}");
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
            SourceFormat = sourceFormat,
            Categories = [.. categories],
            NCN = line.ncn,
            WebArchivingLink = webArchivingLink
        };
        return meta;
    }
}
