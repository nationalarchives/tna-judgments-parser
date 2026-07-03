#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Backlog.Csv;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backlog.Tracking;

internal interface ITracker
{
    Guid CurrentParserRunId { get; }
    bool HasCsvParseErrors { get; }
    int NumAllLinesInCsv { get; set; }
    void TrackCsvParseError(string errorMessage);
    void TrackSkipped(string identifier);
    Task<bool> IsAlreadySentToIngesterAsync(Guid sourceUuid);
    Task StartTrackingAsync(Guid sourceUuid, CsvLine csvLine, string csvMetadataHash);
    Task UpdateToParsedAsync(Guid sourceUuid, string treReference, string? ncn, string documentContentHash, string? caseName);
    Task UpdateToParserFailedAsync(Guid sourceUuid, Exception exception);
    Task UpdateToSentToIngesterAsync(Guid sourceUuid);
    Task LogFinalStatisticsAsync();
}

internal class Tracker : ITracker
{
    private readonly List<string> skippedCsvLineIdentifiers = [];
    private readonly List<string> csvParseErrors = [];
    private readonly TimeProvider timeProvider;
    private readonly ILogger<Tracker> logger;
    private readonly TrackerDbContext trackerDbContext;


    public Tracker(TimeProvider timeProvider, ILogger<Tracker> logger, TrackerDbContext trackerDbContext)
    {
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.trackerDbContext = trackerDbContext;

        trackerDbContext.Database.Migrate();
    }

    private IQueryable<TrackerLine> PreviousRunTrackerLines => trackerDbContext.ParserEvents
                                                                               .Where(t => t.ParserRunId !=
                                                                                   CurrentParserRunId);

    private IQueryable<TrackerLine> CurrentRunTrackerLines => trackerDbContext.ParserEvents
                                                                              .Where(t => t.ParserRunId ==
                                                                                  CurrentParserRunId);


    public Guid CurrentParserRunId { get; } = Guid.NewGuid();
    public bool HasCsvParseErrors => csvParseErrors.Any();
    public int NumAllLinesInCsv { get; set; }

    public void TrackCsvParseError(string errorMessage)
    {
        csvParseErrors.Add(errorMessage);
    }

    public void TrackSkipped(string identifier)
    {
        skippedCsvLineIdentifiers.Add(identifier);
    }

    public async Task<bool> IsAlreadySentToIngesterAsync(Guid sourceUuid)
    {
        return await PreviousRunTrackerLines.AnyAsync(previousRunTrackerLine =>
            previousRunTrackerLine.SourceUuid == sourceUuid
            && previousRunTrackerLine.TrackerStatus == TrackerStatus.SentToIngester);
    }

    public async Task StartTrackingAsync(Guid sourceUuid, CsvLine csvLine, string csvMetadataHash)
    {
        trackerDbContext.ParserEvents.Add(new TrackerLine
        {
            SourceUuid = sourceUuid,
            ParserRunId = CurrentParserRunId,
            TrackerStatus = TrackerStatus.Started,
            TrackerLineLastUpdated = timeProvider.GetUtcNow(),
            CsvMetadataHash = csvMetadataHash,
            FileExtension = csvLine.Extension,
            OriginalFileName = csvLine.FilePath,
            Court = csvLine.Court
        });

        await trackerDbContext.SaveChangesAsync();
    }

    public async Task UpdateToParsedAsync(Guid sourceUuid, string treReference, string? ncn, string documentContentHash,
        string? caseName)
    {
        var trackerLine = await CurrentRunTrackerLines.SingleAsync(t => t.SourceUuid == sourceUuid);

        trackerLine.TrackerStatus = TrackerStatus.Parsed;
        trackerLine.TreReference = treReference;
        trackerLine.Ncn = ncn;
        trackerLine.DocumentContentHash = documentContentHash;
        trackerLine.TrackerLineLastUpdated = timeProvider.GetUtcNow();
        trackerLine.CaseName = caseName;

        await trackerDbContext.SaveChangesAsync();
    }

    public async Task UpdateToParserFailedAsync(Guid sourceUuid, Exception exception)
    {
        var trackerLine = await CurrentRunTrackerLines.SingleAsync(t => t.SourceUuid == sourceUuid);

        trackerLine.TrackerStatus = TrackerStatus.ParserFailed;
        trackerLine.ErrorMessage = exception.Message;
        trackerLine.TrackerLineLastUpdated = timeProvider.GetUtcNow();

        await trackerDbContext.SaveChangesAsync();
    }

