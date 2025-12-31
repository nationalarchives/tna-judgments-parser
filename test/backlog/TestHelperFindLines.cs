using System;
using System.IO;
using System.Linq;

using Backlog.Src;

using test.Mocks;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Xunit;

using Metadata = Backlog.Src.Metadata;
using Parser = UK.Gov.NationalArchives.Judgments.Api.Parser;

namespace test.backlog
{
    public sealed class TestHelperFindLines: IDisposable
    {
        private readonly Helper helper;
        private readonly Parser parser = new(new MockLogger<Parser>().Object, new Validator());
        private readonly Metadata csvMetadataReader = new(new MockLogger<Metadata>().Object);
        private string testDataDirectory;
        private string validCsvPath;

        public TestHelperFindLines()
        {
            // Create a temporary directory for test files
            testDataDirectory = Path.Combine(Path.GetTempPath(), "TestHelperFindLines", Guid.NewGuid().ToString());
            Directory.CreateDirectory(testDataDirectory);
            
            // Create valid CSV file with required columns
            validCsvPath = Path.Combine(testDataDirectory, "valid-metadata.csv");
            CreateValidCsvFile(validCsvPath);
            
            helper = new Helper(parser, csvMetadataReader)
            {
                PathToCourtMetadataFile = validCsvPath,
                PathToDataFolder = testDataDirectory
            };
        }

        public void Dispose()
        {
            // Clean up test files
            if (Directory.Exists(testDataDirectory))
            {
                Directory.Delete(testDataDirectory, true);
            }
        }

        private void CreateValidCsvFile(string path)
        {
            var csvContent = @"id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State for the Home Department,Immigration,Appeal Rights,Administrative Law,Judicial Review,This is a test headnote summary
124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,Tax,VAT Appeals,Employment,Tribunal Procedure,Another test case
125,/test/data/test-case3.pdf,.pdf,2025-01-17 11:00:00,GRC/2025/003,UKFTT-GRC,Williams,DWP,Social Security,Employment Support Allowance,Benefits,Appeals Procedure,Benefits case
123,/test/data/test-case4.pdf,.pdf,2025-01-18 12:00:00,IA/2025/004,UKUT-IAC,Brown,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Duplicate ID case";

            File.WriteAllText(path, csvContent);
        }

        [Fact]
        public void FindLines_WithValidId_ReturnsMatchingLines()
        {
            // Act
            var result = helper.FindLines(123);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            // Verify the returned lines have the correct ID
            Assert.True(result.All(line => line.id == "123"), 
                "All returned lines should have ID '123'");
            
            // Verify specific line data
            var firstLine = result.First();
            Assert.Equal("Smith", firstLine.claimants);
            Assert.Equal("Secretary of State for the Home Department", firstLine.respondent);
            Assert.Equal("IA/2025/001", firstLine.CaseNo);
            Assert.Equal(".pdf", firstLine.Extension);
        }

        [Fact]
        public void FindLines_WithValidIdSingleMatch_ReturnsSingleLine()
        {
            // Act
            var result = helper.FindLines(124);

            // Assert
            Assert.NotNull(result);
            
            var line = Assert.Single(result);
            Assert.Equal("124", line.id);
            Assert.Equal("Jones", line.claimants);
            Assert.Equal("HMRC", line.respondent);
            Assert.Equal(".docx", line.Extension);
        }

        [Fact]
        public void FindLines_WithNonExistentId_ReturnsEmptyList()
        {
            // Act
            var result = helper.FindLines(999);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void FindLines_WithZeroId_ReturnsEmptyList()
        {
            // Act
            var result = helper.FindLines(0);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void FindLines_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange - Create helper with non-existent CSV path
            var nonExistentPath = Path.Combine(testDataDirectory, "does-not-exist.csv");
            helper.PathToCourtMetadataFile = nonExistentPath;

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => helper.FindLines(123));
        }

