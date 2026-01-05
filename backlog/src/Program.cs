#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

using DotNetEnv;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src;

public static class Program
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
        
        Helper helper = new(new Parser(Logging.Factory.CreateLogger<Parser>(), new UK.Gov.Legislation.Judgments.AkomaNtoso.Validator()))
        {
            PathToCourtMetadataFile = pathToCourtMetadataFile,
            PathToDataFolder = pathToDataFolder
        };
        var trackerPath = Environment.GetEnvironmentVariable("TRACKER_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploaded-production.csv");
        var tracker = new Tracker(trackerPath);

        Log($"Using data folder: {pathToDataFolder}");
        Log($"Using court metadata from: {pathToCourtMetadataFile}");

        List<Metadata.Line> lines;
        if (id.HasValue)
        {
            // Process only the specific ID
            lines = helper.FindLines(id.Value);
            if (!lines.Any())
            {
                Log($"No records found for id {id.Value}");
                return 1;
            }
        }
        else
        {
            // Process all lines from the document
            lines = Metadata.Read(helper.PathToCourtMetadataFile);
            if (!lines.Any())
            {
                Log("No records found in the metadata file");
                return 1;
            }
        }

        var alreadyDoneLines = new List<Metadata.Line>();
        var successfulLines = new List<Metadata.Line>();
        var failedLines = new List<(Metadata.Line line, Exception exception)>();

        foreach (var line in lines)
        {
            if (tracker.WasDone(line))
            {
                Log("skipping " + line.id);
                alreadyDoneLines.Add(line);
                continue;
            }

            try
            {
                Log($"Processing file: {line.FilePath}");
                var bundle = helper.GenerateBundle(line, judgmentsFilePath, hmctsFilePath, autoPublish);

                var output = Path.Combine(pathToOutputFolder, bundle.Uuid + ".tar.gz");
                Log($"  Writing to output: {output}");
                File.WriteAllBytes(output, bundle.TarGz);


                if (isDryRun)
                {
                    Log("  This is a dry run - not uploading to S3");
                }
                else
                {
                    Log("  Uploading to S3");
                    Bucket.UploadBundle(bundle.Uuid + ".tar.gz", bundle.TarGz).Wait();
                }

                tracker.MarkDone(line, bundle.Uuid);
                successfulLines.Add(line);

                Log("  success");
            }
            catch (Exception ex)
            {
                Log($"Error processing line {line.id}:");
                Log(ex.ToString());
                failedLines.Add((line, ex));
            }
        }

        Log("---------------------------", false);

        Log($"Processed {alreadyDoneLines.Count + successfulLines.Count + failedLines.Count} of {lines.Count} lines");
        Log($"Successfully processed {successfulLines.Count} lines");
        Log($"Skipped {alreadyDoneLines.Count} lines that had previously been processed");

        if (failedLines.Count > 0)
        {
            Log($"Failed to process {failedLines.Count} lines");

            var failedIdsGroupedByErrorMessage = failedLines.GroupBy(f => f.exception.Message, f => f.line.id);
            foreach (var thing in failedIdsGroupedByErrorMessage)
            {
                Log($"  {thing.Count()} lines failed with exception message {thing.Key}");
                Log($"    Ids affected were: ({string.Join(" ,", thing)})");
            }

            return 1;
        }

        Log("No failed lines");

        return 0;
    }

    private static void Log(string message, bool includeTimestamp = true)
    {
        if (includeTimestamp)
        {
            Console.WriteLine("{0:G}: {1}", DateTime.Now, message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}
