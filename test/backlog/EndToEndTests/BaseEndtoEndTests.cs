#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

using Amazon.S3;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using Moq;

using test.Mocks;

using Xunit;

namespace test.backlog.EndToEndTests;

[Collection("EndToEnd")]
public abstract class BaseEndToEndTests : IDisposable
{
    protected readonly MockS3Client mockS3Client = new();
    protected readonly FakeTimeProvider fakeTimeProvider = new();
    private readonly ITestOutputHelper testOutputHelper;
    protected readonly MockLogger<BaseEndToEndTests> ConsolidatedLogger = new();

    protected BaseEndToEndTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;

        // Ensure environment is clean before running any tests
        CleanEnvironmentVariables();

        // Clear lingering overrides from previous tests (overrides are in a static context)
        Environment.SetEnvironmentVariable("IS_TEST", "true");
        Backlog.Program.DependencyInjectionOverrides.Clear();

        // Configure S3 client
        Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");
        Backlog.Program.DependencyInjectionOverrides.Add(service =>
        {
            service.RemoveAll<IAmazonS3>();
            service.AddScoped<IAmazonS3>(_ => mockS3Client.Object);
        });

        // Control time
        Backlog.Program.DependencyInjectionOverrides.Add(service =>
        {
            service.RemoveAll<TimeProvider>();
            service.AddScoped<TimeProvider>(_ => fakeTimeProvider);
        });

        // Mock logger
        var mockLoggerProvider = new Mock<ILoggerProvider>();
        mockLoggerProvider.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(() => ConsolidatedLogger.Object);

        Backlog.Program.DependencyInjectionOverrides.Add(service =>
        {
            service.AddSingleton<ILoggerProvider>(_ => mockLoggerProvider.Object);
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Backlog.Program.DependencyInjectionOverrides.Clear();
        CleanEnvironmentVariables();
    }

    private static void CleanEnvironmentVariables()
    {
        // Remove any .env files from test assembly folder - these are copied to the build folder via backlog
        var assemblyPathDir = Path.GetDirectoryName(TestContext.Current.TestAssembly!.AssemblyPath)!;
        var envFile = Path.Combine(assemblyPathDir, ".env");
        if (File.Exists(envFile))
            File.Delete(envFile);


        // Clean up environment variables
        Environment.SetEnvironmentVariable("BacklogParser__CourtMetadataFilePath", null);
        Environment.SetEnvironmentVariable("BacklogParser__DataFolderPath", null);
        Environment.SetEnvironmentVariable("BacklogParser__TrackerFilePath", null);
        Environment.SetEnvironmentVariable("BacklogParser__OutputFolderPath", null);

        Environment.SetEnvironmentVariable("IS_TEST", null);
        Environment.SetEnvironmentVariable("AWS_REGION", null);

    }

    protected static void SetPathEnvironmentVariables(string dataDir, string outputPath, string courtMetadataFilePath,
        string trackerPath)
    {
        Environment.SetEnvironmentVariable("BacklogParser__CourtMetadataFilePath", courtMetadataFilePath);
        Environment.SetEnvironmentVariable("BacklogParser__DataFolderPath", dataDir);
        Environment.SetEnvironmentVariable("BacklogParser__TrackerFilePath", trackerPath);
        Environment.SetEnvironmentVariable("BacklogParser__OutputFolderPath", outputPath);
    }


    protected static void AssertProgramExitedSuccessfully(int exitCode)
    {
        Assert.True(exitCode == 0, "Program should exit successfully");
    }

    protected static string GetUuidFromKey(string capturedKey)
    {
        var capturedUuid = capturedKey.Substring(0, capturedKey.Length - 7); // Remove .tar.gz
        return capturedUuid;
    }

    protected void PrintToOutputWithNumberedLines(string textToPrint)
    {
        PrintToOutputWithNumberedLines(textToPrint.Split(Environment.NewLine));
    }

    protected void PrintToOutputWithNumberedLines(IEnumerable<string> lines)
    {
        var currentLineNumber = 1;
        foreach (var line in lines)
        {
            var numberedLine = $"{currentLineNumber}: {line}";
            testOutputHelper.WriteLine(numberedLine);
            currentLineNumber++;
        }
    }
}
