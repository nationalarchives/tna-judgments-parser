#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

using DotNetEnv;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src;

public class Program
{
    private static readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = "Use the dry run flag to run the parser without sending to AWS"
    };

    private static readonly Option<uint?> FileIdOption = new("--id")
    {
        Description =
            "The id of a single file in the batch to parse. If not supplied then all records will be processed"
    };

    public static int Main(string[] args)
    {
        try
        {
            RootCommand rootCommand = new("Backlog parser used to bulk parse imported files")
            {
                Options = { DryRunOption, FileIdOption }
            };
            rootCommand.SetAction(parseResult => RunBacklogParser(
                parseResult.GetValue(DryRunOption),
                parseResult.GetValue(FileIdOption))
            );

            var parseResult = rootCommand.Parse(args);
            if (parseResult.Errors.Count > 0)
            {
                foreach (var parseError in parseResult.Errors)
                {
                    Console.Error.WriteLine(parseError.Message);
                }

                return 1;
            }

            return parseResult.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fatal error:");
            Console.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static int RunBacklogParser(bool isDryRun, uint? id)
    {
        var autoPublish = true;

        Env.Load(); // required for bucket name

        var judgmentsFilePath = Environment.GetEnvironmentVariable("JUDGMENTS_FILE_PATH") ?? "";
        var hmctsFilePath = Environment.GetEnvironmentVariable("HMCTS_FILES_PATH") ?? "";
        var pathToCourtMetadataFile = Environment.GetEnvironmentVariable("COURT_METADATA_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "court_metadata.csv");
        var pathToDataFolder = Environment.GetEnvironmentVariable("DATA_FOLDER_PATH") ?? AppDomain.CurrentDomain.BaseDirectory;
        var pathToOutputFolder = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? AppDomain.CurrentDomain.BaseDirectory;
        Directory.CreateDirectory(pathToOutputFolder);
        var trackerPath = Environment.GetEnvironmentVariable("TRACKER_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploaded-production.csv");

        var serviceProvider = ConfigureDependencyInjection(pathToDataFolder, trackerPath, judgmentsFilePath, hmctsFilePath);

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var csvMetadataReader = serviceProvider.GetRequiredService<Metadata>();
            var helper = serviceProvider.GetRequiredService<Helper>();
            var tracker =  serviceProvider.GetRequiredService<Tracker>();

            logger.LogInformation("Using Parser version: {ParserVersion}",
                UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.GetParserVersion());
            logger.LogInformation("Using data folder: {PathToDataFolder}", pathToDataFolder);
            logger.LogInformation("Using court metadata from: {PathToCourtMetadataFile}", pathToCourtMetadataFile);

            var lines = csvMetadataReader.Read(pathToCourtMetadataFile, out List<string> csvParseErrors);
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
                    var bundle = helper.GenerateBundle(line, autoPublish);

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

            LogFinalStatistics(logger, markedAsSkipLines, alreadyDoneLines, successfulNewLines, lines, failedToProcessLines, csvParseErrors);

            if (failedToProcessLines.Count > 0 || csvParseErrors.Count > 0)
            {
                return 1;
            }

            return 0;
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Backlog Parser fell over");
            return 1;
        }
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

    private static ServiceProvider ConfigureDependencyInjection(string pathToDataFolder, string trackerPath,
        string judgmentsFilePath, string hmctsFilePath)
    {
        var services = new ServiceCollection();

        services.AddLogging(loggingBuilder =>
        {
            var logFilePath = Path.Combine(pathToDataFolder, $"log_{DateTime.Now:yy-MM-dd_HH-mm}.txt");
            loggingBuilder.AddConsole()
                          .AddFile(logFilePath,
                              outputTemplate:
                              "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}");
        });
        services
            .AddSingleton<UK.Gov.Legislation.Judgments.AkomaNtoso.IValidator,
                UK.Gov.Legislation.Judgments.AkomaNtoso.Validator>();
        services.AddSingleton<Parser>();
        services.AddSingleton<Helper>();
        services.AddSingleton<Metadata>();
        services.AddSingleton<BacklogFiles>(serviceProvider => new BacklogFiles(serviceProvider.GetRequiredService<ILogger<BacklogFiles>>(), pathToDataFolder,
            judgmentsFilePath, hmctsFilePath));
        services.AddSingleton<Tracker>(_ => new Tracker(trackerPath));

        return services.BuildServiceProvider();
    }
}
