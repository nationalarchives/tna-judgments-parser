using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Backlog.Src;

using Xunit;

namespace test.backlog
{
    /// <summary>
    /// Tests for the Files class focusing on file path handling and UUID resolution.
    /// </summary>
    public class TestBacklogFiles : IDisposable
    {
        private string _tempTestDir;
        private string _courtDocumentsDir;
        private string _tdrMetadataDir;

        public TestBacklogFiles()
        {
            // Create a temporary directory for test files
            _tempTestDir = Path.Combine(Path.GetTempPath(), $"FilesTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempTestDir);

            _courtDocumentsDir = Path.Combine(_tempTestDir, "court_documents");
            _tdrMetadataDir = Path.Combine(_tempTestDir, "tdr_metadata");

            Directory.CreateDirectory(_courtDocumentsDir);
            Directory.CreateDirectory(_tdrMetadataDir);
        }

        public void Dispose()
        {
            // Clean up test files
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }

        /// <summary>
        /// Tests that GetUuid can handle when JUDGMENTS_FILE_PATH is 2 levels deep (e.g., "Documents\Decisions")
        /// and still successfully resolve UUIDs without error.
        /// </summary>
        [Fact]
        public void TestGetUuid_WithTwoLevelDeepJudgmentsFilePath_ReturnsUuidSuccessfully()
        {
            // Arrange - Set up test data with 2-level deep judgments file path
            const string judgmentsFilePath = @"Documents\Decisions";
            const string hmctsFilePath = "data/Claims management decisions";
            const string expectedUuid = "test-uuid-12345";

            // Create a test metadata line with a file path that includes the 2-level judgments path
            var metadataLine = new Metadata.Line
            {
                FilePath = @"Documents\Decisions\j100\test-decision.pdf",
                Extension = ".pdf"
            };

            // Create transfer metadata CSV content that matches the expected structure
            var transferMetadataContent = new StringBuilder();
            transferMetadataContent.AppendLine("file_reference,file_name,file_type,file_size,clientside_original_filepath,rights_copyright,legal_status,held_by,date_last_modified,closure_type,closure_start_date,closure_period,foi_exemption_code,foi_exemption_asserted,title_closed,title_alternate,description,description_closed,description_alternate,language,end_date,file_name_translation,original_filepath,parent_reference,former_reference_department,UUID");
            transferMetadataContent.AppendLine($"TEST1,test-decision.pdf,File,1024,{hmctsFilePath}/j100/test-decision.pdf,Crown Copyright,Public Record(s),\"The National Archives, Kew\",2023-01-01T00:00:00,Open,,,,,false,,,false,,English,,,,,,{expectedUuid}");

            // Write the transfer metadata file
            var transferMetadataPath = Path.Combine(_tdrMetadataDir, "file-metadata.csv");
            File.WriteAllText(transferMetadataPath, transferMetadataContent.ToString());

            // Create a dummy court document file
            var courtDocumentPath = Path.Combine(_courtDocumentsDir, expectedUuid);
            File.WriteAllBytes(courtDocumentPath, new byte[] { 1, 2, 3, 4 });

            // Act & Assert - This should not throw an exception
            byte[] fileContent = BacklogFiles.ReadFile(_tempTestDir, metadataLine, judgmentsFilePath, hmctsFilePath);

            // Verify that we actually got the file content
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, fileContent);
        }

        /// <summary>
        /// Tests that GetUuid works with various path separators and handles path normalization correctly
        /// when JUDGMENTS_FILE_PATH has multiple levels.
        /// </summary>
        [Fact]
        public void TestGetUuid_WithMixedPathSeparators_HandlesNormalizationCorrectly()
        {
            // Arrange - Set up test data with mixed path separators
            const string judgmentsFilePath = @"Documents\Decisions";
            const string hmctsFilePath = "data/Claims management decisions";
            const string expectedUuid = "test-uuid-mixed-paths";

            // Create metadata line with backslashes (Windows style)
            var metadataLine = new Metadata.Line
            {
                FilePath = @"Documents\Decisions\subfolder\test-file.doc",
                Extension = ".doc"
            };

            // Create transfer metadata with forward slashes (Unix style) - should still match
            var transferMetadataContent = new StringBuilder();
            transferMetadataContent.AppendLine("file_reference,file_name,file_type,file_size,clientside_original_filepath,rights_copyright,legal_status,held_by,date_last_modified,closure_type,closure_start_date,closure_period,foi_exemption_code,foi_exemption_asserted,title_closed,title_alternate,description,description_closed,description_alternate,language,end_date,file_name_translation,original_filepath,parent_reference,former_reference_department,UUID");
            transferMetadataContent.AppendLine($"TEST2,test-file.doc,File,2048,{hmctsFilePath}/subfolder/test-file.doc,Crown Copyright,Public Record(s),\"The National Archives, Kew\",2023-01-01T00:00:00,Open,,,,,false,,,false,,English,,,,,,{expectedUuid}");

            // Write the transfer metadata file
            var transferMetadataPath = Path.Combine(_tdrMetadataDir, "file-metadata.csv");
            File.WriteAllText(transferMetadataPath, transferMetadataContent.ToString());

            // Create dummy court document files (.doc files are stored as .docx)
            var courtDocumentPath = Path.Combine(_courtDocumentsDir, expectedUuid + ".docx");
            File.WriteAllBytes(courtDocumentPath, new byte[] { 5, 6, 7, 8, 9 });

            // Act & Assert - This should handle path normalization
            byte[] fileContent = BacklogFiles.ReadFile(_tempTestDir, metadataLine, judgmentsFilePath, hmctsFilePath);

            // Verify that we got the file content and .doc extension was handled correctly
            Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, fileContent);
        }

