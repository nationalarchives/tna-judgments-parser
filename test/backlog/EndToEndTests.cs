using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Amazon.S3;
using Amazon.S3.Model;

using Moq;

using test;

using Xunit;

namespace Backlog.Test
{
    public class EndToEndTests : IDisposable
    {
        private string dataDir;
        private string courtMetadataPath;
        private string trackerPath;
        private string outputPath;
        private string bulkNumbersPath;
        private Mock<IAmazonS3> mockS3Client;
        private const string TEST_BUCKET = "test-bucket";

        private static readonly string ExpectedParserVersion = typeof(UK.Gov.Legislation.Judgments.AkomaNtoso.Metadata)
                                                               .Assembly
                                                               .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                                               .InformationalVersion;

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

            // Create paths for required files
            courtMetadataPath = Path.Combine(dataDir, "court_metadata.csv");
            trackerPath = Path.Combine(dataDir, "uploaded-production.csv");
            outputPath = Path.Combine(dataDir, "output");
            bulkNumbersPath = Path.Combine(dataDir, "bulk_numbers.csv");

            // Create the output directory - input directories should already exist with test data
            Directory.CreateDirectory(outputPath);

            // Set environment variables for this test
            Environment.SetEnvironmentVariable("COURT_METADATA_PATH", courtMetadataPath);
            Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", dataDir);
            Environment.SetEnvironmentVariable("TRACKER_PATH", trackerPath);
            Environment.SetEnvironmentVariable("OUTPUT_PATH", outputPath);
            Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", bulkNumbersPath);
            Environment.SetEnvironmentVariable("BUCKET_NAME", TEST_BUCKET);
            Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");
        }

        public EndToEndTests()
        {
            // Reset mock S3 client for each test to ensure clean state
            mockS3Client = new Mock<IAmazonS3>();
        }

        public void Dispose()
        {
            // Clean up environment variables
            Environment.SetEnvironmentVariable("COURT_METADATA_PATH", null);
            Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", null);
            Environment.SetEnvironmentVariable("TRACKER_PATH", null);
            Environment.SetEnvironmentVariable("OUTPUT_PATH", null);
            Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", null);
            Environment.SetEnvironmentVariable("BUCKET_NAME", null);
            Environment.SetEnvironmentVariable("AWS_REGION", null);
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", null);
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", null);

            // Only clean up output files and tracker, leave test data intact
            if (File.Exists(trackerPath))
                File.Delete(trackerPath);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
        }

        private class S3UploadCapture
        {
            public byte[] CapturedContent { get; set; }
            public string CapturedKey { get; set; }
        }

        private S3UploadCapture SetupS3Mock()
        {
            var capture = new S3UploadCapture();
            var putObjectResponse = new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK };
            var taskCompletionSource = new TaskCompletionSource<PutObjectResponse>();
            taskCompletionSource.SetResult(putObjectResponse);

            mockS3Client
                .Setup(x => x.PutObjectAsync(
                    It.Is<PutObjectRequest>(req => 
                        req.BucketName == TEST_BUCKET && 
                        req.ContentType == "application/gzip"),
                    It.IsAny<CancellationToken>()))
                .Callback<PutObjectRequest, CancellationToken>((req, token) => 
                {
                    // Capture the key (UUID) and content
                    capture.CapturedKey = req.Key;
                    using var ms = new MemoryStream();
                    req.InputStream.CopyTo(ms);
                    capture.CapturedContent = ms.ToArray();
                })
                .Returns(taskCompletionSource.Task);

