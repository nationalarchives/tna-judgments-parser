#nullable enable

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;

using Backlog.Options;
using Backlog.Tracking;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace test.backlog;

public class TestTracker
{
    private const string TrackerFilePath = "/tracker.csv";

    private const string TrackerCsvHeader =
        "Court,FileExtension,SourceUuid,ParserRunId,TrackerStatus,TreReference,Ncn,CaseName,OriginalFileName,DocumentContentHash,CsvMetadataHash,ErrorMessage,TrackerLineLastUpdated";

    private readonly FakeTimeProvider fakeTimeProvider = new();
    private readonly MockFileSystem mockFileSystem = new();

    private readonly IOptions<BacklogParserOptions> options =
        BacklogParserOptionsHelper.Create(trackerFilePath: TrackerFilePath);

    private Tracker CreateTracker(params string[] trackerDataLines)
    {
        var fullTrackerContents = string.Join(Environment.NewLine, trackerDataLines.Prepend(TrackerCsvHeader));

        mockFileSystem.AddFile(TrackerFilePath, new MockFileData(fullTrackerContents));
        return new Tracker(options, mockFileSystem, fakeTimeProvider);
    }

    [Fact]
    public void CurrentParserRunId_IsDifferentForEveryRun()
    {
        // Simulate new runs by creating a new tracker instance
        var tracker = CreateTracker();
        var tracker2 = CreateTracker();
        
        Assert.NotEqual(tracker.CurrentParserRunId, tracker2.CurrentParserRunId);
    }

    [Fact]
    public void IsAlreadySentToIngester_WhenNoTrackerFileExists_ReturnsFalse()
    {
        mockFileSystem.RemoveFile(TrackerFilePath);
        var tracker = CreateTracker();
        Assert.False(tracker.IsAlreadySentToIngester(Guid.NewGuid()));
    }

    [Fact]
    public void IsAlreadySentToIngester_WhenTrackerFileIsEmpty_ReturnsFalse()
    {
        mockFileSystem.AddEmptyFile(TrackerFilePath);
        var tracker = CreateTracker();
        Assert.False(tracker.IsAlreadySentToIngester(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(TrackerStatus.Started, false)]
    [InlineData(TrackerStatus.Parsed, false)]
    [InlineData(TrackerStatus.ParserFailed, false)]
    [InlineData(TrackerStatus.IngesterFailed, false)]
    [InlineData(TrackerStatus.SentToIngester, true)]
    public void IsAlreadySentToIngester_WhenUuidWasSentInPreviousRun_Returns(TrackerStatus previousTrackerStatus,
        bool expectedResult)
    {
        var sourceUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tracker = CreateTracker(
            $"UKSC,docx,{sourceUuid},00000000-0000-0000-0000-000000000099,{previousTrackerStatus},ref1,ncn1,V v V,word.docx,hash1,metahash1,,2025-06-15 10:30:00.000");

        Assert.Equal(expectedResult, tracker.IsAlreadySentToIngester(sourceUuid));
    }

    [Fact]
    public void IsAlreadySentToIngester_MultiplePreviousRunsWithOneSuccess_ReturnsTrue()
    {
        var sourceUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tracker = CreateTracker(
            $"UKSC,docx,{sourceUuid},10000000-0000-0000-0000-000000000099,ParserFailed,ref1,ncn1,V v V,word.docx,hash1,metahash1,,2025-06-14 10:30:00.000",
            $"UKSC,docx,{sourceUuid},20000000-0000-0000-0000-000000000099,SentToIngester,ref1,ncn1,V v V,word.docx,hash1,metahash1,,2025-06-15 10:30:00.000"
        );

        Assert.True(tracker.IsAlreadySentToIngester(sourceUuid));
    }

    [Fact]
    public void IsAlreadySentToIngester_WhenDifferentUuidWasSent_ReturnsFalse()
    {
        var tracker = CreateTracker(
            $"UKSC,docx,99999999-9999-9999-9999-999999999999,00000000-0000-0000-0000-000000000099,{TrackerStatus.SentToIngester},ref1,ncn1,,word.docx,hash1,metahash1,,2025-06-15 10:30:00.000");

        Assert.False(tracker.IsAlreadySentToIngester(Guid.Parse("00000000-0000-0000-0000-000000000001")));
    }

    [Fact]
    public async Task StartTrackingAsync_WritesStartedStatusWithUuidAndRunIdToFile()
    {
        const string sourceUuid = "00000000-0000-0000-0000-000000000001";
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero));
        var tracker = CreateTracker();

        await tracker.StartTrackingAsync(Guid.Parse(sourceUuid), CsvMetadataLineHelper.DummyLine, "my-metadata-hash");

        var trackerLines =
            await mockFileSystem.File.ReadAllLinesAsync(TrackerFilePath, TestContext.Current.CancellationToken);
        Assert.Equal([
            TrackerCsvHeader,
            $"UKFTT-GRC,.pdf,{sourceUuid},{tracker.CurrentParserRunId},Started,,,,/some/long/path/example.pdf,,my-metadata-hash,,2025-06-15 10:30:00.000"
        ], trackerLines);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[2023] ABCD 123")]
    public async Task UpdateToParsedAsync_SetsParsedStatusWithTreReferenceNcnAndContentHash(string? ncn)
    {
        // Arrange
        const string sourceUuid = "00000000-0000-0000-0000-000000000001";
        const string treReference = "00000000-0000-0000-0000-00000000002";
        const string csvMetadataHash = "my-metadata-hash";
        const string documentContentHash = "my-document-hash";
        const string caseName = "Case Name";

        var tracker = CreateTracker();
        // Arrange - set existing tracker lines via tracker because it holds internal state separate to the file
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero));
        await tracker.StartTrackingAsync(Guid.Parse(sourceUuid), CsvMetadataLineHelper.DummyLine, csvMetadataHash);

