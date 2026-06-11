using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Backlog.Tracking;

using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

using Metadata = UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata;

namespace test.backlog.EndToEndTests
{
    public partial class EndToEndTests(ITestOutputHelper testOutputHelper) : BaseEndToEndTests(testOutputHelper)
    {
        private static readonly string ExpectedParserVersion = typeof(Metadata)
                                                               .Assembly
                                                               .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                                               .InformationalVersion;

        private string outputDir;
        private string trackerPath;
        private string dataDir;

        protected override void Dispose(bool disposing)
        {
            CleanFiles();
            base.Dispose(disposing);
        }

        private void CleanFiles()
        {
            // Only clean up output files and tracker, leave test data intact
            var trackerDirectory = Path.GetDirectoryName(trackerPath);
            if (Directory.Exists(trackerDirectory))
            {
                foreach (var trackerDbFile in Directory.GetFiles(trackerDirectory!, $"{Path.GetFileName(trackerPath)}*"))
                {
                    File.Delete(trackerDbFile);
                }
            }

            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }

            if (Directory.Exists(dataDir))
            {
                foreach (var logFile in Directory.GetFiles(dataDir, "log*.txt"))
                {
                    File.Delete(logFile);
                }
            }
        }

        private void ConfigureTestEnvironment(string testCaseName)
        {
            // Use the test data directory with pre-populated files
            var workingDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
            var backlogDirectory = workingDirectory
                                   .Parent?
                                   .Parent?
                                   .Parent?
                                   .GetDirectories("backlog").SingleOrDefault()
                                   ?? throw new DirectoryNotFoundException("Could not find backlog directory");
            var testDataDirectory = backlogDirectory.GetDirectories("test-data").SingleOrDefault()
                                    ?? throw new DirectoryNotFoundException("Could not find test-data directory");

            dataDir = testDataDirectory.GetDirectories(testCaseName).SingleOrDefault()?.FullName
                      ?? throw new DirectoryNotFoundException($"Could not find {testCaseName} directory");

            var courtMetadataFilePath = Path.Combine(dataDir, "court_metadata.csv");
            
            // Ensure court document directory exists
            var courtDocumentsDir = Path.Combine(dataDir, "court_documents");
            Directory.CreateDirectory(courtDocumentsDir); //creates the folder if it doesn't exist

            // Create the output directory - input directories should already exist with test data
            outputDir = Path.Combine(dataDir, "output");
            Directory.CreateDirectory(outputDir);

            // Store the tracker path so we can clean it later
            trackerPath = Path.Combine(dataDir, $"tracker{Guid.NewGuid()}.db");

            // Set environment variables for this test
            SetPathEnvironmentVariables(dataDir, outputDir, courtMetadataFilePath, trackerPath);

            // Requires the environment variables to be set up to know where to clean
            CleanFiles();
        }

        private void AssertCapturedContentMatchesOutputContent(string capturedKey)
        {
            var capturedContent = mockS3Client.GetCapturedContent(capturedKey);

            var outputFilePath = Path.Combine(outputDir, capturedKey);
            Assert.True(File.Exists(outputFilePath), "Output file should exist");

            var outputContent = File.ReadAllBytes(outputFilePath);
            Assert.Equal(capturedContent, outputContent);
        }

        private void AssertCapturedContentContainsExpectedXml(string capturedKey, string expectedXmlResourceName)
        {
            var actualXml =
                mockS3Client.GetCapturedContent(capturedKey).GetFileFromZippedContentAsString("judgment.xml");
            var expectedXml = DocumentHelpers.ReadXml(expectedXmlResourceName);

            Assert.Equal(ExpectedParserVersion, ExtractParserVersion(actualXml));

            actualXml = DocumentHelpers.RemoveNonDeterministicMetadata(actualXml);
            expectedXml = DocumentHelpers.RemoveNonDeterministicMetadata(expectedXml);

            Assert.Equal(expectedXml, actualXml);
        }

