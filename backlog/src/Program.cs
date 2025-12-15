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

        Helper helper = new(new Parser(Logging.Factory.CreateLogger<Parser>(), new UK.Gov.Legislation.Judgments.AkomaNtoso.Validator()))
        {
            PathToCourtMetadataFile =
                Environment.GetEnvironmentVariable("COURT_METADATA_PATH") ??
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "court_metadata.csv"),
            PathToDataFolder = Environment.GetEnvironmentVariable("DATA_FOLDER_PATH") ??
                               AppDomain.CurrentDomain.BaseDirectory
        };
        var trackerPath = Environment.GetEnvironmentVariable("TRACKER_PATH") ??
                          Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploaded-production.csv");
        var tracker = new Tracker(trackerPath);

        List<Metadata.Line> lines;
        if (id.HasValue)
        {
            // Process only the specific ID
            lines = helper.FindLines(id.Value);
            if (!lines.Any())
            {
                Console.WriteLine($"No records found for id {id.Value}");
                return 1;
            }
        }
        else
        {
            // Process all lines from the document
            lines = Metadata.Read(helper.PathToCourtMetadataFile);
            if (!lines.Any())
            {
                Console.WriteLine("No records found in the metadata file");
                return 1;
            }
        }

        foreach (var line in lines)
        {
            if (tracker.WasDone(line))
            {
                Console.WriteLine("skipping " + line.id);
                continue;
            }

            try
            {
                Console.WriteLine($"Processing file: {line.FilePath}");
                Console.WriteLine($"Using court metadata from: {helper.PathToCourtMetadataFile}");
                Console.WriteLine($"Using data folder: {helper.PathToDataFolder}");

                var bundle = helper.GenerateBundle(line, judgmentsFilePath, hmctsFilePath, autoPublish);

                var outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ??
                                 AppDomain.CurrentDomain.BaseDirectory;
                Directory.CreateDirectory(outputPath);
                var output = Path.Combine(outputPath, bundle.Uuid + ".tar.gz");
                File.WriteAllBytes(output, bundle.TarGz);

                Console.WriteLine(bundle.Uuid + ".tar.gz");
                Console.WriteLine(DateTime.Now);

                if (isDryRun)
                {
                    Console.WriteLine("This is a dry run - not uploading to S3");
                }
                else
                {
                    Console.WriteLine("Uploading to S3");
                    Bucket.UploadBundle(bundle.Uuid + ".tar.gz", bundle.TarGz).Wait();
                }

                tracker.MarkDone(line, bundle.Uuid);

                Console.WriteLine("success");
                Console.WriteLine(bundle.Uuid + ".tar.gz");
                Console.WriteLine(DateTime.Now);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing line {line.id}:");
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        return 0;
    }
}
