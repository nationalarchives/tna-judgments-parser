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
            var csvContent = @"id,FilePath,Extension,decision_datetime,CaseNo,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,Smith,Secretary of State for the Home Department,Immigration,Appeal Rights,Administrative Law,Judicial Review,This is a test headnote summary
124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,Jones,HMRC,Tax,VAT Appeals,Employment,Tribunal Procedure,Another test case
125,/test/data/test-case3.pdf,.pdf,2025-01-17 11:00:00,GRC/2025/003,Williams,DWP,Social Security,Employment Support Allowance,Benefits,Appeals Procedure,Benefits case
123,/test/data/test-case4.pdf,.pdf,2025-01-18 12:00:00,IA/2025/004,Brown,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Duplicate ID case";

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
            Assert.That(firstLine.CaseNo, Is.EqualTo("IA/2025/001"));
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
            var emptyContent = "id,FilePath,Extension,decision_datetime,CaseNo,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary";
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
            var ex = Assert.Throws<CsvHelper.MissingFieldException>(() => malformedHelper.FindLines(123),
                "Should throw CsvHelper.MissingFieldException for CSV missing required headers");
                
            // Verify the exception message contains information about missing headers
            Assert.That(ex.Message, Does.Contain("FilePath").Or.Contain("Extension").Or.Contain("CaseNo"), 
                "Exception message should mention at least one of the missing required columns");
        }
        [Test]
        public void FindLines_PartiallyMissingRequiredColumns_ThrowsCsvHelperException()
        {
            // Arrange - Create CSV missing only 'FilePath' column
            var partialCsvPath = Path.Combine(testDataDirectory, "partial-metadata.csv");
            var partialContent = @"id,Extension,decision_datetime,CaseNo,claimants,respondent
123,.pdf,2025-01-15 09:00:00,IA/2025/001,Smith,Secretary of State for the Home Department";
            File.WriteAllText(partialCsvPath, partialContent);
            
            var partialHelper = new Helper
            {
                PathToCourtMetadataFile = partialCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act & Assert - CsvHelper will throw when required columns are missing
            var ex = Assert.Throws<CsvHelper.MissingFieldException>(() => partialHelper.FindLines(123),
                "Should throw CsvHelper.MissingFieldException for partially missing required columns");
                
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
            Assert.That(line.CaseNo, Is.TypeOf<string>());
            Assert.That(line.Extension, Is.TypeOf<string>());
            Assert.That(line.FilePath, Is.TypeOf<string>());
            
            // Verify computed properties work correctly
            Assert.That(line.DecisionDate, Is.EqualTo("2025-01-15"));
            Assert.That(line.CaseNo, Is.EqualTo("IA/2025/001"));
        }

        [Test]
        public void FindLines_WithMainSubcategoryButNoMainCategory_ThrowsCsvValidationException()
        {
            // Arrange - Create CSV with main_subcategory but no main_category
            var invalidCategoryCsvPath = Path.Combine(testDataDirectory, "invalid-category-metadata.csv");
            var invalidCategoryContent = @"id,FilePath,Extension,decision_datetime,CaseNo,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary
126,/test/data/test-case-invalid.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,Smith,Secretary of State,,Appeals,Tax,VAT,Test case with orphaned main_subcategory";
            File.WriteAllText(invalidCategoryCsvPath, invalidCategoryContent);
            
            var invalidCategoryHelper = new Helper
            {
                PathToCourtMetadataFile = invalidCategoryCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act & Assert - Should throw CsvHelperException during CSV reading
            var ex = Assert.Throws<CsvHelper.CsvHelperException>(() => invalidCategoryHelper.FindLines(126),
                "Should throw CsvHelper.CsvHelperException for main_subcategory without main_category during CSV reading");
                
            // Verify the exception message contains information about the validation rule
            Assert.That(ex.Message, Does.Contain("main_subcategory").And.Contain("main_category"), 
                "Exception message should mention the validation rule for main_subcategory and main_category");
        }

        [Test]
        public void FindLines_WithSecSubcategoryButNoSecCategory_ThrowsCsvValidationException()
        {
            // Arrange - Create CSV with sec_subcategory but no sec_category
            var invalidSecCategoryCsvPath = Path.Combine(testDataDirectory, "invalid-sec-category-metadata.csv");
            var invalidSecCategoryContent = @"id,FilePath,Extension,decision_datetime,CaseNo,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary
127,/test/data/test-case-invalid2.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,Smith,Secretary of State,Immigration,Appeals,,VAT,Test case with orphaned sec_subcategory";
            File.WriteAllText(invalidSecCategoryCsvPath, invalidSecCategoryContent);
            
            var invalidSecCategoryHelper = new Helper
            {
                PathToCourtMetadataFile = invalidSecCategoryCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act & Assert - Should throw CsvHelperException during CSV reading
            var ex = Assert.Throws<CsvHelper.CsvHelperException>(() => invalidSecCategoryHelper.FindLines(127),
                "Should throw CsvHelper.CsvHelperException for sec_subcategory without sec_category during CSV reading");
                
            // Verify the exception message contains information about the validation rule
            Assert.That(ex.Message, Does.Contain("sec_subcategory").And.Contain("sec_category"), 
                "Exception message should mention the validation rule for sec_subcategory and sec_category");
        }

        [Test]
        public void FindLines_WithValidCategoryHierarchy_ProcessesSuccessfully()
        {
            // Arrange - Create CSV with proper category hierarchy
            var validCategoryCsvPath = Path.Combine(testDataDirectory, "valid-category-metadata.csv");
            var validCategoryContent = @"id,FilePath,Extension,decision_datetime,CaseNo,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary
128,/test/data/test-case-valid.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,Smith,Secretary of State,Immigration,Appeals,Tax,VAT,Test case with valid category hierarchy";
            File.WriteAllText(validCategoryCsvPath, validCategoryContent);
            
            var validCategoryHelper = new Helper
            {
                PathToCourtMetadataFile = validCategoryCsvPath,
                PathToDataFolder = testDataDirectory
            };

            // Act - Should process successfully without throwing (validation happens during CSV reading)
            var lines = validCategoryHelper.FindLines(128);
            
            // Assert
            Assert.That(lines, Is.Not.Null);
            Assert.That(lines.Count, Is.EqualTo(1));
            
            // Verify the metadata can be created successfully
            var metadata = Metadata.MakeMetadata(lines.First());
            Assert.That(metadata.Categories, Is.Not.Null);
            Assert.That(metadata.Categories.Count, Is.EqualTo(4)); // main, main_sub, sec, sec_sub
        }
    }
}