        private void AssertCapturedContentContainsExpectedSourceFile(string capturedKey, string bundleSourceName,
            string testFileResourceName)
        {
            var actualFileContents = mockS3Client.GetCapturedContent(capturedKey)
                                                 .GetFileFromZippedContentAsBytes(bundleSourceName);
            var expectedContents = DocumentHelpers.GetEmbeddedResourceAsBytes(testFileResourceName);

            Assert.Equal(expectedContents, actualFileContents);
        }

        private string GetParserRunIdFromCapturedMetadataJson(string capturedKey)
        {
            var actualMetadataJson =
                mockS3Client.GetCapturedContent(capturedKey).GetFileFromZippedContentAsString("bulk-metadata.json");

            return Regex.Match(actualMetadataJson, $"\"parser_run_id\":\"({GuidRegex()})\"").Groups[1].Value;
        }

        private void AssertCapturedContentContainsExpectedMetadataJson(string capturedKey,
            string expectedMetadataJsonResourceName)
        {
            var actualMetadataJson =
                mockS3Client.GetCapturedContent(capturedKey).GetFileFromZippedContentAsString("bulk-metadata.json");
            var expectedMetadataJson = DocumentHelpers.ReadEmbeddedResourceAsString(expectedMetadataJsonResourceName);

            // Remove non-deterministic GUIDs
            actualMetadataJson = GuidRegex().Replace(actualMetadataJson, "");
            expectedMetadataJson = GuidRegex().Replace(expectedMetadataJson, "");

            Assert.Equal(expectedMetadataJson, actualMetadataJson);
        }

