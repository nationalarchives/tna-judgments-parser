#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src;

/// <summary>
/// This is the main entry point to the bulk backlog parsing process
/// </summary>
internal class BacklogParserWorker(
    ILogger<BacklogParserWorker> logger,
    Api.Parser parser,
    BacklogFiles backlogFiles,
    Metadata csvMetadataReader,
    Tracker tracker)
{
    public int Run(bool isDryRun, uint? id, string pathToCourtMetadataFile, bool autoPublish, string pathToOutputFolder)
    {
        var lines = csvMetadataReader.Read(pathToCourtMetadataFile, out var csvParseErrors);
        if (lines.Count == 0)
        {
            logger.LogCritical("No valid records found in the metadata file");
            return 1;
        }

        if (id.HasValue)
        {
            // Process only the specific ID
            lines = lines.Where(line => line.id == id.Value.ToString()).ToList();
            if (!lines.Any())
            {
                logger.LogCritical("No valid records found for id {SuppliedId}", id.Value);
                return 1;
            }
        }

        var alreadyDoneLines = new List<Metadata.Line>();
        var markedAsSkipLines = new List<Metadata.Line>();
        var successfulNewLines = new List<Metadata.Line>();
        var failedToProcessLines = new List<(Metadata.Line line, Exception exception)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            try
            {
                if (line.Skip)
                {
                    logger.LogWarning("Skipping {LineId} because it was marked to skip in the csv", line.id);
                    markedAsSkipLines.Add(line);
                    continue;
                }

                if (tracker.WasDone(line))
                {
                    logger.LogWarning("Skipping {LineId} because it was previously processed", line.id);
                    alreadyDoneLines.Add(line);
                    continue;
                }

                logger.LogInformation("Processing file: {FilePath}", line.FilePath);
                var bundle = GenerateBundle(line, autoPublish);

                var bundleFileName = bundle.Uuid + ".tar.gz";
                var output = Path.Combine(pathToOutputFolder, bundleFileName);
                logger.LogInformation("  Writing to output: {Output}", output);
                File.WriteAllBytes(output, bundle.TarGz);

                if (isDryRun)
                {
                    logger.LogInformation("  This is a dry run - not uploading to S3");
                }
                else
                {
                    logger.LogInformation("  Uploading {BundleFileName} to S3", bundleFileName);
                    Bucket.UploadBundle(bundleFileName, bundle.TarGz).Wait();
                }

                tracker.MarkDone(line, bundle.Uuid);
                successfulNewLines.Add(line);

                logger.LogInformation("  success");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing line {LineId}:", line.id);
                failedToProcessLines.Add((line, ex));
            }
            finally
            {
                logger.LogInformation("{Percent}% done", 100 * (i + 1) / lines.Count);
            }
        }

        LogFinalStatistics(logger, markedAsSkipLines, alreadyDoneLines, successfulNewLines, lines,
            failedToProcessLines, csvParseErrors);

        if (failedToProcessLines.Count > 0 || csvParseErrors.Count > 0)
        {
            return 1;
        }

        return 0;
    }

    private static void LogFinalStatistics(ILogger logger, List<Metadata.Line> markedAsSkipLines,
        List<Metadata.Line> alreadyDoneLines,
        List<Metadata.Line> successfulNewLines, List<Metadata.Line> parsedLinesFromCsv,
        List<(Metadata.Line line, Exception exception)> failedToProcessLines,
        List<string> csvParseErrors)
    {
        var markedAsSkipIds = markedAsSkipLines.Any()
            ? $"[{string.Join(", ", markedAsSkipLines.Select(l => l.id))}]"
            : string.Empty;

        logger.LogInformation("""
                              ---------------------------
                              Successfully processed {SuccessfulLinesCount} of {CsvLinesCount} csv lines, of which:
                                - {NewLinesCount} lines were new
                                - {MarkedToSkipLineCount} lines were marked in the csv to skip {MarkedToSkipIds} 
                                - {AlreadyDoneLineCount} lines were skipped because they had been processed in a previous run
                              """,
            markedAsSkipLines.Count + alreadyDoneLines.Count + successfulNewLines.Count,
            parsedLinesFromCsv.Count + csvParseErrors.Count,
            successfulNewLines.Count,
            markedAsSkipLines.Count, markedAsSkipIds,
            alreadyDoneLines.Count
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

        if (failedToProcessLines.Count > 0)
        {
            var failedIdsGroupedByErrorMessage = failedToProcessLines
                .GroupBy(f =>
                    {
                        return f.exception.Message switch
                        {
                            _ when f.exception.Message.StartsWith("Could not find file") => "Could not find file",
                            _ when f.exception.Message.StartsWith("Couldn't find file with UUID") =>
                                "Couldn't find file with UUID",
                            _ when f.exception.Message.EndsWith("was not recognized as a valid DateTime.") =>
                                "String was not recognized as a valid DateTime",
                            _ => f.exception.Message
                        };
                    },
                    f => f.line.id);

            var groupedErrorDescriptions = failedIdsGroupedByErrorMessage.Select(groupOfErrors =>
            {
                var affectedIds = groupOfErrors.Count() <= 5
                    ? string.Join(", ", groupOfErrors)
                    : string.Join(", ", groupOfErrors.Take(5)) + "...";
                return
                    $"  - {groupOfErrors.Count()} lines failed with exception message \"{groupOfErrors.Key}\". Ids affected were: ({affectedIds})";
            });


            logger.LogError("""
                            ---------------------------
                            Failed to process {FailedLineCount} lines, of which:
                            {GroupedErrorDescriptions}
                            """,
                failedToProcessLines.Count, string.Join(Environment.NewLine, groupedErrorDescriptions));
        }

        if (failedToProcessLines.Count == 0 && csvParseErrors.Count == 0)
        {
            logger.LogInformation("No failed lines");
        }
    }

    private Api.Response CreateResponse(ExtendedMetadata meta, byte[] content)
    {
        var isPdf = meta.SourceFormat.ToLower() == "application/pdf";
        if (isPdf)
        {
            var metadata = new Api.Meta
            {
                DocumentType = "decision",
                Court = meta.Court?.Code,
                Date = meta.Date?.Date,
                Name = meta.Name
            };

            var stub = Stub.Make(meta);

            var response = new Api.Response { Xml = stub.Serialize(), Meta = metadata };
            return response;
        }
        else
        {
            var metadata = new Api.Meta
            {
                DocumentType = "decision",
                Court = meta.Court?.Code,
                Date = meta.Date?.Date,
                Name = meta.Name,
                JurisdictionShortNames = meta.Jurisdictions.Select(j => j.ShortName).ToList(),
                Extensions = new Api.Extensions
                {
                    SourceFormat = meta.SourceFormat,
                    CaseNumbers = meta.CaseNumbers,
                    Parties = meta.Parties,
                    Categories = meta.Categories,
                    WebArchivingLink = meta.WebArchivingLink
                }
            };

            var request = new Api.Request
            {
                Meta = metadata,
                Hint = Api.Hint.UKUT,
                Content = content
            };

            var response = parser.Parse(request);
            return response;
        }
    }

    private Bundle GenerateBundle(Metadata.Line line, bool autoPublish)
    {
        var meta = Metadata.MakeMetadata(line);

        var uuid = !string.IsNullOrWhiteSpace(line.Uuid)
            ? line.Uuid
            : backlogFiles.FindUuidInTransferMetadata(line.FilePath);
        var content = backlogFiles.ReadFile(uuid);

        var response = CreateResponse(meta, content);

        var source = new Bundle.Source
        {
            Filename = Path.GetFileName(line.FilePath),
            Content = content,
            MimeType = meta.SourceFormat
        };

        logger.LogInformation("Creating bundle with source: {SourceFilename}", source.Filename);
        logger.LogInformation("Creating bundle with content: {ContentLength} bytes", source.Content.Length);
        return Bundle.Make(source, response, autoPublish);
    }
}
