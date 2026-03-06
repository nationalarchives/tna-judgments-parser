#nullable enable

using System;
using System.CommandLine;
using System.IO;

using Backlog.Csv;

using Backlog.Utilities;

using DotNetEnv;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src;

public class Program
{
    static Program()
    {
        SplitFilesByExtension.SetAction(validatedCommandInputs =>
        {
            var originalPath = validatedCommandInputs.GetValue(SplitFilesOriginalPathArgument)!; // Argument is required
            var destinationPath = validatedCommandInputs.GetValue(SplitFilesDestinationPathOption)!; // Option is required

            var services = new ServiceCollection();
            services.AddLogging(loggingBuilder => { loggingBuilder.AddConsole(); });
            services.AddSingleton<SplitTdrFilesByExtensionWorker>();

            var splitFilesWorker = services.BuildServiceProvider().GetRequiredService<SplitTdrFilesByExtensionWorker>();

            return splitFilesWorker.Run(originalPath, destinationPath);
        });

        RootCommand.SetAction(validatedCommandInputs =>
            RunBacklogParser(validatedCommandInputs.GetValue(DryRunOption), validatedCommandInputs.GetValue(FileIdOption))
        );
    }

    #region SplitCommand

    private static readonly Argument<DirectoryInfo> SplitFilesOriginalPathArgument = new("originalPath")
    {
        Arity = ArgumentArity.ExactlyOne,
        Description =
            "The original path containing TDR folders to split. This can be a single TDR folder or a folder containing multiple TDR folders"
    };

    private static readonly Option<DirectoryInfo> SplitFilesDestinationPathOption = new("--destination")
    {
        Arity = ArgumentArity.ExactlyOne,
        Description =
            "The destination path for the split files. A new folder will be created here with the time of the run which contains the split file results",
        Required = true
    };

    private static readonly Command SplitFilesByExtension = new("split",
        "Copies files in TDR folders into destination folders named by extension. Useful for collating files for file conversion")
    {
        Arguments = { SplitFilesOriginalPathArgument }, Options = { SplitFilesDestinationPathOption }
    };

    #endregion

    #region RootCommand

    private static readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = "Use the dry run flag to run the parser without sending to AWS"
    };

    private static readonly Option<uint?> FileIdOption = new("--id")
    {
        Description =
            "The id of a single file in the batch to parse. If not supplied then all records will be processed"
    };


    private static readonly RootCommand RootCommand = new("Backlog parser used to bulk parse imported files")
    {
        Options = { DryRunOption, FileIdOption }, Subcommands = { SplitFilesByExtension }
    };

    #endregion
    
    /// <summary>
    /// This is the entry point method that is triggered by running the backlog parser on commandline
    /// </summary>
    /// <param name="args">The arguments specified on the commandline</param>
    /// <returns></returns>
    public static int Main(params string[] args)
    {
        try
        {
            var parseResult = RootCommand.Parse(args);
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
            logger.LogInformation("Using Parser version: {ParserVersion}",
                UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.GetParserVersion());
            logger.LogInformation("Using data folder: {PathToDataFolder}", pathToDataFolder);
            logger.LogInformation("Using court metadata from: {PathToCourtMetadataFile}", pathToCourtMetadataFile);

            var backlogParserWorker = serviceProvider.GetRequiredService<BacklogParserWorker>();
            
            return backlogParserWorker.Run(isDryRun, id, pathToCourtMetadataFile, autoPublish, pathToOutputFolder);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Backlog Parser fell over");
            return 1;
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
        services.AddSingleton<BacklogParserWorker>();
        services.AddSingleton<CsvMetadataReader>();
        services.AddSingleton<BacklogFiles>(serviceProvider => new BacklogFiles(serviceProvider.GetRequiredService<ILogger<BacklogFiles>>(), pathToDataFolder,
            judgmentsFilePath, hmctsFilePath));
        services.AddSingleton<Tracker>(_ => new Tracker(trackerPath));

        return services.BuildServiceProvider();
    }
}
