#nullable enable
namespace Backlog.Options;

public sealed class BacklogParserOptions
{
    public static readonly string SectionName = "BacklogParser";
    public required string MetadataProvidedFilePathPrefix { get; set; }
    public required string TransferMetadataFilePathPrefix { get; set; }
    
    public required string CourtMetadataFilePath { get; set; }
    public required string DataFolderPath { get; set; }
    public required string OutputFolderPath { get; set; }
    public required string TrackerFilePath { get; set; }

    public string? BucketName { get; set; }

    public bool IsDryRun { get; set; }
    public uint? SingleIdToRun { get; set; }
    public bool AutoPublish { get; set; }
}
