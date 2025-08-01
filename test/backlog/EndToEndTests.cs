using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Backlog.Test
{
    [TestFixture]
    public class EndToEndTests
    {
        private string dataDir;
        private string courtMetadataPath;
        private string trackerPath;
        private string outputPath;
        private string bulkNumbersPath;
        private Mock<IAmazonS3> mockS3Client;
        private const string TEST_BUCKET = "test-bucket";

        private void ConfigureTestEnvironment(string testCaseName)
        {
            // Use the test data directory with pre-populated files
            dataDir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "backlog", "test-data", testCaseName));
            
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

        [SetUp]
        public void SetUp()
        {
            // Reset mock S3 client for each test to ensure clean state
            mockS3Client = new Mock<IAmazonS3>();
        }

        [TearDown]
        public void TearDown()
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

        private async Task AssertS3UploadResults(byte[] capturedContent, string capturedKey, string expectedXmlFileName)
        {
            // Verify content was uploaded
            Assert.That(capturedContent, Is.Not.Null, "No content was uploaded to S3");
            Assert.That(capturedContent.Length, Is.GreaterThan(0), "Uploaded content was empty");
            Assert.That(capturedKey, Is.Not.Null, "No key was captured from upload");
            Assert.That(capturedKey, Does.Match(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.tar\.gz$"), 
                "Key should be a UUID followed by .tar.gz");

            // Use the captured UUID for subsequent checks
            var capturedUuid = capturedKey.Substring(0, capturedKey.Length - 7); // Remove .tar.gz
            
            // Check if tracker was updated
            var trackerContent = await File.ReadAllTextAsync(trackerPath);
            StringAssert.Contains(capturedUuid, trackerContent, "Tracker should contain the UUID");

            // Check if output file was created and matches uploaded content
            var outputFilePath = Path.Combine(outputPath, capturedKey);
            Assert.That(File.Exists(outputFilePath), Is.True, "Output file should exist");
            var outputContent = await File.ReadAllBytesAsync(outputFilePath);
            Assert.That(outputContent, Is.EqualTo(capturedContent), "Output file should match uploaded content");

            // Check if generated XML matches expected output
            await AssertXmlOutput(outputContent, expectedXmlFileName);
        }

        private async Task AssertXmlOutput(byte[] outputContent, string expectedXmlFileName)
        {
            using (var gzipStream = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(new MemoryStream(outputContent)))
            using (var archive = new ICSharpCode.SharpZipLib.Tar.TarInputStream(gzipStream, System.Text.Encoding.UTF8))
            {
                var entry = archive.GetNextEntry();
                while (entry != null && !entry.Name.EndsWith(".xml"))
                {
                    entry = archive.GetNextEntry();
                }
                
                Assert.That(entry, Is.Not.Null, "XML file not found in tar.gz");
                
                using var reader = new StreamReader(archive);
                var actualXml = await reader.ReadToEndAsync();
                var expectedXml = await File.ReadAllTextAsync(
                    Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "backlog", "expected-output", 
                               expectedXmlFileName));
                
                // Remove timestamps that will differ
                actualXml = NormalizeTimestamps(actualXml);
                expectedXml = NormalizeTimestamps(expectedXml);
                
                Assert.That(actualXml, Is.EqualTo(expectedXml), "Generated XML does not match expected output");
            }
        }

        private string NormalizeTimestamps(string xml)
        {
            return System.Text.RegularExpressions.Regex.Replace(xml, 
                @"date=""\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}""", "date=\"TIMESTAMP\"");
        }

        [Test]
        public async Task ProcessBacklogJudgment_SuccessfullyUploadsToS3()
        {
            // Setup test environment for DOCX test
            ConfigureTestEnvironment("Altaf Ebrahim t_a Ebrahim & Co v OISC");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "JudgmentFiles");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/HMCTS_Judgment_Files");
            
            // Configure S3 client
            Backlog.Src.Bucket.Configure(mockS3Client.Object, TEST_BUCKET);

            // Arrange
            const uint docId = 5;  // doc id 5 from the court_metadata.csv being tested
            var s3Capture = SetupS3Mock();

            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--id", docId.ToString() });

            // Assert
            Assert.That(exitCode, Is.EqualTo(0), "Program should exit successfully");
            await AssertS3UploadResults(s3Capture.CapturedContent, s3Capture.CapturedKey, "Altaf Ebrahim t_a Ebrahim & Co v OISC.xml");
        }

        [Test]
        public async Task ProcessBacklogJudgment_SuccessfullyProcessesPDF()
        {
            // Setup test environment for PDF test
            ConfigureTestEnvironment("Money Worries Ltd v Office of Fair Trading");
            Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", "Documents");
            Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", "data/Consumer Credit Appeals/Documents");

            // Configure S3 client
            Backlog.Src.Bucket.Configure(mockS3Client.Object, TEST_BUCKET);

            // Arrange
            const uint docId = 20;  // Using doc id of a PDF
            var s3Capture = SetupS3Mock();

            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--id", docId.ToString() });

            // Assert
            Assert.That(exitCode, Is.EqualTo(0), "Program should exit successfully");
            await AssertS3UploadResults(s3Capture.CapturedContent, s3Capture.CapturedKey, "Money Worries Ltd v Office of Fair Trading.xml");
        }

    }
}
