#nullable enable

using System;
using System.Collections.Generic;

using TRE.Metadata.MetadataFieldTypes;

using UK.Gov.NationalArchives.CaseLaw.Model;

namespace TRE.Metadata.Enums;

public enum MetadataFieldName
{
    CaseNumber,
    Category,
    Court,
    CsvMetadataFileProperties,
    Date,
    HeadnoteSummary,
    Jurisdiction,
    Name,
    Ncn,
    Party,
    SourceFormat,
    Uri,
    WebArchivingLink
}

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
                or MetadataFieldName.WebArchivingLink => typeof(string),
            MetadataFieldName.Category => typeof(Category),
            MetadataFieldName.CsvMetadataFileProperties => typeof(CsvProperties),
            MetadataFieldName.Date => typeof(DateTime),
            MetadataFieldName.Party => typeof(Party),
            MetadataFieldName.Uri => typeof(Uri),
            _ => throw new ArgumentOutOfRangeException(nameof(metadataFieldName), metadataFieldName, null)
        };
    }
}
