#nullable enable

using System;

using NationalArchives.FindCaseLaw.Utils.Metadata.Enums;
using NationalArchives.FindCaseLaw.Utils.Metadata.MetadataFieldTypes;

using TRE.Metadata.MetadataFieldTypes;

using UK.Gov.NationalArchives.CaseLaw.Model;

namespace TRE.Metadata.Enums;

public static class MetadataFieldNameExtensions
{
    public static Type GetFieldValueType(this MetadataFieldName metadataFieldName)
    {
        return metadataFieldName switch
        {
            MetadataFieldName.CaseNumber
                or MetadataFieldName.Court
                or MetadataFieldName.HeadnoteSummary
                or MetadataFieldName.Jurisdiction
                or MetadataFieldName.Name
                or MetadataFieldName.Ncn
                or MetadataFieldName.SourceFormat
                or MetadataFieldName.Uri
                or MetadataFieldName.WebArchivingLink => typeof(string),
            MetadataFieldName.Category => typeof(Category),
            MetadataFieldName.CsvMetadataFileProperties => typeof(CsvProperties),
            MetadataFieldName.Date => typeof(DateTime),
            MetadataFieldName.Judge => typeof(Judge),
            MetadataFieldName.Party => typeof(Party),
            _ => throw new ArgumentOutOfRangeException(nameof(metadataFieldName), metadataFieldName, null)
        };
    }
}