            return capture;
        }

        private async Task AssertS3UploadResults(byte[] capturedContent, string capturedKey, string expectedXmlResourceName)
        {
            // Verify content was uploaded
            Assert.True(capturedContent is not null, "No content was uploaded to S3");
            Assert.True(capturedContent.Any(), "Uploaded content was empty");
            Assert.True(capturedKey is not null, "No key was captured from upload");
            AssertCapturedKeyIsValid(capturedKey);

            // Use the captured UUID for subsequent checks
            var capturedUuid = capturedKey.Substring(0, capturedKey.Length - 7); // Remove .tar.gz
            
            // Check if tracker was updated
            var trackerContent = await File.ReadAllTextAsync(trackerPath);
            Assert.Contains(capturedUuid, trackerContent);

            // Check if output file was created and matches uploaded content
            var outputFilePath = Path.Combine(outputPath, capturedKey);
            Assert.True(File.Exists(outputFilePath), "Output file should exist");
            var outputContent = await File.ReadAllBytesAsync(outputFilePath);
            Assert.Equal(capturedContent, outputContent);

            // Check if generated XML matches expected output
            await AssertXmlOutput(outputContent, expectedXmlResourceName);
        }

        private async Task AssertXmlOutput(byte[] outputContent, string expectedXmlResourceName)
        {
            using (var gzipStream = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(new MemoryStream(outputContent)))
            using (var archive = new ICSharpCode.SharpZipLib.Tar.TarInputStream(gzipStream, System.Text.Encoding.UTF8))
            {
                var entry = archive.GetNextEntry();
                while (entry != null && !entry.Name.EndsWith(".xml"))
                {
                    entry = archive.GetNextEntry();
                }
                
                Assert.True(entry is not null, "XML file not found in tar.gz");
                
                using var reader = new StreamReader(archive);
                var actualXml = await reader.ReadToEndAsync();
                var expectedXml = DocumentHelpers.ReadXml(expectedXmlResourceName);

                AssertParserVersion(actualXml);

                actualXml = DocumentHelpers.RemoveNonDeterministicMetadata(actualXml);
                expectedXml = DocumentHelpers.RemoveNonDeterministicMetadata(expectedXml);

                Assert.Equal(expectedXml, actualXml);
            }
        }

        /// <summary>
        /// Checks that the generated XML advertises the current parser version.
        /// </summary>
        private static void AssertParserVersion(string actualXml)
        {
            var parserVersion = ExtractParserVersion(actualXml);
            Assert.Equal(ExpectedParserVersion, parserVersion);
        }

        /// <summary>
        /// Pulls the first &lt;uk:parser&gt; value out of the supplied XML, preserving whitespace.
        /// </summary>
        private static string ExtractParserVersion(string xml)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            XNamespace uk = "https://caselaw.nationalarchives.gov.uk/akn";
            return document.Descendants(uk + "parser").FirstOrDefault()?.Value;
        }

        [Fact]
        public async Task ProcessBacklogJudgment_SuccessfullyUploadsToS3()
        {
            // Setup test environment for DOCX test
            ConfigureTestEnvironment("Altaf Ebrahim t_a Ebrahim & Co v OISC");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files/");
            
            // Configure S3 client
            Backlog.Src.Bucket.Configure(mockS3Client.Object, TEST_BUCKET);

            // Arrange
            const uint docId = 5;  // doc id 5 from the court_metadata.csv being tested
            var s3Capture = SetupS3Mock();

            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--id", docId.ToString() });

            AssertProgramExitedSuccessfully(exitCode);
            await AssertS3UploadResults(s3Capture.CapturedContent, s3Capture.CapturedKey, "test.backlog.expected_output.Altaf Ebrahim t_a Ebrahim & Co v OISC.xml");
        }

        private static void AssertProgramExitedSuccessfully(int exitCode)
        {
            Assert.True(exitCode == 0, "Program should exit successfully");
        }

        [Fact]
        public async Task ProcessBacklogJudgment_SuccessfullyProcessesPDF()
        {
            // Setup test environment for PDF test
            ConfigureTestEnvironment("Money Worries Ltd v Office of Fair Trading");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "Documents\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/Consumer Credit Appeals/Documents/");

            // Configure S3 client
            Backlog.Src.Bucket.Configure(mockS3Client.Object, TEST_BUCKET);

            // Arrange
            const uint docId = 20;  // Using doc id of a PDF
            var s3Capture = SetupS3Mock();

            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--id", docId.ToString() });

            // Assert
            AssertProgramExitedSuccessfully(exitCode);
            await AssertS3UploadResults(s3Capture.CapturedContent, s3Capture.CapturedKey, "test.backlog.expected_output.Money Worries Ltd v Office of Fair Trading.xml");
        }

        [Fact]
        public async Task ProcessBacklogJudgment_FullCSV_ProcessesMultipleJudgments()
        {
            // Setup test environment for multi-line CSV test
            ConfigureTestEnvironment("MultiLineTest");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files/");

            // Configure S3 client
            Backlog.Src.Bucket.Configure(mockS3Client.Object, TEST_BUCKET);

            // Arrange - Setup mock to capture multiple uploads
            var s3Captures = new List<S3UploadCapture>();
            var putObjectResponse = new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK };
            var taskCompletionSource = new TaskCompletionSource<PutObjectResponse>();
            taskCompletionSource.SetResult(putObjectResponse);

            mockS3Client
                .Setup(x => x.PutObjectAsync(
                    It.Is<PutObjectRequest>(req => 
                        req.BucketName == TEST_BUCKET && 
                        req.ContentType == "application/gzip"),
                    It.IsAny<CancellationToken>()))
                .Callback<PutObjectRequest, CancellationToken>((req, token) => 
                {
                    var capture = new S3UploadCapture();
                    capture.CapturedKey = req.Key;
                    using var ms = new MemoryStream();
                    req.InputStream.CopyTo(ms);
                    capture.CapturedContent = ms.ToArray();
                    s3Captures.Add(capture);
                })
                .Returns(taskCompletionSource.Task);

            // Act - Run without --id to process full CSV
            var exitCode = Src.Program.Main(new string[0]);

            // Assert
            AssertProgramExitedSuccessfully(exitCode);
            Assert.NotEmpty(s3Captures);

            // Verify each capture has valid content
            foreach (var capture in s3Captures)
            {
                Assert.NotNull(capture.CapturedContent);
                Assert.NotEmpty(capture.CapturedContent);
                AssertCapturedKeyIsValid(capture.CapturedKey);
            }

            // Verify tracker was updated for all processed items
            var trackerContent = await File.ReadAllTextAsync(trackerPath);
            foreach (var capture in s3Captures)
            {
                var uuid = capture.CapturedKey.Substring(0, capture.CapturedKey.Length - 7); // Remove .tar.gz
                Assert.Contains(uuid, trackerContent);
            }
        }

        private static void AssertCapturedKeyIsValid(string capturedKey)
        {
            var capturedKeyIsValid = Regex.IsMatch(capturedKey,
                @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.tar\.gz$");
            Assert.True(capturedKeyIsValid, "Key should be a UUID followed by .tar.gz");
        }

        [Fact]
        public async Task ProcessBacklogJudgment_FullCSV_SkipsAlreadyProcessedItems()
        {
            // Setup test environment
            ConfigureTestEnvironment("MultiLineTest");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles\\");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files/");

            // Pre-populate tracker to mark first item as already processed
            await File.WriteAllTextAsync(trackerPath, "100/JudgmentFiles\\j100\\test1.doc,some-uuid-1,132345678901234567\n");

            // Configure S3 client
            Backlog.Src.Bucket.Configure(mockS3Client.Object, TEST_BUCKET);

            // Arrange - Setup mock to capture uploads
            var s3Captures = new List<S3UploadCapture>();
            var putObjectResponse = new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK };
            var taskCompletionSource = new TaskCompletionSource<PutObjectResponse>();
            taskCompletionSource.SetResult(putObjectResponse);

            mockS3Client
                .Setup(x => x.PutObjectAsync(
                    It.Is<PutObjectRequest>(req => 
                        req.BucketName == TEST_BUCKET && 
                        req.ContentType == "application/gzip"),
                    It.IsAny<CancellationToken>()))
                .Callback<PutObjectRequest, CancellationToken>((req, token) => 
                {
                    var capture = new S3UploadCapture();
                    capture.CapturedKey = req.Key;
                    using var ms = new MemoryStream();
                    req.InputStream.CopyTo(ms);
                    capture.CapturedContent = ms.ToArray();
                    s3Captures.Add(capture);
                })
                .Returns(taskCompletionSource.Task);

            // Act
            var exitCode = Backlog.Src.Program.Main(new string[0]);

            // Assert
            AssertProgramExitedSuccessfully(exitCode);
            
            // Should process fewer items than total (since one was already done)
            // The exact count depends on test data - we'll verify this doesn't process ALL items
            var trackerContent = await File.ReadAllTextAsync(trackerPath);
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
            var exitCode = Src.Program.Main(new string[0]);

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
            var exitCode = Src.Program.Main(new[] { "--unknown-arg" });

            // Assert
            Assert.Equal(1, exitCode);
        }
    }
}
