using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Backlog.Test
{
    [TestFixture]
    public class EndToEndTests : IDisposable
    {
        private string tempDir;
        private string dataDir;
        private string courtDocsDir;
        private string tdrMetadataDir;
        private string courtMetadataPath;
        private string trackerPath;
        private string outputPath;
        private string bulkNumbersPath;
        private Mock<IAmazonS3> mockS3Client;
        private const string TEST_BUCKET = "test-bucket";

        [SetUp]
        public void SetUp()
        {
            // Use the test data directory with pre-populated files
            tempDir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "backlog", "test-data"));
            dataDir = tempDir;
            courtDocsDir = Path.Combine(dataDir, "court_documents");
            tdrMetadataDir = Path.Combine(dataDir, "tdr_metadata");
            
            // Create paths for required files
            courtMetadataPath = Path.Combine(tempDir, "court_metadata.csv");
            trackerPath = Path.Combine(tempDir, "uploaded-production.csv");
            outputPath = Path.Combine(tempDir, "output");
            bulkNumbersPath = Path.Combine(tempDir, "bulk_numbers.csv");

            // Create output directory - other directories should already exist
            Directory.CreateDirectory(outputPath);

            // Set up mock S3 client
            mockS3Client = new Mock<IAmazonS3>();
            Backlog.Src.Bucket.Configure(mockS3Client.Object, TEST_BUCKET);

            // Ensure we're using a clean environment for each test
            Environment.SetEnvironmentVariable("COURT_METADATA_PATH", null);
            Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", null);
            Environment.SetEnvironmentVariable("TRACKER_PATH", null);
            Environment.SetEnvironmentVariable("OUTPUT_PATH", null);
            Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", null);
            Environment.SetEnvironmentVariable("BUCKET_NAME", null);

            // Set environment variables for this test
            Environment.SetEnvironmentVariable("COURT_METADATA_PATH", courtMetadataPath);
            Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", dataDir);
            Environment.SetEnvironmentVariable("TRACKER_PATH", trackerPath);
            Environment.SetEnvironmentVariable("OUTPUT_PATH", outputPath);
            Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", bulkNumbersPath);
            Environment.SetEnvironmentVariable("BUCKET_NAME", TEST_BUCKET);
        }

        [OneTimeTearDown]
        public void TearDown()
        {            
            // Clean up environment variables
            Environment.SetEnvironmentVariable("COURT_METADATA_PATH", null);
            Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", null);
            Environment.SetEnvironmentVariable("TRACKER_PATH", null);
            Environment.SetEnvironmentVariable("OUTPUT_PATH", null);
            Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", null);
            Environment.SetEnvironmentVariable("BUCKET_NAME", null);

            // Reset Bucket configuration
            Backlog.Src.Bucket.ResetConfiguration();
            
            // Only clean up output files and tracker, leave test data intact
            if (File.Exists(trackerPath))
                File.Delete(trackerPath);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
        }

        [Test]
        public async Task ProcessBacklogJudgment_SuccessfullyUploadsToS3()
        {
            // Arrange
            const uint courtId = 5;  // From your court_metadata.csv

            // Configure mock S3 client to capture the uploaded content
            byte[] capturedContent = null;
            string capturedKey = null;
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
                    capturedKey = req.Key;
                    using var ms = new MemoryStream();
                    req.InputStream.CopyTo(ms);
                    capturedContent = ms.ToArray();
                })
                .Returns(taskCompletionSource.Task);

            // Act
            var exitCode = Backlog.Src.Program.Main(new[] { "--id", courtId.ToString() });

            // Assert
            Assert.That(exitCode, Is.EqualTo(0), "Program should exit successfully");

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
                               "Altaf Ebrahim t_a Ebrahim & Co v OISC.xml"));
                
                // Remove timestamps that will differ
                actualXml = System.Text.RegularExpressions.Regex.Replace(actualXml, 
                    @"date=""\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}""", "date=\"TIMESTAMP\"");
                expectedXml = System.Text.RegularExpressions.Regex.Replace(expectedXml,
                    @"date=""\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}""", "date=\"TIMESTAMP\"");
                
                Assert.That(actualXml, Is.EqualTo(expectedXml), "Generated XML does not match expected output");
            }
        }

        // [Test]
        // public async Task ProcessBacklogJudgment_HandlesFailedUpload()
        // {
        //     // Arrange
        //     const string judgmentContent = "Test judgment content";
        //     const string uuid = "test-judgment-uuid";
        //     const uint courtId = 2;

        //     // Configure mock S3 client to simulate failure
        //     mockS3Client
        //         .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
        //         .ThrowsAsync(new AmazonS3Exception("Test upload failure"));

        //     // Test files should already exist in test-data directory

        //     // Act
        //     var exitCode = Backlog.Src.Program.Main(new[] { "--id", "2" });

        //     // Assert
        //     Assert.That(exitCode, Is.EqualTo(1), "Program should exit with error");

        //     // Verify S3 upload was attempted
        //     mockS3Client.Verify(x => x.PutObjectAsync(
        //         It.Is<PutObjectRequest>(req => req.BucketName == TEST_BUCKET && req.Key == $"{uuid}.tar.gz"),
        //         It.IsAny<CancellationToken>()
        //     ), Times.Once);

        //     // Check if tracker was not updated
        //     var trackerContent = await File.ReadAllTextAsync(trackerPath);
        //     StringAssert.DoesNotContain(uuid, trackerContent, "Tracker should not contain the UUID on failure");
        // }

        private void CreateCourtMetadataFile(dynamic[] entries)
        {
            var csv = new StringBuilder();
            csv.AppendLine("id,created_datetime,publication_datetime,last_updatedtime,decision_datetime,file_no_1,file_no_2,file_no_3,claimants,respondent,headnote_summary,is_published,main_subcategory_description,sec_subcategory_description,Name,FilePath,Extension,SizeInMB,FileLastEditTime");
            foreach (var entry in entries)
            {
                // Make sure FilePath matches the format expected in CreateFileMetadataFile
                csv.AppendLine($"{entry.Id},,,,{entry.Date} 10:00:00,FN1,FN2,FN3,Test Claimant,Test Respondent,,true,{entry.Name},,Test Case,JudgmentFiles/test.docx,.docx,1.0,2025-01-01 10:00:00");
            }
            File.WriteAllText(courtMetadataPath, csv.ToString());
        }

        private void CreateFileMetadataFile(params (string FilePath, string Uuid)[] entries)
        {
            var metadataPath = Path.Combine(tdrMetadataDir, "file-metadata.csv");
            var csv = new StringBuilder();
            // No header needed as Files.cs doesn't expect one
            foreach (var entry in entries)
            {
                var hmctsPath = entry.FilePath
                    .Replace("JudgmentFiles", "data/HMCTS_Judgment_Files")
                    .Replace('\\', '/');
                csv.AppendLine($"{hmctsPath},{entry.Uuid}");
            }
            File.WriteAllText(metadataPath, csv.ToString());
        }

        public void Dispose()
        {
            mockS3Client?.Object?.Dispose();
        }
    }
}
