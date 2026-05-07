#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;

using Amazon.S3;

using Backlog.Csv;
using Backlog.Options;
using Backlog.Utilities;

using DotNetEnv.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
            RunBacklogParser(validatedCommandInputs.GetValue(DryRunOption),
                validatedCommandInputs.GetValue(FileIdOption),
                validatedCommandInputs.GetValue(AutoPublishOption))
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

    private static readonly Option<bool> AutoPublishOption = new("--auto-publish")
    {
        Description = "Use the auto-publish flag to automatically publish uploaded judgments"
    };

    private static readonly Option<uint?> FileIdOption = new("--id")
    {
        Description =
            "The id of a single file in the batch to parse. If not supplied then all records will be processed"
    };


    private static readonly RootCommand RootCommand = new("Backlog parser used to bulk parse imported files")
    {
        Options =
        {
            DryRunOption,
            AutoPublishOption,
            FileIdOption
        },
        Subcommands = { SplitFilesByExtension }
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

    private static int RunBacklogParser(bool isDryRun, uint? id, bool autoPublish)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddDotNetEnv();

        builder.Services.AddOptions<BacklogParserOptions>()
               .Bind(builder.Configuration.GetSection(BacklogParserOptions.SectionName))
               .Configure(options =>
               {
                   options.IsDryRun = isDryRun;
                   options.SingleIdToRun = id;
                   options.AutoPublish = autoPublish;
               });

        builder.Services.AddLogging(loggingBuilder =>
        {
            var serviceProvider = loggingBuilder.Services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptions<BacklogParserOptions>>();
            var logFilePath = Path.Combine(options.Value.DataFolderPath, $"log_{DateTime.Now:yy-MM-dd_HH-mm}.txt");
            loggingBuilder.AddConsole()
                          .AddFile(logFilePath,
                              outputTemplate:
                              "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:w4}] {Message:lj}{NewLine}{Exception}");
        });

        ConfigureDependencyInjection(builder.Services);

        var appHost = builder.Build();

        var serviceProvider = appHost.Services;
        var backlogParserOptions = serviceProvider.GetRequiredService<IOptions<BacklogParserOptions>>().Value;

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            logger.LogInformation("Using Parser version: {ParserVersion}",
                UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata.GetParserVersion());
            logger.LogInformation("Using data folder: {PathToDataFolder}", backlogParserOptions.DataFolderPath);
            logger.LogInformation("Using court metadata from: {PathToCourtMetadataFile}",
                backlogParserOptions.CourtMetadataFilePath);

            var backlogParserWorker = serviceProvider.GetRequiredService<BacklogParserWorker>();
            Directory.CreateDirectory(backlogParserOptions.OutputFolderPath);

            return backlogParserWorker.Run();
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Backlog Parser fell over");
            return 1;
        }
    }

    private static List<(Type serviceType, object instance, bool replace)> _dependencyInjectionOverrides = [];

    /// <summary>
    ///     Allow services like S3 to be mocked, but only during tests
    /// </summary>
    internal static List<(Type serviceType, object instance, bool replace)> DependencyInjectionOverrides =>
        IsTest()
            ? _dependencyInjectionOverrides
            : throw new InvalidOperationException("Cannot use dependency injection overrides in production");

    internal static void ConfigureDependencyInjection(IServiceCollection services)
    {
        services.AddSingleton<UK.Gov.Legislation.Judgments.AkomaNtoso.IValidator, UK.Gov.Legislation.Judgments.AkomaNtoso.Validator>();
        services.AddSingleton<Parser>();
        services.AddSingleton<BacklogParserWorker>();
        services.AddSingleton<CsvMetadataReader>();
        services.AddSingleton<BacklogFiles>();
        services.AddSingleton<Tracker>();
        services.AddSingleton<IAmazonS3, AmazonS3Client>();
        services.AddSingleton<Bucket>();
        services.AddSingleton<MetadataTransformer>();
        services.AddSingleton<TimeProvider>(_ => TimeProvider.System);

        if (IsTest())
        {
            OverrideDependencyInjection(services);
        }
    }

    private static void OverrideDependencyInjection(IServiceCollection services)
    {
        foreach (var (serviceType, instance, replace) in DependencyInjectionOverrides)
        {
            if (replace)
            {
                services.RemoveAll(serviceType);
            }

            services.AddSingleton(serviceType, instance);
        }
    }

    /// <summary>
    /// Are we running in a test environment (is the IS_TEST envvar true?)
    /// </summary>
    private static bool IsTest()
    {
        return bool.TryParse(Environment.GetEnvironmentVariable("IS_TEST"), out var isTest) && isTest;
    }
}
