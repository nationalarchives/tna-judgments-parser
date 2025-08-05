using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Backlog.Src.Batch.One;
using CsvHelper;

namespace Backlog.Test
{
    [TestFixture]
    public class TestHelperFindLines
    {
        private Helper helper;
        private string testDataDirectory;
        private string validCsvPath;
        private string invalidCsvPath;
        
        [SetUp]
        public void Setup()
        {
            // Create a temporary directory for test files
            testDataDirectory = Path.Combine(Path.GetTempPath(), "TestHelperFindLines", Guid.NewGuid().ToString());
            Directory.CreateDirectory(testDataDirectory);
            
            // Create valid CSV file with required columns
            validCsvPath = Path.Combine(testDataDirectory, "valid-metadata.csv");
            CreateValidCsvFile(validCsvPath);
            
            // Create invalid CSV file missing required columns
            invalidCsvPath = Path.Combine(testDataDirectory, "invalid-metadata.csv");
            CreateInvalidCsvFile(invalidCsvPath);
            
            helper = new Helper
            {
                PathToCourtMetadataFile = validCsvPath,
                PathToDataFolder = testDataDirectory
            };
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test files
            if (Directory.Exists(testDataDirectory))
            {
                Directory.Delete(testDataDirectory, true);
            }
        }

        private void CreateValidCsvFile(string path)
        {
            var csvContent = @"id,FilePath,Extension,decision_datetime,file_no_1,file_no_2,file_no_3,claimants,respondent,main_subcategory_description,sec_subcategory_description,headnote_summary
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA,2025,001,Smith,Secretary of State for the Home Department,Immigration Appeals,Asylum,This is a test headnote summary
124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA,2025,002,Jones,HMRC,Tax Appeals,VAT,Another test case
125,/test/data/test-case3.pdf,.pdf,2025-01-17 11:00:00,GRC,2025,003,Williams,DWP,Social Security,ESA,Benefits case
123,/test/data/test-case4.pdf,.pdf,2025-01-18 12:00:00,IA,2025,004,Brown,Home Office,Immigration Appeals,Human Rights,Duplicate ID case";
            
            File.WriteAllText(path, csvContent);
        }

        private void CreateInvalidCsvFile(string path)
        {
            // CSV missing required 'file_no_1' and 'Extension' columns
            var csvContent = @"id,FilePath,decision_datetime,file_no_2,file_no_3,claimants,respondent
123,/test/data/test-case.pdf,2025-01-15 09:00:00,2025,001,Smith,Secretary of State for the Home Department";
            
            File.WriteAllText(path, csvContent);
        }

        [Test]
        public void FindLines_WithValidId_ReturnsMatchingLines()
        {
            // Act
            var result = helper.FindLines(123);

            // Assert
            Assert.That(result, Is.Not.Null, "Result should not be null");
            Assert.That(result.Count, Is.EqualTo(2), "Should return 2 lines with ID 123");
            
            // Verify the returned lines have the correct ID
            Assert.That(result.All(line => line.id == "123"), Is.True, 
                "All returned lines should have ID '123'");
            
            // Verify specific line data
            var firstLine = result.First();
            Assert.That(firstLine.claimants, Is.EqualTo("Smith"));
            Assert.That(firstLine.respondent, Is.EqualTo("Secretary of State for the Home Department"));
            Assert.That(firstLine.file_no_1, Is.EqualTo("IA"));
            Assert.That(firstLine.Extension, Is.EqualTo(".pdf"));
        }

        [Test]
        public void FindLines_WithValidIdSingleMatch_ReturnsSingleLine()
        {
            // Act
            var result = helper.FindLines(124);

            // Assert
            Assert.That(result, Is.Not.Null, "Result should not be null");
            Assert.That(result.Count, Is.EqualTo(1), "Should return 1 line with ID 124");
            
            var line = result.Single();
            Assert.That(line.id, Is.EqualTo("124"));
            Assert.That(line.claimants, Is.EqualTo("Jones"));
            Assert.That(line.respondent, Is.EqualTo("HMRC"));
            Assert.That(line.Extension, Is.EqualTo(".docx"));
        }

        [Test]
        public void FindLines_WithNonExistentId_ReturnsEmptyList()
        {
            // Act
            var result = helper.FindLines(999);

            // Assert
            Assert.That(result, Is.Not.Null, "Result should not be null");
            Assert.That(result.Count, Is.EqualTo(0), "Should return empty list for non-existent ID");
        }

        [Test]
        public void FindLines_WithZeroId_ReturnsEmptyList()
        {
            // Act
            var result = helper.FindLines(0);

            // Assert
            Assert.That(result, Is.Not.Null, "Result should not be null");
            Assert.That(result.Count, Is.EqualTo(0), "Should return empty list for ID 0");
        }

        [Test]
        public void FindLines_WithValidationError_ThrowsInvalidOperationException()
        {
            // Arrange - Create helper with invalid CSV path
            var invalidHelper = new Helper
            {
                PathToCourtMetadataFile = invalidCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => invalidHelper.FindLines(123),
                "Should throw InvalidOperationException for CSV missing required columns");
            
            Assert.That(ex.Message, Does.Contain("CSV validation failed"), 
                "Exception message should indicate CSV validation failure");
            Assert.That(ex.Message, Does.Contain("Missing required columns"), 
                "Exception message should mention missing required columns");
            Assert.That(ex.Message, Does.Contain("file_no_1"), 
                "Exception message should list missing 'file_no_1' column");
            Assert.That(ex.Message, Does.Contain("Extension"), 
                "Exception message should list missing 'Extension' column");
        }

