#nullable enable

using Backlog.Options;

using Microsoft.Extensions.Options;

namespace test.backlog;

public static class BacklogParserOptionsHelper
{
    public static IOptions<BacklogParserOptions> Create(
        string dataFolderPath = @"c:\my-data-dir\",
        string courtMetadataFilePath = @"c:\my-data-dir\court-metadata.csv",
        string outputFolderPath = @"c:\my-data-dir\output",
        string trackerFilePath = ":memory:", // Default to in-memory sqlite db for test independence
        bool autoPublish = false,
        bool isDryRun = false,
        string? bucketName = null
    )
    {
        return Options.Create(new BacklogParserOptions
        {
            DataFolderPath = dataFolderPath,
            CourtMetadataFilePath = courtMetadataFilePath,
            OutputFolderPath = outputFolderPath,
            TrackerFilePath = trackerFilePath,
            AutoPublish = autoPublish,
            IsDryRun = isDryRun,
            BucketName = bucketName
        });
    }
}