        /// <summary>
        ///     Pulls the first &lt;uk:parser&gt; value out of the supplied XML, preserving whitespace.
        /// </summary>
        private static string ExtractParserVersion(string xml)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            XNamespace uk = "https://caselaw.nationalarchives.gov.uk/akn";
            return document.Descendants(uk + "parser").FirstOrDefault()?.Value;
        }

        [Theory]
        [InlineData("2002-010.doc.docx", "test.backlog.test_data.Altaf_Ebrahim_t_a_Ebrahim___Co_v_OISC.court_documents.e14fb247-5d9b-42b8-9238-52ae3bd8345b.docx", "Altaf Ebrahim t_a Ebrahim & Co v OISC", 5)]
        [InlineData("D 2011 306 Sultan  Others.docx", "test.backlog.test_data.Sultan_Others.court_documents.3cf61114-2d77-4e7a-aba0-6891faaf9d39.docx", "Sultan Others", 1243)]
        [InlineData("CCA20120008_20130118_order_appeal_discontinued.pdf", "test.backlog.test_data.Money_Worries_Ltd_v_Office_of_Fair_Trading.court_documents.ac4e30ac-416c-494d-8a76-a0dee0ca93bc", "Money Worries Ltd v Office of Fair Trading", 20)]
        [InlineData("original, document, name.docx", "test.backlog.test_data.DocxWithNcn.court_documents.f89b65cc-6709-4a2f-bc34-a2e21372dea6.docx", "DocxWithNcn", 42)]
        public void ProcessBacklogJudgment_SuccessfullyUploadsExpectedFilesToS3(string fileName, string resourceName,
            string testCaseName, uint docId)
        {
            // Setup test environment
            ConfigureTestEnvironment(testCaseName);
            // This time is the "now" that is used in the "expected metadata" JSON fixture
            var expectedTime = new DateTimeOffset(1999, 9, 9, 9, 9, 9, TimeSpan.Zero);
            fakeTimeProvider.AdjustTime(expectedTime);

            // Act
            var exitCode = Backlog.Program.Main("--id", docId.ToString(), "--auto-publish");

            // Assert - Program exited successfully
            AssertProgramExitedSuccessfully(exitCode);

            // Assert - Verify content was uploaded
            mockS3Client.AssertNumberOfUploads(1);
            mockS3Client.AssertUploadsWereValid();

            var capturedKey = mockS3Client.CapturedKeys.Single();

            // Assert - Check tracker was updated
            File.Exists(trackerPath).ShouldBeTrue();
            using (var trackerDb = TrackerDbHelper.OpenFileTrackerDb(trackerPath))
            {
                trackerDb.ParserEvents
                         .ShouldHaveSingleItem()
                         .TrackerStatus.ShouldBe(TrackerStatus.SentToIngester);
            }

            // Assert - Check output files are as expected
            AssertCapturedContentMatchesOutputContent(capturedKey);
            AssertCapturedContentContainsExpectedSourceFile(capturedKey, fileName, resourceName);
            AssertCapturedContentContainsExpectedXml(capturedKey, $"test.backlog.expected_output.{testCaseName}.xml");
            AssertCapturedContentContainsExpectedMetadataJson(capturedKey,
                $"test.backlog.expected_output.{testCaseName}.json");
        }

        [Fact]
        public void ProcessBacklogJudgment_FullCSV_ProcessesMultipleJudgments()
        {
            // Setup test environment for multi-line CSV test
            ConfigureTestEnvironment("MultiLineTest");

            // Act - Run without --id to process full CSV
            var exitCode = Backlog.Program.Main();

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            // Assert - uploaded 3 files to S3
            mockS3Client.AssertNumberOfUploads(3);
            mockS3Client.AssertUploadsWereValid();

            // Assert - only one parser run id in captured metadata
            var parserRunId = mockS3Client.CapturedKeys
                                          .Select(GetParserRunIdFromCapturedMetadataJson)
                                          .Distinct()
                                          .ShouldHaveSingleItem();

            // Assert - tracker was updated for all processed items
            File.Exists(trackerPath).ShouldBeTrue();
            using var trackerDb = TrackerDbHelper.OpenFileTrackerDb(trackerPath);
            trackerDb.ParserEvents.Count().ShouldBe(3);
            foreach (var treReference in mockS3Client.CapturedKeys.Select(key => key.Replace(".tar.gz", string.Empty)))
            {
                trackerDb.ParserEvents.Single(t => t.TreReference == treReference)
                         .ShouldSatisfyAllConditions(
                             t => t.TrackerStatus.ShouldBe(TrackerStatus.SentToIngester),
                             t => t.ParserRunId.ToString().ShouldBe(parserRunId));
            }
        }

        [Fact]
        public void ProcessBacklogJudgment_FullCSV_SkipsAlreadyProcessedItems()
        {
            // Setup test environment
            ConfigureTestEnvironment("MultiLineTest");

            // Pre-populate tracker to mark first item as already processed
            TrackerDbHelper.SeedFileTrackerDb(trackerPath,
                new TrackerLine
                {
                    Court = "UKSC",
                    FileExtension = "docx",
                    SourceUuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    ParserRunId = Guid.Parse("6ee2ae0f-9b8a-4d9f-99f0-f66d7234bd2e"),
                    TrackerStatus = TrackerStatus.SentToIngester,
                    TrackerLineLastUpdated = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TrackerLine
                {
                    Court = "UKSC",
                    FileExtension = "docx",
                    SourceUuid = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    ParserRunId = Guid.Parse("8342b8f3-4e7e-40e1-a330-a06657bd67f2"),
                    TrackerStatus = TrackerStatus.ParserFailed,
                    TrackerLineLastUpdated = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)
                });

            // Act
            var exitCode = Backlog.Program.Main();

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            File.Exists(trackerPath).ShouldBeTrue();
            using var trackerDb = TrackerDbHelper.OpenFileTrackerDb(trackerPath);
            var trackerLines = trackerDb.ParserEvents.ToList();

            // Should have original entries plus new successful entries
            trackerLines.Count.ShouldBe(4);

            // Old run lines should still be present
            trackerLines.ShouldContain(t =>
                t.SourceUuid == Guid.Parse("11111111-1111-1111-1111-111111111111")
                && t.TrackerStatus == TrackerStatus.SentToIngester
                && t.ParserRunId == Guid.Parse("6ee2ae0f-9b8a-4d9f-99f0-f66d7234bd2e"));
            trackerLines.ShouldContain(t =>
                t.SourceUuid == Guid.Parse("22222222-2222-2222-2222-222222222222")
                && t.TrackerStatus == TrackerStatus.ParserFailed
                && t.ParserRunId == Guid.Parse("8342b8f3-4e7e-40e1-a330-a06657bd67f2"));

            // Retried line and newly processed line
            trackerLines.ShouldContain(t =>
                t.SourceUuid == Guid.Parse("22222222-2222-2222-2222-222222222222")
                && t.TrackerStatus == TrackerStatus.SentToIngester
                && t.ParserRunId != Guid.Parse("8342b8f3-4e7e-40e1-a330-a06657bd67f2"));
            trackerLines.ShouldContain(t =>
                t.SourceUuid == Guid.Parse("33333333-3333-3333-3333-333333333333")
                && t.TrackerStatus == TrackerStatus.SentToIngester);

            // Log file should mention skips
            ConsolidatedLogger.VerifyLog("Skipping line 5 because it was marked to skip in the csv", LogLevel.Information)
                              .VerifyLog("Skipping 100 because it was previously processed", LogLevel.Information)
                              .VerifyLog("""
                                         ---------------------------
                                         Successfully processed 4 of 4 csv lines, of which:
                                           - 2 lines were new (1 .docx, 1 .pdf)
                                           - 1 lines were marked in the csv to skip (Line 5)
                                           - 1 lines were skipped because they had been processed in a previous run
                                         """, LogLevel.Information);
        }

        [Fact]
        public void ProcessBacklogJudgment_WithId_OnlyProcessesSpecifiedId()
        {
            // Setup test environment
            ConfigureTestEnvironment("MultiLineTest");

            // Act
            var exitCode = Backlog.Program.Main("--id", "102");

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            File.Exists(trackerPath).ShouldBeTrue();
            using var trackerDb = TrackerDbHelper.OpenFileTrackerDb(trackerPath);
            trackerDb.ParserEvents.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                t => t.SourceUuid.ShouldBe(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                t => t.TrackerStatus.ShouldBe(TrackerStatus.SentToIngester)
            );
        }

        [Fact]
        public void ProcessBacklogJudgment_FullCSV_WithEmptyCSV_ReturnsError()
        {
            // Setup test environment with empty CSV
            ConfigureTestEnvironment("EmptyCSVTest");

            // Act
            var exitCode = Backlog.Program.Main();

            // Assert
            Assert.Equal(1, exitCode);
            ConsolidatedLogger.VerifyLog("No valid records found in the metadata file", LogLevel.Critical);
        }

        [Fact]
        public void ProcessBacklogJudgment_WithInvalidIdArgument_ReturnsError()
        {
            // Act
            var exitCode = Backlog.Program.Main("--id", "invalid");

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public void ProcessBacklogJudgment_WithInvalidArguments_ReturnsError()
        {
            // Act
            var exitCode = Backlog.Program.Main("--unknown-arg");

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Theory]
        [InlineData(new string[0], false)]
        [InlineData(new[] { "--auto-publish", "false" }, false)]
        [InlineData(new[] { "--auto-publish" }, true)]
        [InlineData(new[] { "--auto-publish", "true" }, true)]
        public void ProcessBacklogJudgment_AutoPublish_IsConfigurableViaCli(string[] extraArgs, bool expectedAutoPublish)
        {
            // Setup test environment
            ConfigureTestEnvironment("Money Worries Ltd v Office of Fair Trading");
            fakeTimeProvider.AdjustTime(new DateTimeOffset(1999, 9, 9, 9, 9, 9, TimeSpan.Zero));

            // Act
            var args = new[] { "--id", "20" }.Concat(extraArgs).ToArray();
            var exitCode = Backlog.Program.Main(args);

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            var capturedKey = mockS3Client.CapturedKeys.Single();
            var metadataJson =
                mockS3Client.GetCapturedContent(capturedKey).GetFileFromZippedContentAsString("bulk-metadata.json");
            var jsonNode = JsonNode.Parse(metadataJson);
            var autoPublish = jsonNode!["parameters"]!["INGESTER_OPTIONS"]!["auto_publish"]!.GetValue<bool>();
            Assert.Equal(expectedAutoPublish, autoPublish);
        }

        [GeneratedRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")]
        private static partial Regex GuidRegex();
    }
}
