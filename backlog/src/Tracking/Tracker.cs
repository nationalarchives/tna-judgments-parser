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

using Microsoft.Extensions.Options;

namespace Backlog.Tracking;

internal interface ITracker
{
    bool IsAlreadySentToIngester(Guid sourceUuid);
    Task StartTrackingAsync(Guid sourceUuid, CsvLine csvLine, Guid parserRunId, string csvMetadataHash);

    Task UpdateToParsedAsync(Guid sourceUuid, string treReference, string? ncn, string documentContentHash, string? caseName);

    Task UpdateToParserFailedAsync(Guid sourceUuid, Exception exception);
    Task UpdateToSentToIngesterAsync(Guid sourceUuid);
}

internal class Tracker : ITracker
{
    private readonly IFileSystem fileSystem;
    private readonly TimeProvider timeProvider;

    public Tracker(IOptions<BacklogParserOptions> backlogParserOptions, IFileSystem fileSystem,
        TimeProvider timeProvider)
    {
        this.fileSystem = fileSystem;
        this.timeProvider = timeProvider;
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

    public bool IsAlreadySentToIngester(Guid sourceUuid)
    {
        var alreadySentToIngester = previousRunTrackerLines.Any(previousRunTrackerLine =>
            previousRunTrackerLine.SourceUuid == sourceUuid &&
            previousRunTrackerLine.TrackerStatus ==
            TrackerStatus.SentToIngester);

        return alreadySentToIngester;
    }

    public async Task StartTrackingAsync(Guid sourceUuid, CsvLine csvLine, Guid parserRunId, string csvMetadataHash)
    {
        var trackerLine = new TrackerLine
        {
            SourceUuid = sourceUuid,
            CsvLine = csvLine,
            ParserRunId = parserRunId,
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
        currentRunTrackerLines[sourceUuid].TrackerStatus = TrackerStatus.ParserFailed;
        currentRunTrackerLines[sourceUuid].ErrorMessage = exception.Message;
        currentRunTrackerLines[sourceUuid].TrackerLineLastUpdated = timeProvider.GetUtcNow();
        await UpdateTrackerFileAsync();
    }

    public async Task UpdateToSentToIngesterAsync(Guid sourceUuid)
    {
        currentRunTrackerLines[sourceUuid].TrackerStatus = TrackerStatus.SentToIngester;
        currentRunTrackerLines[sourceUuid].TrackerLineLastUpdated = timeProvider.GetUtcNow();
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

        // We update by overwriting the file
        await using var fileSystemStream = fileSystem.File.OpenWrite(trackerFilePath);
        await using var streamWriter = new StreamWriter(fileSystemStream);
        await using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(newFileContents);
    }
}
