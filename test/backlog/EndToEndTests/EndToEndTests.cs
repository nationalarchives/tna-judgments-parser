using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;

using Xunit;

using Metadata = UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata;

namespace test.backlog.EndToEndTests
{
    public partial class EndToEndTests(ITestOutputHelper testOutputHelper) : BaseEndToEndTests(testOutputHelper)
    {
        private static readonly string ExpectedParserVersion = typeof(Metadata)
                                                               .Assembly
                                                               .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                                               .InformationalVersion ?? string.Empty;

        private string outputDir = string.Empty;
        private string trackerPath = string.Empty;
        private string dataDir = string.Empty;

        protected override void Dispose(bool disposing)
        {
            CleanFiles();
            base.Dispose(disposing);
        }

        private void CleanFiles()
        {
            // Only clean up output files and tracker, leave test data intact
            if (File.Exists(trackerPath))
            {
                File.Delete(trackerPath);
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

            // Ensure court document directory exists
            var courtDocumentsDir = Path.Combine(dataDir, "court_documents");
            Directory.CreateDirectory(courtDocumentsDir); //creates the folder if it doesn't exist
            
            // Create the output directory - input directories should already exist with test data
            outputDir = Path.Combine(dataDir, "output");
            Directory.CreateDirectory(outputDir);

            // Store the tracker path so we can clean it later
            trackerPath = Path.Combine(dataDir, "uploaded-production.csv");

            // Set environment variables for this test
            SetPathEnvironmentVariables(dataDir, outputDir, trackerPath: trackerPath);

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
                ZipFileHelpers.GetFileFromZippedContent(mockS3Client.GetCapturedContent(capturedKey), @".*\.xml");
            var expectedXml = DocumentHelpers.ReadXml(expectedXmlResourceName);

            Assert.Equal(ExpectedParserVersion, ExtractParserVersion(actualXml));

            actualXml = DocumentHelpers.RemoveNonDeterministicMetadata(actualXml);
            expectedXml = DocumentHelpers.RemoveNonDeterministicMetadata(expectedXml);

            Assert.Equal(expectedXml, actualXml);
        }

        private string GetParserRunIdFromCapturedMetadataJson(string capturedKey)
        {
            var actualMetadataJson =
                ZipFileHelpers.GetFileFromZippedContent(mockS3Client.GetCapturedContent(capturedKey), @".*\.json");

            return Regex.Match(actualMetadataJson, $"\"parser_run_id\":\"({GuidRegex()})\"").Groups[1].Value;
        }

        private static string[] ReadManifestLines(string manifestPath)
        {
            return File.ReadAllLines(manifestPath)
                       .Where(line => !string.IsNullOrWhiteSpace(line))
                       .ToArray();
        }

        private void AssertCapturedContentContainsExpectedMetadataJson(string capturedKey,
            string expectedMetadataJsonResourceName)
        {
            var actualMetadataJson =
                ZipFileHelpers.GetFileFromZippedContent(mockS3Client.GetCapturedContent(capturedKey), @".*\.json");
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
            return document.Descendants(uk + "parser").FirstOrDefault()?.Value ?? string.Empty;
        }

        [Theory]
        [InlineData("docx", "Altaf Ebrahim t_a Ebrahim & Co v OISC", "JudgmentFiles\\", "data/HMCTS_Judgment_Files/", 5)]
        [InlineData("docx", "Sultan Others", "", "", 1243)]
        [InlineData("pdf", "Money Worries Ltd v Office of Fair Trading", "Documents\\", "data/Consumer Credit Appeals/Documents/", 20)]
        public void ProcessBacklogJudgment_SuccessfullyUploadsExpectedFilesToS3(string _, string testCaseName, string judgmentFilePath, string hmctsFilesPath, uint docId)
        {
            // Setup test environment
            ConfigureTestEnvironment(testCaseName);
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", judgmentFilePath);
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", hmctsFilesPath);
            // This time is the "now" that is used in the "expected metadata" JSON fixture
            var expectedTime = new DateTimeOffset(1999, 9, 9, 9, 9, 9, TimeSpan.Zero);
            fakeTimeProvider.AdjustTime(expectedTime);

            // Act
            var exitCode = Backlog.Src.Program.Main("--id", docId.ToString(), "--auto-publish");

            // Assert - Program exited successfully
            AssertProgramExitedSuccessfully(exitCode);

            // Assert - Verify content was uploaded
            mockS3Client.AssertNumberOfUploads(1);
            mockS3Client.AssertUploadsWereValid();

            var capturedKey = mockS3Client.CapturedKeys.Single();

            // Assert - Check tracker was updated
            Assert.Contains(GetUuidFromKey(capturedKey), File.ReadAllText(trackerPath));

            // Assert - Check output files are as expected
            AssertCapturedContentMatchesOutputContent(capturedKey);
            AssertCapturedContentContainsExpectedXml(capturedKey, $"test.backlog.expected_output.{testCaseName}.xml");
            AssertCapturedContentContainsExpectedMetadataJson(capturedKey, $"test.backlog.expected_output.{testCaseName}.json");
        }

        [Fact]
        public void ProcessBacklogJudgment_FullCSV_ProcessesMultipleJudgments()
        {
            // Setup test environment for multi-line CSV test
            ConfigureTestEnvironment("MultiLineTest");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files/");

            // Act - Run without --id to process full CSV
            var exitCode = Backlog.Src.Program.Main();
            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            mockS3Client.AssertNumberOfUploads(3);
            mockS3Client.AssertUploadsWereValid();

            var capturedParserRunIds = new List<string>();
            var trackerContents = File.ReadAllText(trackerPath);
            foreach (var key in mockS3Client.CapturedKeys)
            {
                // Verify tracker was updated for all processed items
                Assert.Contains(GetUuidFromKey(key), trackerContents);
                capturedParserRunIds.Add(GetParserRunIdFromCapturedMetadataJson(key));
            }
            
            //Ensure that the parser run ids with each document is the same
            Assert.Single(capturedParserRunIds.Distinct());

            // Verify manifest CSV structure and contents
            var manifestPath = Directory.GetFiles(outputDir, "batch-manifest-*.csv").Single();
            var manifestLines = ReadManifestLines(manifestPath);
            var capturedKeys = mockS3Client.CapturedKeys.ToArray();
            var parserRunIdFilePath = Directory.GetFiles(outputDir, "parser-run-id-*.txt").Single();
            var bundleReferencesFilePath = Directory.GetFiles(outputDir, "bundle-references-*.txt").Single();
            
            // Verify header and row count
            Assert.Equal(capturedKeys.Length + 1, manifestLines.Length);
            Assert.Equal("parser_run_id,bundle_reference,bundle_filename,source_filename,source_uuid,parser_uri,parser_cite", manifestLines[0]);

            var manifestRows = manifestLines.Skip(1).Select(line => line.Split(',')).ToArray();
            Assert.Equal(capturedKeys.Length, manifestRows.Length);
            Assert.All(manifestRows, row => Assert.Equal(7, row.Length));

            // All rows should have the same parser_run_id (col 0)
            Assert.All(manifestRows, row => Assert.Equal(capturedParserRunIds[0], row[0]));

            // Verify direct reconciliation inputs were written alongside the manifest
            Assert.Equal(capturedParserRunIds[0], File.ReadAllText(parserRunIdFilePath).Trim());

            // Verify bundle filenames (col 2) match captured S3 keys
            var bundleFilenamesFromManifest = manifestRows.Select(r => r[2]).OrderBy(x => x).ToArray();
            var capturedKeysOrdered = capturedKeys.OrderBy(k => k).ToArray();
            Assert.Equal(capturedKeysOrdered, bundleFilenamesFromManifest);

            var expectedBundleReferences = manifestRows.Select(r => r[1]).OrderBy(x => x).ToArray();
            var bundleReferencesFromFile = File.ReadAllLines(bundleReferencesFilePath).OrderBy(x => x).ToArray();
            Assert.Equal(expectedBundleReferences, bundleReferencesFromFile);

            // Verify source filenames (col 3) match test data
            var sourceFilenames = manifestRows.Select(r => r[3]).OrderBy(x => x).ToArray();
            Assert.Contains("test1.doc", sourceFilenames);
            Assert.Contains("test2.docx", sourceFilenames);
            Assert.Contains("test3.pdf", sourceFilenames);

            // Verify bundle_reference (col 1) and source_uuid (col 4) are valid GUIDs
            foreach (var row in manifestRows)
            {
                Assert.True(Guid.TryParse(row[1], out _));
                Assert.True(Guid.TryParse(row[4], out _));
                Assert.EndsWith(".tar.gz", row[2]);
            }

            // Log manifest contents for verification
            PrintToOutputWithNumberedLines("Manifest CSV contents:");
            PrintToOutputWithNumberedLines(manifestLines);
        }

        [Fact]
        public async Task ProcessBacklogJudgment_FullCSV_SkipsAlreadyProcessedItems()
        {
            // Setup test environment
            ConfigureTestEnvironment("MultiLineTest");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files/");

            // Pre-populate tracker to mark first item as already processed
            await File.WriteAllTextAsync(trackerPath, "100/JudgmentFiles\\j100\\test1.doc,some-uuid-1,132345678901234567\n",
                TestContext.Current.CancellationToken);

            // Act
            var exitCode = Backlog.Src.Program.Main();

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            // Should process fewer items than total (since one was already done)
            // The exact count depends on test data - we'll verify this doesn't process ALL items
            var trackerContent = await File.ReadAllTextAsync(trackerPath, TestContext.Current.CancellationToken);
            var trackerLines = trackerContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Should have the original entry plus new entries
            Assert.True(trackerLines.Length > 1, "Tracker should have original entry plus new entries");
            Assert.True(trackerLines[0].Contains("some-uuid-1"), "First line should be the pre-existing entry");

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
        public async Task ProcessBacklogJudgment_WithId_OnlyProcessesSpecifiedId()
        {
            // Setup test environment
            ConfigureTestEnvironment("MultiLineTest");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files/");

            // Act
            var exitCode = Backlog.Src.Program.Main("--id", "102");

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            var trackerLines = await File.ReadAllLinesAsync(trackerPath, TestContext.Current.CancellationToken);
            var singleLine = Assert.Single(trackerLines);
            Assert.StartsWith("102/JudgmentFiles\\j102\\test3.pdf,", singleLine);
        }

        [Fact]
        public void ProcessBacklogJudgment_FullCSV_WithEmptyCSV_ReturnsError()
        {
            // Setup test environment with empty CSV
            ConfigureTestEnvironment("EmptyCSVTest");

            // Act
            var exitCode = Backlog.Src.Program.Main();

            // Assert
            Assert.Equal(1, exitCode);
            ConsolidatedLogger.VerifyLog("No valid records found in the metadata file", LogLevel.Critical);
        }

        [Fact]
        public void ProcessBacklogJudgment_WithInvalidIdArgument_ReturnsError()
        {
            // Act
            var exitCode = Backlog.Src.Program.Main("--id", "invalid");

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public void ProcessBacklogJudgment_WithInvalidArguments_ReturnsError()
        {
            // Act
            var exitCode = Backlog.Src.Program.Main("--unknown-arg");

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
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "Documents\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/Consumer Credit Appeals/Documents/");
            fakeTimeProvider.AdjustTime(new DateTimeOffset(1999, 9, 9, 9, 9, 9, TimeSpan.Zero));

            // Act
            var args = new[] { "--id", "20" }.Concat(extraArgs).ToArray();
            var exitCode = Backlog.Src.Program.Main(args);

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            var capturedKey = mockS3Client.CapturedKeys.Single();
            var metadataJson =
                ZipFileHelpers.GetFileFromZippedContent(mockS3Client.GetCapturedContent(capturedKey), @".*\.json");
            var jsonNode = JsonNode.Parse(metadataJson);
            var autoPublish = jsonNode!["parameters"]!["INGESTER_OPTIONS"]!["auto_publish"]!.GetValue<bool>();
            Assert.Equal(expectedAutoPublish, autoPublish);
        }

        [GeneratedRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")]
        private static partial Regex GuidRegex();
    }
}