        [Fact]
        public void FindLines_WithEmptyFile_ReturnsEmptyList()
        {
            // Arrange - Create empty CSV file with just headers
            var emptyCsvPath = Path.Combine(testDataDirectory, "empty-metadata.csv");
            var emptyContent = "id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary";
            File.WriteAllText(emptyCsvPath, emptyContent);
            
            helper.PathToCourtMetadataFile = emptyCsvPath;

            // Act
            var result = helper.FindLines(123);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void FindLines_WithMalformedCsv_ThrowsCsvHelperException()
        {
            // Arrange - Create malformed CSV file (missing many required headers)
            var malformedCsvPath = Path.Combine(testDataDirectory, "malformed-metadata.csv");
            var malformedContent = @"id,decision_datetime,claimants,respondent
123,2025-01-15 09:00:00,Smith,Secretary of State
124,2025-01-16 10:00:00,Jones,HMRC";
            File.WriteAllText(malformedCsvPath, malformedContent);
            
            helper.PathToCourtMetadataFile = malformedCsvPath;

            // Act & Assert - CsvHelper will throw when required headers are missing
            var ex = Assert.Throws<CsvHelper.MissingFieldException>(() => helper.FindLines(123));
                
            // Verify the exception message contains information about first missing header
            Assert.Contains("court", ex.Message);
        }

        [Fact]
        public void FindLines_PartiallyMissingRequiredColumns_ThrowsCsvHelperException()
        {
            // Arrange - Create CSV missing only 'FilePath' column
            var partialCsvPath = Path.Combine(testDataDirectory, "partial-metadata.csv");
            var partialContent = @"id,Extension,decision_datetime,CaseNo,claimants,respondent,court
123,.pdf,2025-01-15 09:00:00,IA/2025/001,Smith,Secretary of State for the Home Department,UKUT-IAC";
            File.WriteAllText(partialCsvPath, partialContent);
            
            helper.PathToCourtMetadataFile = partialCsvPath;

            // Act & Assert - CsvHelper will throw when required columns are missing
            var ex = Assert.Throws<CsvHelper.MissingFieldException>(() => helper.FindLines(123));
                
            // Verify the exception message contains information about the missing FilePath column
            Assert.Contains("FilePath", ex.Message);
        }

        [Fact]
        public void FindLines_WithMainSubcategoryButNoMainCategory_ThrowsCsvValidationException()
        {
            // Arrange - Create CSV with main_subcategory but no main_category
            var invalidCategoryCsvPath = Path.Combine(testDataDirectory, "invalid-category-metadata.csv");
            var invalidCategoryContent = @"id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary
126,/test/data/test-case-invalid.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State,,Appeals,Tax,VAT,Test case with orphaned main_subcategory";
            File.WriteAllText(invalidCategoryCsvPath, invalidCategoryContent);
            
            helper.PathToCourtMetadataFile = invalidCategoryCsvPath;

            // Act & Assert - Should throw CsvHelperException during CSV reading
            var ex = Assert.Throws<CsvHelper.CsvHelperException>(() => helper.FindLines(126));
                
            // Verify the exception message contains information about the validation rule
            Assert.Contains("main_subcategory", ex.Message);
            Assert.Contains("main_category", ex.Message);
        }

        [Fact]
        public void FindLines_WithSecSubcategoryButNoSecCategory_ThrowsCsvValidationException()
        {
            // Arrange - Create CSV with sec_subcategory but no sec_category
            var invalidSecCategoryCsvPath = Path.Combine(testDataDirectory, "invalid-sec-category-metadata.csv");
            var invalidSecCategoryContent = @"id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary
127,/test/data/test-case-invalid2.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State,Immigration,Appeals,,VAT,Test case with orphaned sec_subcategory";
            File.WriteAllText(invalidSecCategoryCsvPath, invalidSecCategoryContent);
            
            helper.PathToCourtMetadataFile = invalidSecCategoryCsvPath;

            // Act & Assert - Should throw CsvHelperException during CSV reading
            var ex = Assert.Throws<CsvHelper.CsvHelperException>(() => helper.FindLines(127));
                
            // Verify the exception message contains information about the validation rule
            Assert.Contains("sec_subcategory", ex.Message);
            Assert.Contains("sec_category", ex.Message);
        }

        [Fact]
        public void FindLines_WithValidCategoryHierarchy_ProcessesSuccessfully()
        {
            // Arrange - Create CSV with proper category hierarchy
            var validCategoryCsvPath = Path.Combine(testDataDirectory, "valid-category-metadata.csv");
            var validCategoryContent = @"id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary
128,/test/data/test-case-valid.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State,Immigration,Appeals,Tax,VAT,Test case with valid category hierarchy";
            File.WriteAllText(validCategoryCsvPath, validCategoryContent);
            
            helper.PathToCourtMetadataFile = validCategoryCsvPath;

            // Act - Should process successfully without throwing (validation happens during CSV reading)
            var lines = helper.FindLines(128);
            
            // Assert
            Assert.Single(lines);
            
            // Verify the metadata can be created successfully
            var metadata = Metadata.MakeMetadata(lines.First());
            Assert.NotNull(metadata.Categories);
            Assert.Equal(4, metadata.Categories.Count); // main, main_sub, sec, sec_sub
        }
    }
}