        // Arrange - advance time by a minute to ensure the timestamp is updated
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(2025, 6, 15, 10, 31, 0, TimeSpan.Zero));

        // Act - update tracker
        await tracker.UpdateToParsedAsync(Guid.Parse(sourceUuid), treReference, ncn, documentContentHash, caseName);

        // Assert
        var trackerLines =
            await mockFileSystem.File.ReadAllLinesAsync(TrackerFilePath, TestContext.Current.CancellationToken);
        Assert.Equal([
                TrackerCsvHeader,
                $"UKFTT-GRC,.pdf,{sourceUuid},{tracker.CurrentParserRunId},Parsed,{treReference},{ncn ?? ""},{caseName},/some/long/path/example.pdf,{documentContentHash},{csvMetadataHash},,2025-06-15 10:31:00.000"
            ],
            trackerLines);
    }

    [Fact]
    public async Task UpdateToParserFailedAsync_SetsParserFailedStatusWithExceptionMessage()
    {
        // Arrange
        const string sourceUuid = "00000000-0000-0000-0000-000000000001";
        const string csvMetadataHash = "my-metadata-hash";
        var tracker = CreateTracker();

        // Arrange - set existing tracker lines via tracker because it holds internal state separate to the file
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero));
        await tracker.StartTrackingAsync(Guid.Parse(sourceUuid), CsvMetadataLineHelper.DummyLine, csvMetadataHash);

        // Arrange - advance time by a minute to ensure the timestamp is updated
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(2025, 6, 15, 10, 31, 0, TimeSpan.Zero));

        // Act - update tracker
        await tracker.UpdateToParserFailedAsync(Guid.Parse(sourceUuid),
            new InvalidOperationException("Something went wrong"));

        // Assert
        var trackerLines =
            await mockFileSystem.File.ReadAllLinesAsync(TrackerFilePath, TestContext.Current.CancellationToken);
        Assert.Equal([
                TrackerCsvHeader,
                $"UKFTT-GRC,.pdf,{sourceUuid},{tracker.CurrentParserRunId},ParserFailed,,,,/some/long/path/example.pdf,,{csvMetadataHash},Something went wrong,2025-06-15 10:31:00.000"
            ],
            trackerLines);
    }

    [Fact]
    public async Task UpdateToSentToIngesterAsync_SetsSentToIngesterStatus()
    {
        // Arrange
        const string sourceUuid = "00000000-0000-0000-0000-000000000001";
        const string treReference = "00000000-0000-0000-0000-00000000002";
        const string csvMetadataHash = "my-metadata-hash";
        const string documentContentHash = "my-document-hash";
        const string ncn = "[2023] ABCD 123";
        const string caseName = "Case Name";

        var tracker = CreateTracker();

        // Arrange - set existing tracker lines via tracker because it holds internal state separate to the file
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero));
        await tracker.StartTrackingAsync(Guid.Parse(sourceUuid), CsvMetadataLineHelper.DummyLine, csvMetadataHash);
        await tracker.UpdateToParsedAsync(Guid.Parse(sourceUuid), treReference, ncn, documentContentHash, caseName);

        // Arrange - advance time by a couple of minutes to ensure the timestamp is updated
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(2025, 6, 15, 10, 32, 0, TimeSpan.Zero));

        // Act
        await tracker.UpdateToSentToIngesterAsync(Guid.Parse(sourceUuid));

        // Assert
        var trackerLines =
            await mockFileSystem.File.ReadAllLinesAsync(TrackerFilePath, TestContext.Current.CancellationToken);

        Assert.Equal(TrackerCsvHeader, trackerLines[0]);

        Assert.Equal([
                TrackerCsvHeader,
                $"UKFTT-GRC,.pdf,{sourceUuid},{tracker.CurrentParserRunId},SentToIngester,{treReference},{ncn},{caseName},/some/long/path/example.pdf,{documentContentHash},{csvMetadataHash},,2025-06-15 10:32:00.000"
            ],
            trackerLines);
    }


    [Fact]
    public async Task TrackerOperations_DoNotOverwritePreviousRunLines()
    {
        // Arrange
        const string sourceUuid = "00000000-0000-0000-0000-000000000001";
        const string treReference = "00000000-0000-0000-0000-00000000002";
        const string csvMetadataHash = "my-metadata-hash";
        const string documentContentHash = "my-document-hash";
        const string caseName = "Case Name";
        const string ncn = "[2023] ABCD 123";

        // Arrange
        string[] oldTrackerLines =
        [
            "UKSC,docx,e169cd7c-6fe0-446d-91d2-9e4de2829b38,10000000-0000-0000-0000-000000000099,SentToIngester,ref1,ncn1,Case Name,word.docx,hash1,metahash1,,2025-06-14 09:30:00.000",
            $"UKSC,docx,{sourceUuid},10000000-0000-0000-0000-000000000099,ParserFailed,ref1,ncn1,Case Name,word.docx,hash1,metahash1,,2025-06-14 10:30:00.000",
            $"UKSC,docx,{sourceUuid},20000000-0000-0000-0000-000000000099,Parsed,ref1,ncn1,Case Name,word.docx,hash1,metahash1,,2025-06-15 10:30:00.000"
        ];
        var tracker = CreateTracker(oldTrackerLines);
        fakeTimeProvider.SetUtcNow(new DateTimeOffset(2025, 6, 15, 10, 32, 0, TimeSpan.Zero));

        // Act - trigger all tracker operations
        _ = tracker.IsAlreadySentToIngester(Guid.Parse(sourceUuid));
        await tracker.StartTrackingAsync(Guid.Parse(sourceUuid), CsvMetadataLineHelper.DummyLine, csvMetadataHash);
        await tracker.UpdateToParsedAsync(Guid.Parse(sourceUuid), treReference, ncn, documentContentHash, caseName);

        await tracker.UpdateToSentToIngesterAsync(Guid.Parse(sourceUuid));

        var trackerLines =
            await mockFileSystem.File.ReadAllLinesAsync(TrackerFilePath, TestContext.Current.CancellationToken);
        Assert.Equal([
                TrackerCsvHeader,
                .. oldTrackerLines,
                $"UKFTT-GRC,.pdf,{sourceUuid},{tracker.CurrentParserRunId},SentToIngester,{treReference},{ncn},{caseName},/some/long/path/example.pdf,{documentContentHash},{csvMetadataHash},,2025-06-15 10:32:00.000"
            ],
            trackerLines);
    }
}
