#nullable enable

using System;

using Backlog.Csv;

using CsvHelper.Configuration.Attributes;

namespace Backlog.Tracking;

internal class TrackerLine
{
    public string? Court { get; set;}
    public string? FileExtension {get; set; }
    
    
    public Guid SourceUuid { get; init; }

    [Ignore]
    public CsvLine? CsvLine { get; init; }

    public Guid ParserRunId { get; set; }
    public TrackerStatus TrackerStatus { get; set; }
    public string? TreReference { get; set; }
    public string? Ncn { get; set; }
    public string? CaseName { get; set; }
    public string? OriginalFileName { get; set; }
    public string? DocumentContentHash { get; set; }
    public string? CsvMetadataHash { get; set; }
    public string? ErrorMessage { get; set; }

    [Format("yyyy-MM-dd HH:mm:ss.fff")]
    public DateTimeOffset TrackerLineLastUpdated { get; set; }

    
}
