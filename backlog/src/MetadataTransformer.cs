using System;
using System.Collections.Generic;
using System.Linq;

using Backlog.Csv;
using Backlog.TreMetadata;

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

        ExtendedMetadata meta = new()
        {
            Type = JudgmentType.Decision,
            Court = Courts.GetByCode(line.court),
            Jurisdictions = line.Jurisdictions.Select(jurisdiction => new OutsideJurisdiction { ShortName = jurisdiction }),
            Date = new WNamedDate { Date = line.decision_datetime.ToString("yyyy-MM-dd"), Name = "decision" },
            Name = line.FirstPartyName + " v " + line.respondent,
            CaseNumbers = [line.CaseNo],
            Parties = line.Parties.ToList(),
            Categories = line.Categories.ToList(),
            SourceFormat = sourceFormat,
            NCN = line.ncn,
            WebArchivingLink = line.webarchiving
        };
        return meta;
    }
}
