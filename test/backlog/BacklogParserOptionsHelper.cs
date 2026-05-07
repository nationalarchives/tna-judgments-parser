using Backlog.Options;

using Microsoft.Extensions.Options;

namespace test.backlog;

public static class BacklogParserOptionsHelper
{
    public static IOptions<BacklogParserOptions> Create(
        string dataFolderPath = @"c:\my-data-dir\",
        string metadataProvidedFilePathPrefix = "",
        string transferMetadataFilePathPrefix = "",
        string courtMetadataFilePath = @"c:\my-data-dir\court-metadata.csv",
        string outputFolderPath = @"c:\my-data-dir\output",
        string trackerFilePath = @"c:\my-data-dir\tracker.csv",
        bool autoPublish = false
    )
    {
        return Options.Create(new BacklogParserOptions
        {
            DataFolderPath = dataFolderPath,
            MetadataProvidedFilePathPrefix = metadataProvidedFilePathPrefix,
            TransferMetadataFilePathPrefix = transferMetadataFilePathPrefix,
            CourtMetadataFilePath = courtMetadataFilePath,
            OutputFolderPath = outputFolderPath,
            TrackerFilePath = trackerFilePath,
            AutoPublish = autoPublish
        });
    }
}