        /// <summary>
        /// Tests that when JUDGMENTS_FILE_PATH doesn't match the metadata file path, 
        /// an appropriate exception is thrown.
        /// </summary>
        [Fact]
        public void TestGetUuid_WithMismatchedJudgmentsFilePath_ThrowsArgumentException()
        {
            // Arrange
            const string judgmentsFilePath = @"Documents\Decisions";
            const string hmctsFilePath = "data/Claims management decisions";

            // Create metadata line with a path that doesn't start with judgmentsFilePath
            var metadataLine = new Metadata.Line
            {
                FilePath = @"Different\Path\j100\test-file.pdf",
                Extension = ".pdf"
            };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                BacklogFiles.ReadFile(_tempTestDir, metadataLine, judgmentsFilePath, hmctsFilePath);
            });

            Assert.Contains("must start with", exception.Message);
        }

        /// <summary>
        /// Tests the CopyAllFilesWithExtension method with 2-level deep judgments file path
        /// to ensure file copying works correctly.
        /// </summary>
        [Fact]
        public void TestCopyAllFilesWithExtension_WithTwoLevelDeepPath_CopiesFilesSuccessfully()
        {
            // Arrange
            const string judgmentsFilePath = @"Documents\Decisions";
            const string hmctsFilePath = "data/Claims management decisions";
            const string expectedUuid1 = "copy-test-uuid-1";
            const string expectedUuid2 = "copy-test-uuid-2";

            var metadataLines = new List<Metadata.Line>
            {
                new Metadata.Line
                {
                    FilePath = @"Documents\Decisions\folder1\file1.pdf",
                    Extension = ".pdf"
                },
                new Metadata.Line
                {
                    FilePath = @"Documents\Decisions\folder2\file2.doc",
                    Extension = ".doc"
                }
            };

            // Create transfer metadata CSV
            var transferMetadataContent = new StringBuilder();
            transferMetadataContent.AppendLine("file_reference,file_name,file_type,file_size,clientside_original_filepath,rights_copyright,legal_status,held_by,date_last_modified,closure_type,closure_start_date,closure_period,foi_exemption_code,foi_exemption_asserted,title_closed,title_alternate,description,description_closed,description_alternate,language,end_date,file_name_translation,original_filepath,parent_reference,former_reference_department,UUID");
            transferMetadataContent.AppendLine($"TEST1,file1.pdf,File,1024,{hmctsFilePath}/folder1/file1.pdf,Crown Copyright,Public Record(s),\"The National Archives, Kew\",2023-01-01T00:00:00,Open,,,,,false,,,false,,English,,,,,,{expectedUuid1}");
            transferMetadataContent.AppendLine($"TEST2,file2.doc,File,2048,{hmctsFilePath}/folder2/file2.doc,Crown Copyright,Public Record(s),\"The National Archives, Kew\",2023-01-01T00:00:00,Open,,,,,false,,,false,,English,,,,,,{expectedUuid2}");

            var transferMetadataPath = Path.Combine(_tdrMetadataDir, "file-metadata.csv");
            File.WriteAllText(transferMetadataPath, transferMetadataContent.ToString());

            // Create source court document files
            var sourcePath1 = Path.Combine(_courtDocumentsDir, expectedUuid1);
            var sourcePath2 = Path.Combine(_courtDocumentsDir, expectedUuid2 + ".docx"); // .doc files stored as .docx
            
            File.WriteAllBytes(sourcePath1, new byte[] { 10, 20, 30 });
            File.WriteAllBytes(sourcePath2, new byte[] { 40, 50, 60 });

            // Act
            BacklogFiles.CopyAllFilesWithExtension(_tempTestDir, metadataLines, judgmentsFilePath, hmctsFilePath);

            // Assert - Check that files were copied with correct extensions
            var targetPath1 = Path.Combine(_courtDocumentsDir, expectedUuid1 + ".pdf");
            var targetPath2 = Path.Combine(_courtDocumentsDir, expectedUuid2 + ".doc");

            Assert.True(File.Exists(targetPath1), "PDF file should be copied");
            Assert.True(File.Exists(targetPath2), "DOC file should be copied with .doc extension");

            // Verify file contents
            Assert.Equal(new byte[] { 10, 20, 30 }, File.ReadAllBytes(targetPath1));
            Assert.Equal(new byte[] { 40, 50, 60 }, File.ReadAllBytes(targetPath2));
        }
    }
}
