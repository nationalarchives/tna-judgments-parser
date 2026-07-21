#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Backlog.Tracking;

using Shouldly;

using Xunit;

namespace test.backlog.EndToEndTests;

public class TrackerTests(ITestOutputHelper testOutputHelper) : BaseEndToEndTests(testOutputHelper)
{
    private string? trackerPath;
    private string? courtDocumentsDir;
    private string? courtMetadataPath;
    private string? tempDataDir;
    private const string Court = "UKUT-LC";

    protected override void Dispose(bool disposing)
    {
        if (tempDataDir is not null && Directory.Exists(tempDataDir))
        {
            Directory.Delete(tempDataDir, true);
        }

        base.Dispose(disposing);
    }

    private record TestData
    {
        public bool Is(Properties flag)
        {
            return properties.HasFlag(flag);
        }

        public TestData(int Id, Properties properties, TrackerStatus? Status = null)
        {
            this.Id = Id;
            this.properties = properties;
            this.Status = Status;

            Uuid = Guid.Parse($"00000000-0000-0000-0000-{Id:000000000000}");

            Appellant = $"appellant {Id}";
            Respondent = $"respondent {Id}";

            Ncn = Is(Properties.HasNcn) ? $"[2017] UKUT {Id} (LC)" : null;

            if (Is(Properties.IsDocx))
            {
                Extension = ".docx";
                Contents = DocumentHelpers.ReadDocx(1);
                DocumentHash = "6a67df51a7306d58da905765c3b050faf23a2f99e1a8e5750cab732b9814cf82";
            }
            else if (Is(Properties.IsPdf))
            {
                Extension = ".pdf";
                Contents = DocumentHelpers.GetEmbeddedResourceAsBytes(
                    "test.backlog.test_data.Money_Worries_Ltd_v_Office_of_Fair_Trading.court_documents.ac4e30ac-416c-494d-8a76-a0dee0ca93bc");
                DocumentHash = "a12036d81e1e533b7aa91dcfa73c96c36abca19b8c78a52aeecef89e6a3a578b";
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(properties), properties,
                    "Must set test data document type to either docx or pdf");
            }

            OriginalFilePathForDocument = $"Data/Folder/Orig{Id}{Extension}";
        }

        public string Extension { get; }
        public int Id { get; }
        public string? Ncn { get; }
        public string Appellant { get; }
        public string Respondent { get; }
        public string OriginalFilePathForDocument { get; }

        public Guid Uuid { get; }

        public byte[] Contents { get; }
        public string DocumentHash { get; }

        public TrackerStatus? Status { get; }

        private readonly Properties properties;

