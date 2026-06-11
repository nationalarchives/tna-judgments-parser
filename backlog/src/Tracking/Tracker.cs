#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;

using Backlog.Csv;
using Backlog.Options;

using CsvHelper;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backlog.Tracking;

internal interface ITracker
{
    Guid CurrentParserRunId { get; }
    bool HasCsvParseErrors { get; }
    int NumAllLinesInCsv { get; set; }
    void TrackCsvParseError(string errorMessage);
    void TrackSkipped(string identifier);
    bool IsAlreadySentToIngester(Guid sourceUuid);
    Task StartTrackingAsync(Guid sourceUuid, CsvLine csvLine, string csvMetadataHash);
    Task UpdateToParsedAsync(Guid sourceUuid, string treReference, string? ncn, string documentContentHash, string? caseName);
    Task UpdateToParserFailedAsync(Guid sourceUuid, Exception exception);
    Task UpdateToSentToIngesterAsync(Guid sourceUuid);

    void LogFinalStatistics();
}

internal class Tracker : ITracker
{
    private readonly IFileSystem fileSystem;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<Tracker> logger;
    private readonly List<string> skippedCsvLineIdentifiers = [];
    private readonly List<string> csvParseErrors = [];

    public Tracker(IOptions<BacklogParserOptions> backlogParserOptions, IFileSystem fileSystem,
        TimeProvider timeProvider, ILogger<Tracker> logger)
    {
        this.fileSystem = fileSystem;
        this.timeProvider = timeProvider;
        this.logger = logger;
        trackerFilePath = backlogParserOptions.Value.TrackerFilePath;

        if (fileSystem.File.Exists(trackerFilePath) &&
            !string.IsNullOrWhiteSpace(fileSystem.File.ReadAllText(trackerFilePath)))
        {
            previousRunTrackerLines = ReadTrackerFile();
        }
    }

    private readonly TrackerLine[] previousRunTrackerLines = [];
    private readonly Dictionary<Guid, TrackerLine> currentRunTrackerLines = new();
    private readonly string trackerFilePath;

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

    public bool IsAlreadySentToIngester(Guid sourceUuid)
    {
        var alreadySentToIngester = previousRunTrackerLines.Any(previousRunTrackerLine =>
            previousRunTrackerLine.SourceUuid == sourceUuid &&
            previousRunTrackerLine.TrackerStatus ==
            TrackerStatus.SentToIngester);

        return alreadySentToIngester;
    }

    public async Task StartTrackingAsync(Guid sourceUuid, CsvLine csvLine, string csvMetadataHash)
    {
        var trackerLine = new TrackerLine
        {
            SourceUuid = sourceUuid,
            ParserRunId = CurrentParserRunId,
            TrackerStatus = TrackerStatus.Started,
            TrackerLineLastUpdated = timeProvider.GetUtcNow(),
            CsvMetadataHash = csvMetadataHash,
            FileExtension = csvLine.Extension,
            OriginalFileName = csvLine.FilePath,
            Court = csvLine.Court

        };
        currentRunTrackerLines.Add(trackerLine.SourceUuid, trackerLine);

        await UpdateTrackerFileAsync();
    }

    public async Task UpdateToParsedAsync(Guid sourceUuid, string treReference, string? ncn, string documentContentHash, string? caseName)
    {
        var trackerLine = currentRunTrackerLines[sourceUuid];

        trackerLine.TrackerStatus = TrackerStatus.Parsed;
        trackerLine.TreReference = treReference;
        trackerLine.Ncn = ncn;
        trackerLine.DocumentContentHash = documentContentHash;
        trackerLine.TrackerLineLastUpdated = timeProvider.GetUtcNow();
        trackerLine.CaseName = caseName;

        await UpdateTrackerFileAsync();
    }

    public async Task UpdateToParserFailedAsync(Guid sourceUuid, Exception exception)
    {
        var trackerLine = currentRunTrackerLines[sourceUuid];
        
        trackerLine.TrackerStatus = TrackerStatus.ParserFailed;
        trackerLine.ErrorMessage = exception.Message;
        trackerLine.TrackerLineLastUpdated = timeProvider.GetUtcNow();

        await UpdateTrackerFileAsync();
    }

    public async Task UpdateToSentToIngesterAsync(Guid sourceUuid)
    {
        var trackerLine = currentRunTrackerLines[sourceUuid];
        
        trackerLine.TrackerStatus = TrackerStatus.SentToIngester;
        trackerLine.TrackerLineLastUpdated = timeProvider.GetUtcNow();

        await UpdateTrackerFileAsync();
    }

    private TrackerLine[] ReadTrackerFile()
    {
        using var fileSystemStream = fileSystem.File.OpenRead(trackerFilePath);
        using var streamReader = new StreamReader(fileSystemStream);
        using var csv = new CsvReader(streamReader, CultureInfo.InvariantCulture);

        // Read the header first
        csv.Read();
        csv.ReadHeader();

        return csv.GetRecords<TrackerLine>().ToArray();
    }

    private async Task UpdateTrackerFileAsync()
    {
        var newFileContents = previousRunTrackerLines.Concat(currentRunTrackerLines.Values).ToArray();

        // We update by overwriting the file.
        // First write to a temp file (so we don't lose data if the operation fails)
        var tempLogFile = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), fileSystem.Path.GetRandomFileName());
        await using (var fileSystemStream = fileSystem.File.OpenWrite(tempLogFile))
        {
            await using var streamWriter = new StreamWriter(fileSystemStream);
            await using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            await csv.WriteRecordsAsync(newFileContents);
        }

        //Then overwrite the old log with the new temp log
        fileSystem.File.Copy(tempLogFile, trackerFilePath, true);
    }

    public void LogFinalStatistics()
    {
        var numSkippedCsvLines = skippedCsvLineIdentifiers.Count;
        var markedAsSkipIds = numSkippedCsvLines > 0
            ? StringJoinFirstFive(skippedCsvLineIdentifiers, ", ")
            : string.Empty;
        var sentToIngester = currentRunTrackerLines.Values.Where(t => t.TrackerStatus == TrackerStatus.SentToIngester).ToArray();
        var alreadyDoneLines = previousRunTrackerLines.Where(t => t.TrackerStatus == TrackerStatus.SentToIngester).ToArray();
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

        var failedToProcessLines = currentRunTrackerLines.Values.Where(t => t.TrackerStatus == TrackerStatus.ParserFailed).ToArray();
        
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