        [Test]
        public void FindLines_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange - Create helper with non-existent CSV path
            var nonExistentPath = Path.Combine(testDataDirectory, "does-not-exist.csv");
            var invalidHelper = new Helper
            {
                PathToCourtMetadataFile = nonExistentPath,
                PathToDataFolder = testDataDirectory
            };

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => invalidHelper.FindLines(123),
                "Should throw FileNotFoundException for non-existent CSV file");
        }

        [Test]
        public void FindLines_WithEmptyFile_ReturnsEmptyList()
        {
            // Arrange - Create empty CSV file with just headers
            var emptyCsvPath = Path.Combine(testDataDirectory, "empty-metadata.csv");
            var emptyContent = "id,FilePath,Extension,decision_datetime,file_no_1,file_no_2,file_no_3,claimants,respondent,main_subcategory_description,sec_subcategory_description,headnote_summary";
            File.WriteAllText(emptyCsvPath, emptyContent);
            
            var emptyHelper = new Helper
            {
                PathToCourtMetadataFile = emptyCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act
            var result = emptyHelper.FindLines(123);

            // Assert
            Assert.That(result, Is.Not.Null, "Result should not be null");
            Assert.That(result.Count, Is.EqualTo(0), "Should return empty list for empty CSV");
        }

        [Test]
        public void FindLines_WithMalformedCsv_ThrowsHeaderValidationException()
        {
            // Arrange - Create malformed CSV file (missing many required headers)
            var malformedCsvPath = Path.Combine(testDataDirectory, "malformed-metadata.csv");
            var malformedContent = @"id,decision_datetime,claimants,respondent
123,2025-01-15 09:00:00,Smith,Secretary of State
124,2025-01-16 10:00:00,Jones,HMRC";
            File.WriteAllText(malformedCsvPath, malformedContent);
            
            var malformedHelper = new Helper
            {
                PathToCourtMetadataFile = malformedCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act & Assert
            var ex = Assert.Throws<System.InvalidOperationException>(() => malformedHelper.FindLines(123),
                "Should throw InvalidOperationException for CSV missing required headers");

            Assert.That(ex.Message, Does.Contain("CSV validation failed. Missing required columns: FilePath, Extension, file_no_1, file_no_2, file_no_3."));
            Assert.That(ex.Message, Does.Contain("Found headers:"));
            Assert.That(ex.Message, Does.Contain("id, decision_datetime, claimants, respondent"));
            Assert.That(ex.Message, Does.Contain("Please preprocess your CSV to match the expected column names exactly."));
        }

        [Test]
        public void FindLines_CaseInsensitiveColumnValidation_ThrowsHeaderValidationException()
        {
            // The validation method uses case-insensitive comparison for required columns,
            // but CsvHelper itself is case-sensitive for property mapping
            
            // Arrange - Create CSV with mixed case headers 
            var mixedCaseCsvPath = Path.Combine(testDataDirectory, "mixed-case-metadata.csv");
            var mixedCaseContent = @"ID,FILEPATH,EXTENSION,DECISION_DATETIME,FILE_NO_1,FILE_NO_2,FILE_NO_3,CLAIMANTS,RESPONDENT,Main_Subcategory_Description,Sec_Subcategory_Description,Headnote_Summary
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA,2025,001,Smith,Secretary of State for the Home Department,Immigration Appeals,Asylum,This is a test headnote summary";
            File.WriteAllText(mixedCaseCsvPath, mixedCaseContent);
            
            var mixedCaseHelper = new Helper
            {
                PathToCourtMetadataFile = mixedCaseCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act & Assert - CsvHelper will throw HeaderValidationException for case mismatch
            var ex = Assert.Throws<HeaderValidationException>(() => mixedCaseHelper.FindLines(123),
                "Should throw HeaderValidationException for case-mismatched headers");
            
            Assert.That(ex.Message, Does.Contain("Header with name"), 
                "Exception message should mention missing headers due to case mismatch");
        }

        [Test]
        public void FindLines_PartiallyMissingRequiredColumns_ThrowsValidationError()
        {
            // Arrange - Create CSV missing only 'FilePath' column
            var partialCsvPath = Path.Combine(testDataDirectory, "partial-metadata.csv");
            var partialContent = @"id,Extension,decision_datetime,file_no_1,file_no_2,file_no_3,claimants,respondent
123,.pdf,2025-01-15 09:00:00,IA,2025,001,Smith,Secretary of State for the Home Department";
            File.WriteAllText(partialCsvPath, partialContent);
            
            var partialHelper = new Helper
            {
                PathToCourtMetadataFile = partialCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => partialHelper.FindLines(123),
                "Should throw InvalidOperationException for partially missing required columns");
            
            Assert.That(ex.Message, Does.Contain("FilePath"), 
                "Exception message should mention missing 'FilePath' column");
        }

        [Test]
        public void FindLines_ReturnsCorrectDataTypes()
        {
            // Act
            var result = helper.FindLines(123);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThan(0));
            
            var line = result.First();
            
            // Verify all properties are strings (as expected from CSV parsing)
            Assert.That(line.id, Is.TypeOf<string>());
            Assert.That(line.decision_datetime, Is.TypeOf<string>());
            Assert.That(line.claimants, Is.TypeOf<string>());
            Assert.That(line.respondent, Is.TypeOf<string>());
            Assert.That(line.file_no_1, Is.TypeOf<string>());
            Assert.That(line.Extension, Is.TypeOf<string>());
            Assert.That(line.FilePath, Is.TypeOf<string>());
            
            // Verify computed properties work correctly
            Assert.That(line.DecisionDate, Is.EqualTo("2025-01-15"));
            Assert.That(line.CaseNo, Is.EqualTo("IA/2025/001"));
        }
    }
}