        [Flags]
        public enum Properties
        {
            ShouldSkip = 1,
            IsDocx = 2,
            IsPdf = 4,
            HasNcn = 8
        }
    }

    private void ConfigureTestEnvironment(params TestData[] testData)
    {
        // Create directories
        tempDataDir = Path.Combine(Path.GetTempPath(), $"FilesTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDataDir);

        var outputPath = Path.Combine(tempDataDir, "output");
        Directory.CreateDirectory(outputPath);

        courtDocumentsDir = Path.Combine(tempDataDir, "court_documents");
        Directory.CreateDirectory(courtDocumentsDir);

        // Set environment variables
        courtMetadataPath = Path.Combine(tempDataDir, "court_metadata.csv");
        trackerPath = Path.Combine(tempDataDir, $"tracker{Guid.NewGuid()}.db");

        SetPathEnvironmentVariables(tempDataDir, outputPath, courtMetadataPath, trackerPath);

        // Create files
        foreach (var document in testData)
        {
            File.WriteAllBytes(Path.Combine(courtDocumentsDir, document.Uuid.ToString()), document.Contents);
        }

        WriteCourtMetadataCsv(testData);
        var existingTrackerData = testData.Where(t => t.Status is not null)
                                          .Select(t => new TrackerLine
                                          {
                                              Court = Court,
                                              FileExtension = t.Extension,
                                              ParserRunId = Guid.NewGuid(),
                                              SourceUuid = t.Uuid,
                                              TrackerStatus = t.Status!.Value,
                                              TrackerLineLastUpdated = new DateTimeOffset(2025, 1, 1, 0, 0, 0,
                                                  TimeSpan.Zero)
                                          })
                                          .ToArray();
        TrackerDbHelper.SeedFileTrackerDb(trackerPath, existingTrackerData);
    }

    private void WriteCourtMetadataCsv(TestData[] testData)
    {
        const string headerLine = "id,FilePath,Extension,decision_datetime,court,appellants,respondent,skip,UUID,ncn";
        var csvMetadataLines = new List<string> { headerLine };

        csvMetadataLines.AddRange(testData.Select(item =>
            string.Join(',',
                item.Id,
                item.OriginalFilePathForDocument,
                item.Extension,
                "2001-01-01",
                Court,
                item.Appellant,
                item.Respondent,
                item.Is(TestData.Properties.ShouldSkip) ? "skip" : "",
                item.Uuid,
                item.Ncn
            )));

        File.WriteAllLines(courtMetadataPath!, csvMetadataLines);
        PrintToOutputWithNumberedLines(csvMetadataLines);
    }

    [Fact]
    public void Tracker_SuccessfulParse_AddsParserEventSentToIngester()
    {
        TestData[] data =
        [
            new(1, TestData.Properties.IsDocx | TestData.Properties.HasNcn),
            new(2, TestData.Properties.IsDocx),
            new(3, TestData.Properties.IsPdf | TestData.Properties.HasNcn),
            new(4, TestData.Properties.IsPdf)
        ];
        ConfigureTestEnvironment(data);

        var expectedTime = new DateTimeOffset(2026, 07, 08, 09, 10, 11, TimeSpan.Zero);
        fakeTimeProvider.AdjustTime(expectedTime);

        // Act
        var exitCode = Backlog.Program.Main();
        AssertProgramExitedSuccessfully(exitCode);

        // Assert - tracker was updated for all processed items
        using var trackerDb = TrackerDbHelper.OpenFileTrackerDb(trackerPath!);
        var actualParserEvents = trackerDb.ParserEvents.ToArray();
        actualParserEvents.Length.ShouldBe(4);

        // Assert - ParserEvents data that is always the same
        actualParserEvents.ShouldAllBe(t => t.TrackerStatus == TrackerStatus.SentToIngester);
        actualParserEvents.ShouldAllBe(t => t.ErrorMessage == null);
        actualParserEvents.ShouldAllBe(t => t.Court == Court);

        // Assert - CsvMetadataHash is the same for all ParserEvents (it varies per test run because of the temporary files created during the test)
        var expectedCsvMetadataHash = actualParserEvents[0].CsvMetadataHash;
        expectedCsvMetadataHash.ShouldBe("627c8ca63f0331c301b82dedeb4af9a4e927bb8dedaf307c9aa9d497951cd342");
        actualParserEvents.ShouldAllBe(t =>
            t.CsvMetadataHash == "627c8ca63f0331c301b82dedeb4af9a4e927bb8dedaf307c9aa9d497951cd342");

        // Assert - ParserEvents contains data generated during run
        var parserRunId = GetParserRunIdFromLogs();
        actualParserEvents.ShouldAllBe(t => t.ParserRunId == parserRunId);
        actualParserEvents.Select(t => t.TreReference).ShouldBe(mockS3Client.TreReferencesFromCapturedKeys);

        // Assert - ParserEvents contains data from test inputs
        foreach (var expectedDocument in data)
        {
            var actualParserEvent = actualParserEvents
                                    .Where(t => t.SourceUuid == expectedDocument.Uuid)
                                    .ShouldHaveSingleItem();

            actualParserEvent.FileExtension.ShouldBe(expectedDocument.Extension);
            actualParserEvent.SourceUuid.ShouldBe(expectedDocument.Uuid);
            actualParserEvent.Ncn.ShouldBe(expectedDocument.Ncn);
            actualParserEvent.CaseName.ShouldBe($"{expectedDocument.Appellant} v {expectedDocument.Respondent}");
            actualParserEvent.OriginalFileName.ShouldBe(expectedDocument.OriginalFilePathForDocument);
            actualParserEvent.DocumentContentHash.ShouldBe(expectedDocument.DocumentHash);
            actualParserEvent.TrackerLineLastUpdated.ShouldBe(expectedTime);
        }
    }

    [Fact]
    public void Tracker_SkippedDocument_DoesNotAddParserEvent()
    {
        var skipped1 = new TestData(1, TestData.Properties.IsDocx | TestData.Properties.ShouldSkip);
        var skipped2 = new TestData(2, TestData.Properties.IsPdf | TestData.Properties.ShouldSkip);

        ConfigureTestEnvironment(
            new TestData(12, TestData.Properties.IsDocx),
            skipped1,
            new TestData(13, TestData.Properties.IsPdf),
            skipped2);

        // Act
        var exitCode = Backlog.Program.Main();
        AssertProgramExitedSuccessfully(exitCode);

        // Assert
        using var trackerDb = TrackerDbHelper.OpenFileTrackerDb(trackerPath!);

        trackerDb.ParserEvents.Count().ShouldBe(2);
        trackerDb.ParserEvents.ShouldNotContain(t => t.SourceUuid == skipped1.Uuid);
        trackerDb.ParserEvents.ShouldNotContain(t => t.SourceUuid == skipped2.Uuid);
    }
}
