#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using Backlog.Tracking;

using Microsoft.Extensions.Time.Testing;

using Shouldly;

using test.Mocks;

using Xunit;

namespace test.backlog;

public sealed class TestTracker : IDisposable
{
    private readonly FakeTimeProvider fakeTimeProvider = new();
    private readonly MockLogger<Tracker> mockLogger = new();

    private readonly Tracker tracker;
    private readonly TrackerDbContext trackerDbContext;

    public TestTracker()
    {
        trackerDbContext = TrackerDbHelper.CreateInMemoryTrackerDb();
        tracker = new Tracker(fakeTimeProvider, mockLogger.Object, trackerDbContext);
    }

    public void Dispose()
    {
        trackerDbContext.Dispose();
    }

    [Fact]
    public void CurrentParserRunId_IsDifferentForEveryRun()
    {
        var tracker1 = new Tracker(fakeTimeProvider, mockLogger.Object, trackerDbContext);
        var tracker2 = new Tracker(fakeTimeProvider, mockLogger.Object, trackerDbContext);

        tracker1.CurrentParserRunId.ShouldNotBe(tracker2.CurrentParserRunId);
    }

    [Fact]
    public async Task IsAlreadySentToIngester_WhenNoTrackerDbExists_ReturnsFalse()
    {
        var result = await tracker.IsAlreadySentToIngesterAsync(Guid.NewGuid());
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(TrackerStatus.Started, false)]
    [InlineData(TrackerStatus.Parsed, false)]
    [InlineData(TrackerStatus.ParserFailed, false)]
    [InlineData(TrackerStatus.IngesterFailed, false)]
    [InlineData(TrackerStatus.SentToIngester, true)]
    public async Task IsAlreadySentToIngester_WhenUuidWasSentInPreviousRun_Returns(TrackerStatus previousTrackerStatus,
        bool expectedResult)
    {
        var sourceUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        trackerDbContext.SetupTrackerWithExistingData(new TrackerLine
        {
            Court = "UKSC",
            ParserRunId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
            TrackerStatus = previousTrackerStatus,
            SourceUuid = sourceUuid,
            TreReference = "ref1",
            Ncn = "ncn1",
            CaseName = "V v V",
            OriginalFileName = "word.docx",
            DocumentContentHash = "hash1",
            CsvMetadataHash = "metahash1",
            ErrorMessage = "",
            TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero)
        });

