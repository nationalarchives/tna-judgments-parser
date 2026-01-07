#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

using Backlog.Src;

using Xunit;

namespace test.backlog.EndToEndTests;

[Collection("EndToEnd")]
public abstract class BaseEndToEndTests : IDisposable
{
    protected readonly MockS3Client mockS3Client = new();
    protected readonly ITestOutputHelper TestOutputHelper;

    protected BaseEndToEndTests(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;

        // Ensure environment is clean before running any tests
        CleanEnvironmentVariables();
        
        // Configure S3 client
        Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");
        Bucket.Configure(mockS3Client.Object, MockS3Client.TestBucket);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
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
        Environment.SetEnvironmentVariable("COURT_METADATA_PATH", null);
        Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", null);
        Environment.SetEnvironmentVariable("TRACKER_PATH", null);
        Environment.SetEnvironmentVariable("OUTPUT_PATH", null);
        Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", null);
        Environment.SetEnvironmentVariable("AWS_REGION", null);

        Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", null);
        Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", null);
    }

    protected static void SetPathEnvironmentVariables(string dataDir, string? outputPath = null,
        string? courtMetadataPath = null, string? trackerPath = null, string? bulkNumbersPath = null)
    {
        outputPath ??= Path.Combine(dataDir, "output");
        courtMetadataPath ??= Path.Combine(dataDir, "court_metadata.csv");
        trackerPath ??= Path.Combine(dataDir, "uploaded-production.csv");
        bulkNumbersPath ??= Path.Combine(dataDir, "bulk_numbers.csv");

        Environment.SetEnvironmentVariable("COURT_METADATA_PATH", courtMetadataPath);
        Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", dataDir);
        Environment.SetEnvironmentVariable("TRACKER_PATH", trackerPath);
        Environment.SetEnvironmentVariable("OUTPUT_PATH", outputPath);
        Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", bulkNumbersPath);
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
            TestOutputHelper.WriteLine(numberedLine);
            currentLineNumber++;
        }
    }
}
