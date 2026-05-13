#nullable enable

using System.IO.Abstractions.TestingHelpers;
using System.Linq;

using Backlog.Options;

using Microsoft.Extensions.Options;

using Xunit;

namespace test.backlog;

public class TestBacklogParserOptionsValidation
{
    private readonly BacklogParserOptionsValidation validator;

    public TestBacklogParserOptionsValidation()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(@"c:\my-data-dir\");
        fileSystem.AddFile(@"c:\my-data-dir\court-metadata.csv", new MockFileData(""));
        validator = new BacklogParserOptionsValidation(fileSystem);
    }

    private static void AssertSingleFailure(ValidateOptionsResult result, string expectedFailureMessage)
    {
        Assert.True(result.Failed);
        var failureMessage = Assert.Single(result.Failures);
        Assert.Equal(expectedFailureMessage, failureMessage);
    }

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: true).Value;

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.Null(result.Failures);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenNotDryRunAndBucketNameIsNullOrEmpty_ReturnsFailed(string? bucketName)
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: false, bucketName: bucketName).Value;

        var result = validator.Validate(null, options);

        AssertSingleFailure(result, "BucketName, IsDryRun: BucketName must be set when IsDryRun is false");
    }

    [Fact]
    public void Validate_WhenNotDryRunAndBucketNameIsSet_ReturnsSuccess()
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: false, bucketName: "my-bucket").Value;

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.Null(result.Failures);
    }

    [Theory]
    [InlineData("", "CourtMetadataFilePath: The CourtMetadataFilePath field is required.")]
    [InlineData(" ", "CourtMetadataFilePath: The CourtMetadataFilePath field is required.")]
    [InlineData("/nonexistent/file.csv", """CourtMetadataFilePath: CourtMetadataFilePath "/nonexistent/file.csv" does not exist""")]
    public void Validate_WhenCourtMetadataFilePathIsInvalid_ReturnsFailed(string courtMetadataFilePath, string expectedFailureMessage)
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: true, courtMetadataFilePath: courtMetadataFilePath).Value;

        var result = validator.Validate(null, options);

        AssertSingleFailure(result, expectedFailureMessage);
    }

    [Theory]
    [InlineData("", "DataFolderPath: The DataFolderPath field is required.")]
    [InlineData(" ", "DataFolderPath: The DataFolderPath field is required.")]
    [InlineData("/nonexistent/folder", """DataFolderPath: DataFolderPath "/nonexistent/folder" does not exist""")]
    public void Validate_WhenDataFolderPathIsInvalid_ReturnsFailed(string dataFolderPath, string expectedFailureMessage)
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: true, dataFolderPath: dataFolderPath).Value;

        var result = validator.Validate(null, options);

        AssertSingleFailure(result, expectedFailureMessage);
    }

    [Theory]
    [InlineData("" )]
    [InlineData(" ")]
    public void Validate_WhenTrackerFilePathIsMissing_ReturnsFailed(string trackerFilePath)
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: true, trackerFilePath:trackerFilePath).Value;

        var result = validator.Validate(null, options);

        AssertSingleFailure(result, "TrackerFilePath: The TrackerFilePath field is required.");
    }
    
    [Theory]
    [InlineData("" )]
    [InlineData(" ")]
    public void Validate_WhenOutputFolderPathIsMissing_ReturnsFailed(string outputFolderPath)
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: true, outputFolderPath:outputFolderPath).Value;

        var result = validator.Validate(null, options);

        AssertSingleFailure(result, "OutputFolderPath: The OutputFolderPath field is required.");
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReportsAllFailures()
    {
        var options = BacklogParserOptionsHelper.Create(
            isDryRun: false,
            bucketName: null,
            trackerFilePath: "",
            courtMetadataFilePath: "/nonexistent/file.csv",
            dataFolderPath: "/nonexistent/folder").Value;

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(
            [
                "TrackerFilePath: The TrackerFilePath field is required.",
                "BucketName, IsDryRun: BucketName must be set when IsDryRun is false",
                "CourtMetadataFilePath: CourtMetadataFilePath \"/nonexistent/file.csv\" does not exist",
                "DataFolderPath: DataFolderPath \"/nonexistent/folder\" does not exist"
            ],
            result.Failures
        );
    }
}
