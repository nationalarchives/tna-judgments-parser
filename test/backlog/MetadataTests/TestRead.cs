#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Backlog.Csv;

using test.Mocks;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestRead: IDisposable
{
    private readonly CsvMetadataReader csvMetadataReader = new(new MockLogger<CsvMetadataReader>().Object);
    private readonly string testDataDirectory;

    public TestRead()
    {
        // Create a unique temporary directory for test files (used by some tests accessing the real file system)
        testDataDirectory = Path.Combine(Path.GetTempPath(), nameof(TestRead), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDataDirectory);
    }
    
    private string MakeRealCsvFile(string csvContent)
    {
        var csvPath = Path.Combine(testDataDirectory, "metadata.csv");
      
        File.WriteAllText(csvPath, csvContent);
        return csvPath;
    }

    public void Dispose()
    {
        // Clean up test files
        if (Directory.Exists(testDataDirectory))
        {
            Directory.Delete(testDataDirectory, true);
        }
    }

    [Fact]
    public void Read_WithOnlyRequiredColumnsAndClaimants_ParsesCsvIntoLines()
    {
        using var csvStream = new StringReader(
            """
            id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent
            123 , /test/data/test-case.pdf , .pdf , 2025-01-15 09:00:00 , IA/2025/001,UKUT-IAC , Smith , Secretary of State for the Home Department
            124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC
            """
        );

        var result = csvMetadataReader.Read(csvStream, out var csvParseErrors);
        Assert.Empty(csvParseErrors);

        CsvMetadataLineHelper.AssertCsvLinesMatch(result,
            new CsvLine
            {
                id = "123",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case.pdf",
                Extension = ".pdf",
                DecisionDateTime = new DateTime(2025, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/001",
                Jurisdictions = [],
                Claimants = "Smith",
                Appellants = null,
                Respondent = "Secretary of State for the Home Department",
                MainCategory = null,
                MainSubcategory = null,
                SecCategory = null,
                SecSubcategory = null,
                Ncn = null,
                HeadnoteSummary = null
            },
            new CsvLine
            {
                id = "124",
                Court = "UKFTT-TC",
                FilePath = "/test/data/test-case2.docx",
                Extension = ".docx",
                DecisionDateTime = new DateTime(2025, 01, 16, 10, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/002",
                Jurisdictions = [],
                Claimants = "Jones",
                Appellants = null,
                Respondent = "HMRC",
                MainCategory = null,
                MainSubcategory = null,
                SecCategory = null,
                SecSubcategory = null,
                Ncn = null,
                HeadnoteSummary = null
            }
        );
    }

    [Theory]
    [InlineData(nameof(CsvLine.id))]
    [InlineData(nameof(CsvLine.FilePath))]
    [InlineData(nameof(CsvLine.Extension))]
    [InlineData(nameof(CsvLine.DecisionDateTime))]
    [InlineData(nameof(CsvLine.CaseNo))]
    [InlineData(nameof(CsvLine.Court))]
    [InlineData("claimants")] // missing claimants/appellants has a different validation message
    [InlineData(nameof(CsvLine.Respondent))]
    public void Read_WithMissingRequiredColumns_ReturnsParseErrors(string missingColumn)
    {
        var validCsvWithAllRequiredColumns = """
                id,FilePath,Extension,DecisionDateTime,CaseNo,Court,claimants,Respondent
                123 , /test/data/test-case.pdf , .pdf , 2025-01-15 09:00:00 , IA/2025/001,UKUT-IAC , Smith , Secretary of State for the Home Department
                124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC
                """;
        
        var csvWithMissingColumn = validCsvWithAllRequiredColumns.Replace(missingColumn, "missing_column");
        
        using var csvStream = new StringReader(
            csvWithMissingColumn
        );

        var result = csvMetadataReader.Read(csvStream, out var csvParseErrors);

        Assert.Empty(result);
        Assert.All(csvParseErrors, csvParseError => Assert.Contains(missingColumn, csvParseError));
    }

    [Theory]
    [InlineData("", new string[] { })]
    [InlineData("     ", new string[] { })]
    [InlineData(",   ,  ", new string[] { })]
    [InlineData("\"Community,Environment\"", new[] { "Community", "Environment" })]
    [InlineData("\"Community, Environment,Other , Another ,\"", new[] { "Community", "Environment", "Other", "Another" })]
    [InlineData("\"Community, Environment,,  ,Other , Another ,\"", new[] { "Community", "Environment", "Other", "Another" })]
    [InlineData("\"Environment\"", new[] { "Environment" })]
    [InlineData("Environment", new[] { "Environment" })]
    public void Read_WithJurisdictions_StoresTrimmedNonEmptyJurisdictions(string csvJurisdictions, string[] expectedJurisdictions)
    {
        using var csvStream = new StringReader(
            $"""
             id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,jurisdictions
             125,/test/data/test-case4.docx,.docx,2025-01-19 13:00:00,IA/2025/004,UKUT-IAC,Taylor,Home Office,{csvJurisdictions}
             """
        );

        var result = csvMetadataReader.Read(csvStream, out _);

        var line = Assert.Single(result);
        Assert.Equal(expectedJurisdictions, line.Jurisdictions);
    }

    [Fact]
    public void Read_WithExtraColumns_StoresAllCsvDataInFullCsvLineContents()
    {
        using var csvStream = new StringReader(
            """
            id,extra column,FilePath,Extension, Other extra Column,decision_datetime,CaseNo,court,claimants,respondent
            123,with data,/test/data/test-case.pdf,.pdf,   ,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State for the Home Department
            124,,/test/data/test-case2.docx,.docx,some data here,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC
            125,data here,/test/data/test-case3.docx,.docx,and data here,2025-01-17 11:00:00,IA/2025/003,UKFTT-TC,Jones,HMRC
            """
        );

        var result = csvMetadataReader.Read(csvStream, out _);

        Assert.Collection(result,
            line => Assert.Equivalent(new Dictionary<string, string>
            {
                { "id", "123" },
                { "extra column", "with data" },
                { "court", "UKUT-IAC" },
                { "FilePath", "/test/data/test-case.pdf" },
                { "Extension", ".pdf" },
                { "decision_datetime", "2025-01-15 09:00:00" },
                { "CaseNo", "IA/2025/001" },
                { "claimants", "Smith" },
                { "respondent", "Secretary of State for the Home Department" }
            }, line.FullCsvLineContents),
            line => Assert.Equivalent(new Dictionary<string, string>
            {
                { "id", "124" },
                { "court", "UKFTT-TC" },
                { "FilePath", "/test/data/test-case2.docx" },
                { "Extension", ".docx" },
                { "Other extra Column", "some data here" },
                { "decision_datetime", "2025-01-16 10:00:00" },
                { "CaseNo", "IA/2025/002" },
                { "claimants", "Jones" },
                { "respondent", "HMRC" }
            }, line.FullCsvLineContents),
            line => Assert.Equivalent(new Dictionary<string, string>
            {
                { "id", "125" },
                { "court", "UKFTT-TC" },
                { "FilePath", "/test/data/test-case3.docx" },
                { "Extension", ".docx" },
                { "decision_datetime", "2025-01-17 11:00:00" },
                { "CaseNo", "IA/2025/003" },
                { "claimants", "Jones" },
                { "respondent", "HMRC" },
                { "extra column", "data here" },
                { "Other extra Column", "and data here" }
            }, line.FullCsvLineContents)
        );
    }

    [Fact]
    public void Read_WithAllPossibleColumns_ParsesCsvIntoLines()
    {
        // Arrange - This CSV content should have ALL possible columns in it
        const string csvContent =
            """
            id,FilePath,Extension,DecisionDateTime,CaseNo,court,appellants,claimants,respondent,maincategory,mainsubcategory,seccategory,secsubcategory,headnotesummary,jurisdictions,ncn,webarchiving,uuid,skip
            123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,,Smith,Secretary of State for the Home Department,Immigration,Appeal Rights,Administrative Law,Judicial Review,This is a test headnote summary,,,,,
            124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,,HMRC,Tax,VAT Appeals,Employment,Tribunal Procedure,Another test case,    ,    ,    ,    ,
            125,/test/data/test-case3.pdf,.pdf,2025-01-17 11:00:00,GRC/2025/003,UKFTT-GRC,,Williams,DWP,Social Security,Employment Support Allowance,Benefits,Appeals Procedure,Benefits case,,[2023] EWCA Civ 123 & 124,,,
            123,/test/data/test-case4.pdf,.pdf,2025-01-18 12:00:00,IA/2025/004,UKUT-IAC,Brown,,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Duplicate ID case,,,,,
            126,/test/data/test-case5.docx,.docx,2025-01-19 13:00:00,IA/2025/005,UKUT-IAC,,Taylor,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Multiple Jurisdictions,"Community,Environment",,,,
            127,/test/data/test-case6.docx,.docx,2025-01-19 13:00:00,IA/2025/006,UKUT-IAC,,Taylor,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Multiple Jurisdictions with spaces,"Community, Environment,Other , Another ,",,,,
            128,/test/data/test-case7.docx,.docx,2025-01-19 13:00:00,IA/2025/007,UKUT-IAC,,Davies,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,One Jurisdiction,Environment,,,,
            129,/test/data/test-case8.pdf,.pdf,2025-01-20 14:00:00,IA/2025/008,UKUT-IAC,,Berry,Home Office,,,,,With web archiving link,,,http://webarchivinglink,,
            130,/test/data/test-case9.pdf,.pdf,2025-01-20 14:00:00,IA/2025/009,UKUT-IAC,,Berry,Home Office,,,,,With UUID,,,,ba2c15ca-6d3d-4550-8975-b516e3c0ed2d,n
            131,/test/data/test-case10.pdf,.pdf,2025-01-20 14:00:00,IA/2025/009,UKUT-IAC,,Berry,Home Office,,,,,With Skip,,,,,skip me
            """;

        // Arrange - Double check that csv input has all columns in case new ones are added
        var publicPropertiesInCsvLineClass = typeof(CsvLine).GetProperties()
                                                            .Select(p => p.Name)
                                                            .Except([
                                                                nameof(CsvLine.CsvProperties),
                                                                nameof(CsvLine.FullCsvLineContents),
                                                                nameof(CsvLine.Categories),
                                                                nameof(CsvLine.Parties),
                                                                nameof(CsvLine.FileName)
                                                            ]);
        var csvHeaderParts = csvContent.Split(Environment.NewLine)[0].Split(",");
        foreach (var publicProperty in publicPropertiesInCsvLineClass)
        {
            Assert.Contains(publicProperty, csvHeaderParts, StringComparer.InvariantCultureIgnoreCase);
        }

        // Arrange - set up stream reader
        using var csvStream = new StringReader(csvContent);

        //Act
        var result = csvMetadataReader.Read(csvStream, out _);

        CsvMetadataLineHelper.AssertCsvLinesMatch(result,
            new CsvLine
            {
                id = "123",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case.pdf",
                Extension = ".pdf",
                DecisionDateTime = new DateTime(2025, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/001",
                Jurisdictions = [],
                Claimants = "Smith",
                Appellants = null,
                Respondent = "Secretary of State for the Home Department",
                MainCategory = "Immigration",
                MainSubcategory = "Appeal Rights",
                SecCategory = "Administrative Law",
                SecSubcategory = "Judicial Review",
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "This is a test headnote summary",
                Uuid = null,
                Skip = false
            },
            new CsvLine
            {
                id = "124",
                Court = "UKFTT-TC",
                FilePath = "/test/data/test-case2.docx",
                Extension = ".docx",
                DecisionDateTime = new DateTime(2025, 01, 16, 10, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/002",
                Jurisdictions = [],
                Claimants = null,
                Appellants = "Jones",
                Respondent = "HMRC",
                MainCategory = "Tax",
                MainSubcategory = "VAT Appeals",
                SecCategory = "Employment",
                SecSubcategory = "Tribunal Procedure",
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "Another test case",
                Uuid = null,
                Skip = false
            },
            new CsvLine
            {
                id = "125",
                Court = "UKFTT-GRC",
                FilePath = "/test/data/test-case3.pdf",
                Extension = ".pdf",
                DecisionDateTime = new DateTime(2025, 01, 17, 11, 00, 00, DateTimeKind.Utc),
                CaseNo = "GRC/2025/003",
                Jurisdictions = [],
                Claimants = "Williams",
                Appellants = null,
                Respondent = "DWP",
                MainCategory = "Social Security",
                MainSubcategory = "Employment Support Allowance",
                SecCategory = "Benefits",
                SecSubcategory = "Appeals Procedure",
                Ncn = "[2023] EWCA Civ 123 & 124",
                WebArchiving = null,
                HeadnoteSummary = "Benefits case",
                Uuid = null,
                Skip = false
            },
            new CsvLine
            {
                id = "123",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case4.pdf",
                Extension = ".pdf",
                DecisionDateTime = new DateTime(2025, 01, 18, 12, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/004",
                Jurisdictions = [],
                Claimants = null,
                Appellants = "Brown",
                Respondent = "Home Office",
                MainCategory = "Immigration",
                MainSubcategory = "Entry Clearance",
                SecCategory = "Administrative Law",
                SecSubcategory = "Case Management",
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "Duplicate ID case",
                Uuid = null,
                Skip = false
            },
            new CsvLine
            {
                id = "126",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case5.docx",
                Extension = ".docx",
                DecisionDateTime = new DateTime(2025, 01, 19, 13, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/005",
                Jurisdictions = ["Community", "Environment"],
                Claimants = "Taylor",
                Appellants = null,
                Respondent = "Home Office",
                MainCategory = "Immigration",
                MainSubcategory = "Entry Clearance",
                SecCategory = "Administrative Law",
                SecSubcategory = "Case Management",
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "Multiple Jurisdictions",
                Uuid = null,
                Skip = false
            },
            new CsvLine
            {
                id = "127",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case6.docx",
                Extension = ".docx",
                DecisionDateTime = new DateTime(2025, 01, 19, 13, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/006",
                Jurisdictions = ["Community", "Environment", "Other", "Another"],
                Claimants = "Taylor",
                Appellants = null,
                Respondent = "Home Office",
                MainCategory = "Immigration",
                MainSubcategory = "Entry Clearance",
                SecCategory = "Administrative Law",
                SecSubcategory = "Case Management",
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "Multiple Jurisdictions with spaces",
                Uuid = null,
                Skip = false
            },
            new CsvLine
            {
                id = "128",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case7.docx",
                Extension = ".docx",
                DecisionDateTime = new DateTime(2025, 01, 19, 13, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/007",
                Jurisdictions = ["Environment"],
                Claimants = "Davies",
                Appellants = null,
                Respondent = "Home Office",
                MainCategory = "Immigration",
                MainSubcategory = "Entry Clearance",
                SecCategory = "Administrative Law",
                SecSubcategory = "Case Management",
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "One Jurisdiction",
                Uuid = null
            }, new CsvLine
            {
                id = "129",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case8.pdf",
                Extension = ".pdf",
                DecisionDateTime = new DateTime(2025, 01, 20, 14, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/008",
                Jurisdictions = [],
                Claimants = "Berry",
                Appellants = null,
                Respondent = "Home Office",
                MainCategory = null,
                MainSubcategory = null,
                SecCategory = null,
                SecSubcategory = null,
                Ncn = null,
                WebArchiving = "http://webarchivinglink",
                HeadnoteSummary = "With web archiving link",
                Uuid = null,
                Skip = false
            },
            new CsvLine
            {
                id = "130",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case9.pdf",
                Extension = ".pdf",
                DecisionDateTime = new DateTime(2025, 01, 20, 14, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/009",
                Jurisdictions = [],
                Claimants = "Berry",
                Appellants = null,
                Respondent = "Home Office",
                MainCategory = null,
                MainSubcategory = null,
                SecCategory = null,
                SecSubcategory = null,
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "With UUID",
                Uuid = "ba2c15ca-6d3d-4550-8975-b516e3c0ed2d",
                Skip = false
            },
            new CsvLine
            {
                id = "131",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case10.pdf",
                Extension = ".pdf",
                DecisionDateTime = new DateTime(2025, 01, 20, 14, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/009",
                Jurisdictions = [],
                Claimants = "Berry",
                Appellants = null,
                Respondent = "Home Office",
                MainCategory = null,
                MainSubcategory = null,
                SecCategory = null,
                SecSubcategory = null,
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "With Skip",
                Uuid = null,
                Skip = true
            }
        );
    }

    [Fact]
    public void Read_WithInvalidLines_ReturnsBothCsvAndCustomValidationErrors()
    {
        using var csvStream = new StringReader(
            """
            id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory
            121,Valid.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Secretary of State for the Home Department,,,,
            122,AlsoValid.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Secretary of State for the Home Department,My category,,,
            123,NoClaimants.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,,Secretary of State for the Home Department,,,,
            completely invalid line
            124,MissingAComma.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,Bad subcategory,
            125,MissingMainCategory.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,Bad main subcategory,,
            126,MissingSecondaryCategory.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,,,Bad secondary subcategory
            999,ValidOneAtTheEnd.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Secretary of State for the Home Department,,,,
            """
        );

        var validLines = csvMetadataReader.Read(csvStream, out var failedToParseLines);

        Assert.Equal(3, validLines.Count);
        Assert.Equal(5, failedToParseLines.Count);

        Assert.Equivalent(
            new List<string>
            {
                "Line 4: Id 123 - Must have either claimants or appellants. At least one is required.",
                "Line 5: Field at index '5' does not exist. You can ignore missing fields by setting MissingFieldFound to null. [completely invalid line]",
                "Line 6: Field at index '11' does not exist. You can ignore missing fields by setting MissingFieldFound to null. [124,MissingAComma.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,Bad subcategory,]",
                "Line 7: Id 125 - main_subcategory 'Bad main subcategory' cannot exist without main_category being defined",
                "Line 8: Id 126 - sec_subcategory 'Bad secondary subcategory' cannot exist without sec_category being defined"
            },
            failedToParseLines);
    }

    [Fact]
    public void Read_WithMixedCaseHeaders_ParsesCorrectly()
    {
        const string csvContent =
            @"ID,FilePath,extension,DECISION_DATETIME,CaseNo,coUrt,appellants,CLAIMANTS,respondent,MAIN_CATEGORY,main_subcategory,SEC_CATEGORY,sec_subcategory,HEADNOTE_SUMMARY,jurisdictions,NCN,webarchiving,uUiD
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,,Smith,Secretary of State for the Home Department,Immigration,Appeal Rights,Administrative Law,Judicial Review,This is a test headnote summary,,,,
124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,,HMRC,Tax,VAT Appeals,Employment,Tribunal Procedure,Another test case,,,,";

        // Arrange - set up stream reader
        using var csvStream = new StringReader(csvContent);

        //Act
        var result = csvMetadataReader.Read(csvStream, out _);

        CsvMetadataLineHelper.AssertCsvLinesMatch(result,
            new CsvLine
            {
                id = "123",
                Court = "UKUT-IAC",
                FilePath = "/test/data/test-case.pdf",
                Extension = ".pdf",
                DecisionDateTime = new DateTime(2025, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/001",
                Jurisdictions = [],
                Claimants = "Smith",
                Appellants = null,
                Respondent = "Secretary of State for the Home Department",
                MainCategory = "Immigration",
                MainSubcategory = "Appeal Rights",
                SecCategory = "Administrative Law",
                SecSubcategory = "Judicial Review",
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "This is a test headnote summary",
                Uuid = null
            },
            new CsvLine
            {
                id = "124",
                Court = "UKFTT-TC",
                FilePath = "/test/data/test-case2.docx",
                Extension = ".docx",
                DecisionDateTime = new DateTime(2025, 01, 16, 10, 00, 00, DateTimeKind.Utc),
                CaseNo = "IA/2025/002",
                Jurisdictions = [],
                Claimants = null,
                Appellants = "Jones",
                Respondent = "HMRC",
                MainCategory = "Tax",
                MainSubcategory = "VAT Appeals",
                SecCategory = "Employment",
                SecSubcategory = "Tribunal Procedure",
                Ncn = null,
                WebArchiving = null,
                HeadnoteSummary = "Another test case",
                Uuid = null
            }
        );
    }

    [Fact]
    public void Read_WithMixedDateFormats_ParsesValidLines()
    {
        const string csvContent =
            """
            id,filepath,extension,decision_datetime,caseno,court,appellants,claimants,respondent
            123,good.pdf,.pdf,  2025-01-15  ,IA/2025/001,UKUT-IAC,,Smith,Secretary of State for the Home Department
            123,good.pdf,.pdf,  2025/01/15  ,IA/2025/001,UKUT-IAC,,Smith,Secretary of State for the Home Department
            123,good.pdf,.pdf,  2025 01 15  ,IA/2025/001,UKUT-IAC,,Smith,Secretary of State for the Home Department

            125,bad.pdf,.pdf,  01/16/2025 10:00:00  ,IA/2025/002,UKFTT-TC,Jones,,HMRC
            125,bad.pdf,.pdf,  31/01/2025 10:00:00  ,IA/2025/002,UKFTT-TC,Jones,,HMRC

            124,good_with_time.docx,.docx,  2025-01-16 10:00:00  ,IA/2025/002,UKFTT-TC,Jones,,HMRC
            """;

        // Arrange - set up stream reader
        using var csvStream = new StringReader(csvContent);

        //Act
        var result = csvMetadataReader.Read(csvStream, out var csvParseErrors);

        Assert.Collection(result,
            line => Assert.Equal(new DateTime(2025, 01, 15, 0, 0, 0, DateTimeKind.Utc), line.DecisionDateTime),
            line => Assert.Equal(new DateTime(2025, 01, 15, 0, 0, 0, DateTimeKind.Utc), line.DecisionDateTime),
            line => Assert.Equal(new DateTime(2025, 01, 15, 0, 0, 0, DateTimeKind.Utc), line.DecisionDateTime),
            line => Assert.Equal(new DateTime(2025, 01, 16, 10, 00, 00, DateTimeKind.Utc), line.DecisionDateTime)
        );

        Assert.Equivalent(
            new List<string>
            {
                "Line 6: Field '01/16/2025 10:00:00' is not valid. [125,bad.pdf,.pdf,  01/16/2025 10:00:00  ,IA/2025/002,UKFTT-TC,Jones,,HMRC]",
                "Line 7: Field '31/01/2025 10:00:00' is not valid. [125,bad.pdf,.pdf,  31/01/2025 10:00:00  ,IA/2025/002,UKFTT-TC,Jones,,HMRC]"
            },
            csvParseErrors);
    }

    [Fact]
    public void Read_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange - Create helper with non-existent CSV path
        var nonExistentPath = Path.Combine(testDataDirectory, "does-not-exist.csv");
     
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
        {
            _ = csvMetadataReader.Read(nonExistentPath, out _);
        });
    }

    [Fact]
    public void Read_WithEmptyFile_ReturnsEmptyList()
    {
        // Arrange - Create empty CSV file with just headers
        var emptyCsvPath = MakeRealCsvFile("id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary");
            
        // Act
        var lines = csvMetadataReader.Read(emptyCsvPath, out _);

        // Assert
        Assert.Empty(lines);
    }
}
