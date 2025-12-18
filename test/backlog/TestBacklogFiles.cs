using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Backlog.Src;

using Microsoft.Extensions.Logging;

using test.Mocks;

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

        private BacklogFiles backlogFiles;
        private ILogger<BacklogFiles> mockLogger = new MockLogger<BacklogFiles>().Object;

        public TestBacklogFiles()
        {
            // Create a temporary directory for test files
            _tempTestDir = Path.Combine(Path.GetTempPath(), $"FilesTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempTestDir);

            _courtDocumentsDir = Path.Combine(_tempTestDir, "court_documents");
            _tdrMetadataDir = Path.Combine(_tempTestDir, "tdr_metadata");
            
            Directory.CreateDirectory(_courtDocumentsDir);
            Directory.CreateDirectory(_tdrMetadataDir);
            
            backlogFiles = new(mockLogger, _tempTestDir, "", "");
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
        public void FindUuidInTransferMetadata_WithTwoLevelDeepJudgmentsFilePath_ReturnsUuidSuccessfully()
        {
            // Arrange
            const string judgmentsFilePath = @"Documents\Decisions";
            const string hmctsFilePath = "data/Claims management decisions";
            const string expectedUuid = "test-uuid-12345";  
            
            backlogFiles = new(mockLogger, _tempTestDir, judgmentsFilePath, hmctsFilePath);

            // Create transfer metadata CSV content with 2-level deep judgments file path
            var transferMetadataContent = new StringBuilder();
            transferMetadataContent.AppendLine("file_reference,file_name,file_type,file_size,clientside_original_filepath,rights_copyright,legal_status,held_by,date_last_modified,closure_type,closure_start_date,closure_period,foi_exemption_code,foi_exemption_asserted,title_closed,title_alternate,description,description_closed,description_alternate,language,end_date,file_name_translation,original_filepath,parent_reference,former_reference_department,UUID");
            transferMetadataContent.AppendLine($"TEST1,test-decision.pdf,File,1024,{hmctsFilePath}/j100/test-decision.pdf,Crown Copyright,Public Record(s),\"The National Archives, Kew\",2023-01-01T00:00:00,Open,,,,,false,,,false,,English,,,,,,{expectedUuid}");

            // Write the transfer metadata file
            var transferMetadataPath = Path.Combine(_tdrMetadataDir, "file-metadata.csv");
            File.WriteAllText(transferMetadataPath, transferMetadataContent.ToString());

            // Act
            var result = backlogFiles.FindUuidInTransferMetadata(@"Documents\Decisions\j100\test-decision.pdf");

            // Assert
            Assert.Equal(expectedUuid, result);
        }

        /// <summary>
        /// Tests that GetUuid works with various path separators and handles path normalization correctly
        /// when JUDGMENTS_FILE_PATH has multiple levels.
        /// </summary>
        [Fact]
        public void FindUuidInTransferMetadata_WithMixedPathSeparators_HandlesNormalizationCorrectly()
        {
            // Arrange - Set up test data with mixed path separators
            const string judgmentsFilePath = @"Documents\Decisions";
            const string hmctsFilePath = "data/Claims management decisions";
            const string expectedUuid = "test-uuid-mixed-paths";

            backlogFiles = new(mockLogger, _tempTestDir, judgmentsFilePath, hmctsFilePath);

            // Create transfer metadata with forward slashes (Unix style) - should still match
            var transferMetadataContent = new StringBuilder();
            transferMetadataContent.AppendLine("file_reference,file_name,file_type,file_size,clientside_original_filepath,rights_copyright,legal_status,held_by,date_last_modified,closure_type,closure_start_date,closure_period,foi_exemption_code,foi_exemption_asserted,title_closed,title_alternate,description,description_closed,description_alternate,language,end_date,file_name_translation,original_filepath,parent_reference,former_reference_department,UUID");
            transferMetadataContent.AppendLine($"TEST2,test-file.doc,File,2048,{hmctsFilePath}/subfolder/test-file.doc,Crown Copyright,Public Record(s),\"The National Archives, Kew\",2023-01-01T00:00:00,Open,,,,,false,,,false,,English,,,,,,{expectedUuid}");

            // Write the transfer metadata file
            var transferMetadataPath = Path.Combine(_tdrMetadataDir, "file-metadata.csv");
            File.WriteAllText(transferMetadataPath, transferMetadataContent.ToString());

            // Act
            var result = backlogFiles.FindUuidInTransferMetadata(@"Documents\Decisions\subfolder\test-file.doc");

            // Assert
            Assert.Equal(expectedUuid, result);
        }

        /// <summary>
        /// Tests that when JUDGMENTS_FILE_PATH doesn't match the metadata file path, 
        /// an appropriate exception is thrown.
        /// </summary>
        [Fact]
        public void FindUuidInTransferMetadata_WithMismatchedJudgmentsFilePath_ThrowsArgumentException()
        {
            // Arrange
            const string judgmentsFilePath = @"Documents\Decisions";
            const string hmctsFilePath = "data/Claims management decisions";

            backlogFiles = new(mockLogger, _tempTestDir, judgmentsFilePath, hmctsFilePath);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                backlogFiles.FindUuidInTransferMetadata( @"Different\Path\j100\test-file.pdf");
            });

            Assert.Contains("must start with", exception.Message);
        }
    }
}
