using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Backlog.Src.Batch.One;
using CsvHelper;
using CsvHelper.Configuration;

namespace Backlog.Test
{
    [TestFixture]
    public class TestHelperFindLines
    {
        private Helper helper;
        private string testDataDirectory;
        private string validCsvPath;
        
        [SetUp]
        public void Setup()
        {
            // Create a temporary directory for test files
            testDataDirectory = Path.Combine(Path.GetTempPath(), "TestHelperFindLines", Guid.NewGuid().ToString());
            Directory.CreateDirectory(testDataDirectory);
            
            // Create valid CSV file with required columns
            validCsvPath = Path.Combine(testDataDirectory, "valid-metadata.csv");
            CreateValidCsvFile(validCsvPath);
            
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
        public void FindLines_WithMalformedCsv_ThrowsCsvHelperException()
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

            // Act & Assert - CsvHelper will throw when required headers are missing
            var ex = Assert.Throws<CsvHelper.HeaderValidationException>(() => malformedHelper.FindLines(123),
                "Should throw CsvHelper.HeaderValidationException for CSV missing required headers");

            // Verify the exception message contains information about missing headers
            Assert.That(ex.Message, Does.Contain("FilePath").Or.Contain("Extension").Or.Contain("file_no_1"), 
                "Exception message should mention at least one of the missing required columns");
        }
        [Test]
        public void FindLines_PartiallyMissingRequiredColumns_ThrowsCsvHelperException()
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

            // Act & Assert - CsvHelper will throw when required columns are missing
            var ex = Assert.Throws<CsvHelper.HeaderValidationException>(() => partialHelper.FindLines(123),
                "Should throw CsvHelper.HeaderValidationException for partially missing required columns");
                
            // Verify the exception message contains information about the missing FilePath column
            Assert.That(ex.Message, Does.Contain("FilePath"), 
                "Exception message should mention the missing 'FilePath' column");
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