    public async Task UpdateToSentToIngesterAsync(Guid sourceUuid)
    {
        var trackerLine = await CurrentRunTrackerLines.SingleAsync(t => t.SourceUuid == sourceUuid);

        trackerLine.TrackerStatus = TrackerStatus.SentToIngester;
        trackerLine.TrackerLineLastUpdated = timeProvider.GetUtcNow();

        await trackerDbContext.SaveChangesAsync();
    }

    public async Task LogFinalStatisticsAsync()
    {
        var numSkippedCsvLines = skippedCsvLineIdentifiers.Count;
        var markedAsSkipIds = numSkippedCsvLines > 0
            ? StringJoinFirstFive(skippedCsvLineIdentifiers, ", ")
            : string.Empty;

        var sentToIngester = await CurrentRunTrackerLines
                                   .Where(t => t.TrackerStatus == TrackerStatus.SentToIngester)
                                   .ToArrayAsync();
        var alreadyDoneLines = await trackerDbContext.ParserEvents
                                                     .Where(t => t.ParserRunId != CurrentParserRunId)
                                                     .Where(t => t.TrackerStatus == TrackerStatus.SentToIngester)
                                                     .ToArrayAsync();
        var successfulFileExtensionBreakdown = string.Join(", ",
            sentToIngester.GroupBy(l => l.FileExtension).Select(g => $"{g.Count()} {g.Key}"));

        logger.LogInformation("""
                              ---------------------------
                              Successfully processed {SuccessfulLinesCount} of {CsvLinesCount} csv lines, of which:
                                - {NewLinesCount} lines were new ({SuccessfulFileExtensionBreakdown})
                                - {MarkedToSkipLineCount} lines were marked in the csv to skip ({MarkedToSkipIds})
                                - {AlreadyDoneLineCount} lines were skipped because they had been processed in a previous run
                              """,
            numSkippedCsvLines + alreadyDoneLines.Length + sentToIngester.Length,
            NumAllLinesInCsv,
            sentToIngester.Length,
            successfulFileExtensionBreakdown,
            numSkippedCsvLines, markedAsSkipIds,
            alreadyDoneLines.Length
        );

        if (csvParseErrors.Count > 0)
        {
            logger.LogError("""
                            ---------------------------
                            Failed to read {FailedLineCount} lines from the csv:
                            {FailedLineDetails}
                            """,
                csvParseErrors.Count,
                string.Join(Environment.NewLine, csvParseErrors.Select(error => $"  - {error}"))
            );
        }

        var failedToProcessLines = await CurrentRunTrackerLines
                                         .Where(t => t.TrackerStatus == TrackerStatus.ParserFailed)
                                         .ToArrayAsync();

        if (failedToProcessLines.Length > 0)
        {
            var failedIdsGroupedByErrorMessage = failedToProcessLines
                .GroupBy(t =>
                    {
                        return t.ErrorMessage switch
                        {
                            _ when t.ErrorMessage?.StartsWith("Could not find file") ?? false => "Could not find file",
                            _ when t.ErrorMessage?.StartsWith("Couldn't find file with UUID") ?? false =>
                                "Couldn't find file with UUID",
                            _ when t.ErrorMessage?.EndsWith("was not recognized as a valid DateTime.") ?? false =>
                                "String was not recognized as a valid DateTime",
                            _ => t.ErrorMessage
                        };
                    },
                    t => t.SourceUuid.ToString());

            var groupedErrorDescriptions = failedIdsGroupedByErrorMessage.Select(groupOfErrors =>
                $"  - {groupOfErrors.Count()} lines failed with exception message \"{groupOfErrors.Key}\". Ids affected were: ({StringJoinFirstFive(groupOfErrors, ", ")})");
            var failedFileExtensionBreakdown = string.Join(", ",
                failedToProcessLines.GroupBy(t => t.FileExtension).Select(g => $"{g.Count()} {g.Key}"));

            logger.LogError("""
                            ---------------------------
                            Failed to process {FailedLineCount} lines ({FailedFileExtensionBreakdown}), of which:
                            {GroupedErrorDescriptions}
                            """,
                failedToProcessLines.Length,
                failedFileExtensionBreakdown,
                StringJoinFirstFive(groupedErrorDescriptions, Environment.NewLine)
            );
        }

        if (failedToProcessLines.Length == 0 && csvParseErrors.Count == 0)
        {
            logger.LogInformation("No failed lines");
        }
    }

    private static string StringJoinFirstFive(IEnumerable<string> unenumeratedCollection, string separator)
    {
        var array = unenumeratedCollection as string[] ?? unenumeratedCollection.ToArray();
        return array.Length <= 5
            ? string.Join(separator, array)
            : string.Join(separator, array.Take(5)) + "...";
    }
}