        var result = await tracker.IsAlreadySentToIngesterAsync(sourceUuid);
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task IsAlreadySentToIngester_MultiplePreviousRunsWithOneSuccess_ReturnsTrue()
    {
        var sourceUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        trackerDbContext.SetupTrackerWithExistingData(
            new TrackerLine
            {
                Court = "UKSC",
                ParserRunId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
                TrackerStatus = TrackerStatus.ParserFailed,
                SourceUuid = sourceUuid,
                TreReference = "ref1",
                Ncn = "ncn1",
                CaseName = "V v V",
                OriginalFileName = "word.docx",
                DocumentContentHash = "hash1",
                CsvMetadataHash = "metahash1",
                ErrorMessage = "",
                TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 14, 10, 30, 0, TimeSpan.Zero)
            },
            new TrackerLine
            {
                Court = "UKSC",
                ParserRunId = Guid.Parse("00000000-0000-0000-0000-000000000098"),
                TrackerStatus = TrackerStatus.SentToIngester,
                SourceUuid = sourceUuid,
                TreReference = "ref1",
                Ncn = "ncn1",
                CaseName = "V v V",
                OriginalFileName = "word.docx",
                DocumentContentHash = "hash1",
                CsvMetadataHash = "metahash1",
                ErrorMessage = "",
                TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero)
            });

        var result = await tracker.IsAlreadySentToIngesterAsync(sourceUuid);
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAlreadySentToIngester_WhenDifferentUuidWasSent_ReturnsFalse()
    {
        trackerDbContext.SetupTrackerWithExistingData(new TrackerLine
        {
            Court = "UKSC",
            ParserRunId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
            TrackerStatus = TrackerStatus.SentToIngester,
            SourceUuid = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            TreReference = "ref1",
            Ncn = "ncn1",
            CaseName = "V v V",
            OriginalFileName = "word.docx",
            DocumentContentHash = "hash1",
            CsvMetadataHash = "metahash1",
            ErrorMessage = "",
            TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero)
        });

        var result = await tracker.IsAlreadySentToIngesterAsync(Guid.NewGuid());
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task StartTrackingAsync_WritesStartedStatusWithUuidAndRunIdToDb()
    {
        var now = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
        fakeTimeProvider.SetUtcNow(now);

        var uuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        await tracker.StartTrackingAsync(uuid, CsvMetadataLineHelper.DummyLine, "my-metadata-hash");

        trackerDbContext.ShouldHaveSavedSingleTrackerLineWhichIs(new TrackerLine
        {
            SourceUuid = uuid,
            ParserRunId = tracker.CurrentParserRunId,
            TrackerStatus = TrackerStatus.Started,
            Court = "UKFTT-GRC",
            OriginalFileName = "/some/long/path/example.pdf",
            FileExtension = ".pdf",
            CsvMetadataHash = "my-metadata-hash",
            TrackerLineLastUpdated = now
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[2023] ABCD 123")]
    public async Task UpdateToParsedAsync_SetsParsedStatusWithTreReferenceNcnAndContentHash(string? ncn)
    {
        // Arrange
        var sourceUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        trackerDbContext.SetupTrackerWithExistingData(new TrackerLine
        {
            SourceUuid = sourceUuid,
            ParserRunId = tracker.CurrentParserRunId,
            TrackerStatus = TrackerStatus.Started,
            Court = "UKFTT-GRC",
            OriginalFileName = "/some/long/path/example.pdf",
            FileExtension = ".pdf",
            CsvMetadataHash = "my-metadata-hash",
            TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero)
        });

        var now = new DateTimeOffset(2025, 6, 15, 10, 31, 0, TimeSpan.Zero);
        fakeTimeProvider.SetUtcNow(now);

        // Act
        await tracker.UpdateToParsedAsync(sourceUuid, "00000000-0000-0000-0000-00000000002", ncn, "my-document-hash",
            "Case Name");

        // Assert
        trackerDbContext.ShouldHaveSavedSingleTrackerLineWhichIs(new TrackerLine
        {
            SourceUuid = sourceUuid,
            ParserRunId = tracker.CurrentParserRunId,
            TrackerStatus = TrackerStatus.Parsed,
            Court = "UKFTT-GRC",
            OriginalFileName = "/some/long/path/example.pdf",
            FileExtension = ".pdf",
            CsvMetadataHash = "my-metadata-hash",
            TrackerLineLastUpdated = now,
            TreReference = "00000000-0000-0000-0000-00000000002",
            Ncn = ncn,
            DocumentContentHash = "my-document-hash",
            CaseName = "Case Name"
        });
    }

    [Fact]
    public async Task UpdateToParserFailedAsync_SetsParserFailedStatusWithExceptionMessage()
    {
        // Arrange
        var sourceUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        trackerDbContext.SetupTrackerWithExistingData(new TrackerLine
        {
            SourceUuid = sourceUuid,
            ParserRunId = tracker.CurrentParserRunId,
            TrackerStatus = TrackerStatus.Started,
            Court = "UKFTT-GRC",
            OriginalFileName = "/some/long/path/example.pdf",
            FileExtension = ".pdf",
            CsvMetadataHash = "my-metadata-hash",
            TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero)
        });

        var now = new DateTimeOffset(2025, 6, 15, 10, 31, 0, TimeSpan.Zero);
        fakeTimeProvider.SetUtcNow(now);

        // Act
        await tracker.UpdateToParserFailedAsync(sourceUuid, new InvalidOperationException("Something went wrong"));

        // Assert
        trackerDbContext.ShouldHaveSavedSingleTrackerLineWhichIs(new TrackerLine
            {
                SourceUuid = sourceUuid,
                ParserRunId = tracker.CurrentParserRunId,
                Court = "UKFTT-GRC",
                OriginalFileName = "/some/long/path/example.pdf",
                FileExtension = ".pdf",
                CsvMetadataHash = "my-metadata-hash",
                TrackerStatus = TrackerStatus.ParserFailed,
                ErrorMessage = "Something went wrong",
                TrackerLineLastUpdated = now
            }
        );
    }

    [Fact]
    public async Task UpdateToSentToIngesterAsync_SetsSentToIngesterStatus()
    {
        // Arrange
        var sourceUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        trackerDbContext.SetupTrackerWithExistingData(new TrackerLine
        {
            SourceUuid = sourceUuid,
            ParserRunId = tracker.CurrentParserRunId,
            TrackerStatus = TrackerStatus.Parsed,
            Court = "UKFTT-GRC",
            OriginalFileName = "/some/long/path/example.pdf",
            FileExtension = ".pdf",
            CsvMetadataHash = "my-metadata-hash",
            TreReference = "00000000-0000-0000-0000-00000000002",
            Ncn = "[2023] ABCD 123",
            DocumentContentHash = "my-document-hash",
            CaseName = "Case Name",
            TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero)
        });

        var now = new DateTimeOffset(2025, 6, 15, 10, 32, 0, TimeSpan.Zero);
        fakeTimeProvider.SetUtcNow(now);

        // Act
        await tracker.UpdateToSentToIngesterAsync(sourceUuid);

        // Assert
        trackerDbContext.ShouldHaveSavedSingleTrackerLineWhichIs(new TrackerLine
        {
            SourceUuid = sourceUuid,
            ParserRunId = tracker.CurrentParserRunId,
            Court = "UKFTT-GRC",
            OriginalFileName = "/some/long/path/example.pdf",
            FileExtension = ".pdf",
            CsvMetadataHash = "my-metadata-hash",
            TrackerStatus = TrackerStatus.SentToIngester,
            TreReference = "00000000-0000-0000-0000-00000000002",
            Ncn = "[2023] ABCD 123",
            CaseName = "Case Name",
            DocumentContentHash = "my-document-hash",
            TrackerLineLastUpdated = now
        });
    }

    [Fact]
    public void TrackSkipped_NewSkippedLine_DoesNotWriteToDb()
    {
        tracker.TrackSkipped("Line 5");

        trackerDbContext.ParserEvents.ShouldBeEmpty();
    }

    [Fact]
    public void HasCsvParseErrors_WhenNoCsvParseErrors_ReturnsFalse()
    {
        tracker.HasCsvParseErrors.ShouldBeFalse();
    }

    [Fact]
    public void HasCsvParseErrors_WhenCsvParseErrorExists_ReturnsTrue()
    {
        tracker.TrackCsvParseError("Line 3: missing field");
        tracker.HasCsvParseErrors.ShouldBeTrue();
    }

    [Fact]
    public async Task TrackerOperations_DoNotOverwritePreviousRunLines()
    {
        // Arrange - seed previous run data
        var sourceUuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        TrackerLine[] previousTrackerLines =
        [
            new()
            {
                Court = "UKSC",
                FileExtension = "docx",
                SourceUuid = Guid.Parse("e169cd7c-6fe0-446d-91d2-9e4de2829b38"),
                ParserRunId = Guid.Parse("10000000-0000-0000-0000-000000000099"),
                TrackerStatus = TrackerStatus.SentToIngester,
                TreReference = "ref1",
                Ncn = "ncn1",
                CaseName = "Case Name",
                OriginalFileName = "word.docx",
                DocumentContentHash = "hash1",
                CsvMetadataHash = "metahash1",
                ErrorMessage = "",
                TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 14, 9, 30, 0, TimeSpan.Zero)
            },
            new()
            {
                Court = "UKSC",
                FileExtension = "docx",
                SourceUuid = sourceUuid,
                ParserRunId = Guid.Parse("10000000-0000-0000-0000-000000000099"),
                TrackerStatus = TrackerStatus.ParserFailed,
                TreReference = "ref1",
                Ncn = "ncn1",
                CaseName = "Case Name",
                OriginalFileName = "word.docx",
                DocumentContentHash = "hash1",
                CsvMetadataHash = "metahash1",
                ErrorMessage = "",
                TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 14, 10, 30, 0, TimeSpan.Zero)
            },
            new()
            {
                Court = "UKSC",
                FileExtension = "docx",
                SourceUuid = sourceUuid,
                ParserRunId = Guid.Parse("20000000-0000-0000-0000-000000000099"),
                TrackerStatus = TrackerStatus.Parsed,
                TreReference = "ref1",
                Ncn = "ncn1",
                CaseName = "Case Name",
                OriginalFileName = "word.docx",
                DocumentContentHash = "hash1",
                CsvMetadataHash = "metahash1",
                ErrorMessage = "",
                TrackerLineLastUpdated = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero)
            }
        ];
        trackerDbContext.SetupTrackerWithExistingData(previousTrackerLines);

        var now = new DateTimeOffset(2025, 6, 15, 10, 32, 0, TimeSpan.Zero);
        fakeTimeProvider.SetUtcNow(now);

        // Act - trigger all tracker operations
        _ = tracker.IsAlreadySentToIngesterAsync(sourceUuid);
        await tracker.StartTrackingAsync(sourceUuid, CsvMetadataLineHelper.DummyLine, "my-metadata-hash");
        await tracker.UpdateToParsedAsync(sourceUuid, "00000000-0000-0000-0000-00000000002", "[2023] ABCD 123",
            "my-document-hash", "Case Name");
        await tracker.UpdateToSentToIngesterAsync(sourceUuid);

        // Assert - previous run lines are still present plus the new one
        trackerDbContext.ShouldHaveChangesSaved();
        trackerDbContext.ParserEvents.Count().ShouldBe(4);
        trackerDbContext.ParserEvents.ShouldContain(previousTrackerLines[0]);
        trackerDbContext.ParserEvents.ShouldContain(previousTrackerLines[1]);
        trackerDbContext.ParserEvents.ShouldContain(previousTrackerLines[2]);
        trackerDbContext.ParserEvents
                        .Single(t => t.ParserRunId == tracker.CurrentParserRunId)
                        .ShouldBe(new TrackerLine
                        {
                            TrackerStatus = TrackerStatus.SentToIngester,
                            ParserRunId = tracker.CurrentParserRunId,
                            SourceUuid = sourceUuid,
                            TreReference = "00000000-0000-0000-0000-00000000002",
                            Ncn = "[2023] ABCD 123",
                            CaseName = "Case Name",
                            DocumentContentHash = "my-document-hash",
                            CsvMetadataHash = "my-metadata-hash",
                            Court = "UKFTT-GRC",
                            FileExtension = ".pdf",
                            OriginalFileName = "/some/long/path/example.pdf",
                            TrackerLineLastUpdated = now
                        });
    }
}
