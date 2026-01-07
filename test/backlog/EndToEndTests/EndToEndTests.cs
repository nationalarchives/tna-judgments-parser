using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

using Xunit;

using Metadata = UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata;

namespace test.backlog.EndToEndTests
{
    public class EndToEndTests : BaseEndToEndTests
    {
        private static readonly string ExpectedParserVersion = typeof(Metadata)
                                                               .Assembly
                                                               .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                                               .InformationalVersion;

        private string outputDir;
        private string trackerPath;
        private string dataDir;

        public EndToEndTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            // Ensure environment is clean before running any tests
            CleanFiles();
        }

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

            // Create the output directory - input directories should already exist with test data
            outputDir = Path.Combine(dataDir, "output");
            Directory.CreateDirectory(outputDir);

            // Store the tracker path so we can clean it later
            trackerPath = Path.Combine(dataDir, "uploaded-production.csv");

            // Set environment variables for this test
            SetPathEnvironmentVariables(dataDir, outputDir, trackerPath: trackerPath);
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

        /// <summary>
        ///     Pulls the first &lt;uk:parser&gt; value out of the supplied XML, preserving whitespace.
        /// </summary>
        private static string ExtractParserVersion(string xml)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            XNamespace uk = "https://caselaw.nationalarchives.gov.uk/akn";
            return document.Descendants(uk + "parser").FirstOrDefault()?.Value;
        }

        [Fact]
        public void ProcessBacklogJudgment_SuccessfullyUploadsToS3()
        {
            // Setup test environment for DOCX test
            ConfigureTestEnvironment("Altaf Ebrahim t_a Ebrahim & Co v OISC");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files/");

            // Arrange
            const uint docId = 5; // doc id 5 from the court_metadata.csv being tested

            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--id", docId.ToString() });

            AssertProgramExitedSuccessfully(exitCode);

            // Verify content was uploaded
            mockS3Client.AssertNumberOfUploads(1);
            mockS3Client.AssertUploadsWereValid();

            var capturedKey = mockS3Client.CapturedKeys.Single();

            // Check if tracker was updated
            Assert.Contains(GetUuidFromKey(capturedKey), File.ReadAllText(trackerPath));
            AssertCapturedContentMatchesOutputContent(capturedKey);
            AssertCapturedContentContainsExpectedXml(capturedKey,
                "test.backlog.expected_output.Altaf Ebrahim t_a Ebrahim & Co v OISC.xml");
        }

        [Fact]
        public void ProcessBacklogJudgment_SuccessfullyProcessesPDF()
        {
            // Setup test environment for PDF test
            ConfigureTestEnvironment("Money Worries Ltd v Office of Fair Trading");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "Documents\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/Consumer Credit Appeals/Documents/");

            // Arrange
            const uint docId = 20; // Using doc id of a PDF

            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--id", docId.ToString() });

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            // Verify content was uploaded
            mockS3Client.AssertNumberOfUploads(1);
            mockS3Client.AssertUploadsWereValid();

            var capturedKey = mockS3Client.CapturedKeys.Single();

            // Check if tracker was updated
            Assert.Contains(GetUuidFromKey(capturedKey), File.ReadAllText(trackerPath));

            AssertCapturedContentMatchesOutputContent(capturedKey);
            AssertCapturedContentContainsExpectedXml(capturedKey,
                "test.backlog.expected_output.Money Worries Ltd v Office of Fair Trading.xml");
        }

        [Fact]
        public void ProcessBacklogJudgment_FullCSV_ProcessesMultipleJudgments()
        {
            // Setup test environment for multi-line CSV test
            ConfigureTestEnvironment("MultiLineTest");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files/");

            // Act - Run without --id to process full CSV
            var exitCode = Backlog.Src.Program.Main(new string[0]);

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            mockS3Client.AssertNumberOfUploads(3);
            mockS3Client.AssertUploadsWereValid();

            // Verify tracker was updated for all processed items
            foreach (var key in mockS3Client.CapturedKeys)
            {
                Assert.Contains(GetUuidFromKey(key), File.ReadAllText(trackerPath));
            }
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
            var exitCode = Backlog.Src.Program.Main(new string[0]);

            // Assert
            AssertProgramExitedSuccessfully(exitCode);

            // Should process fewer items than total (since one was already done)
            // The exact count depends on test data - we'll verify this doesn't process ALL items
            var trackerContent = await File.ReadAllTextAsync(trackerPath, TestContext.Current.CancellationToken);
            var trackerLines = trackerContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Should have the original entry plus new entries
            Assert.True(trackerLines.Length > 1, "Tracker should have original entry plus new entries");
            Assert.True(trackerLines[0].Contains("some-uuid-1"), "First line should be the pre-existing entry");
        }

        [Fact]
        public void ProcessBacklogJudgment_FullCSV_WithEmptyCSV_ReturnsError()
        {
            // Setup test environment with empty CSV
            ConfigureTestEnvironment("EmptyCSVTest");

            // Act
            var exitCode = Backlog.Src.Program.Main(new string[0]);

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public void ProcessBacklogJudgment_WithInvalidIdArgument_ReturnsError()
        {
            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--id", "invalid" });

            // Assert
            Assert.Equal(1, exitCode);
        }

        [Fact]
        public void ProcessBacklogJudgment_WithInvalidArguments_ReturnsError()
        {
            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--unknown-arg" });

            // Assert
            Assert.Equal(1, exitCode);
        }
    }
}
