#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Backlog.Csv;
using Backlog.Src;

using test.Mocks;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestRead : IDisposable
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
    public void Read_WithOnlyRequiredColumnsAndClaimants_ParsesCsvWithoutError()
    {
        using var csvStream = new StringReader(
            """
            id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,skip
            123 , /test/data/test-case.pdf , .pdf , 2025-01-15 09:00:00 , IA/2025/001,UKUT-IAC , Smith , Secretary of State for the Home Department,
            124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,skip me
            """
        );

        var result =
            csvMetadataReader.Read(csvStream, out var skippedCsvLineIdentifiers, out var csvParseErrors, out _);
        Assert.Empty(csvParseErrors);

        var parsedLine = Assert.Single(result);
        Assert.Equal("123", parsedLine.id);
        Assert.False(parsedLine.Skip);

        Assert.Equal(["Line 3"], skippedCsvLineIdentifiers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n")]
    [InlineData("N")]
    [InlineData("no")]
    [InlineData("No")]
    [InlineData("NO")]
    [InlineData("f")]
    [InlineData("F")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    public void Read_WithFalsySkipValues_ReturnsLine(string skipValue)
    {
        using var csvStream = new StringReader(
            $"""
             id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,skip
             123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State for the Home Department,{skipValue}
             """
        );

        var result =
            csvMetadataReader.Read(csvStream, out var skippedCsvLineIdentifiers, out var csvParseErrors, out _);

        Assert.Empty(csvParseErrors);
        Assert.Empty(skippedCsvLineIdentifiers);

        var line = Assert.Single(result);
        Assert.False(line.Skip);
    }

    [Theory]
    [InlineData("skip")]
    [InlineData("Skip")]
    [InlineData("skip - for reasons")]
    [InlineData("Already in FCL")]
    [InlineData("Duplicate")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("T")]
    public void Read_WithTruthySkipValues_DoesNotReturnLine(string skipValue)
    {
        using var csvStream = new StringReader(
            $"""
             id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,skip
             123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State for the Home Department,{skipValue}
             """
        );

        var result =
            csvMetadataReader.Read(csvStream, out var skippedCsvLineIdentifiers, out var csvParseErrors, out _);

        Assert.Empty(csvParseErrors);
        Assert.Empty(result);
        Assert.Single(skippedCsvLineIdentifiers);
    }

    [Fact]
    public void Read_WithVarietyOfRows_OutputsFullRowCount()
    {
        using var csvStream = new StringReader(
            """
            id,FilePath,Extension,decision_datetime,CaseNo,court,appellants,respondent,skip
            123 , this_is_a_good_unskipped_line.pdf , .pdf , 2025-01-15 09:00:00 , IA/2025/001,UKUT-IAC , Smith , Secretary of State for the Home Department,
            124 , this_is_a_good_skipped_line.pdf , .pdf , 2025-01-15 09:00:00 , IA/2025/001,UKUT-IAC , Smith , Secretary of State for the Home Department,skip me
            125, missing_extension_skipped_line.docx  ,  ,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,skip me
            126, missing_extension_unskipped_line.docx  ,  ,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,
            """
        );
        
        _ = csvMetadataReader.Read(csvStream, out _, out _, out var fullRowCount);

        Assert.Equal(4, fullRowCount);
    }
    
    [Fact]
    public void Read_WithDodgySkippedLines_DoesNotOutputValidationErrors()
    {
        using var csvStream = new StringReader(
            """
            id,FilePath,Extension,decision_datetime,CaseNo,court,appellants,respondent,skip
            123 , this_is_a_good_unskipped_line.pdf , .pdf , 2025-01-15 09:00:00 , IA/2025/001,UKUT-IAC , Smith , Secretary of State for the Home Department,
            124, missing_extension.docx  ,  ,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,skip me
            125, missing_appellant.docx  ,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,  ,HMRC,skip me
            126, missing_date.docx  ,.docx,  ,IA/2025/002,UKFTT-TC,Jones,HMRC,skip me
            127, date_fails_validation.docx  ,.docx, not-a-date ,IA/2025/002,UKFTT-TC,Jones,HMRC,skip me
            128, date_fails_conversion.docx  ,.docx, 2025-99-99 ,IA/2025/002,UKFTT-TC,Jones,HMRC,skip me
            """
        );

        var result =
            csvMetadataReader.Read(csvStream, out var skippedCsvLineIdentifiers, out var csvParseErrors, out _);

        // Assert that the good line is returned
        var parsedLine = Assert.Single(result);
        Assert.Equal("123", parsedLine.id);

        // Assert all skipped lines are returned despite validation errors        
        Assert.Equal(5, skippedCsvLineIdentifiers.Count);

        // Assert that there are no validation errors returned
        Assert.Empty(csvParseErrors);
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
    [InlineData(nameof(CsvLine.Skip))]
    public void Read_WithMissingRequiredColumns_ReturnsParseErrors(string missingColumn)
    {
        var validCsvWithAllRequiredColumns =
            """
            id,FilePath,Extension,DecisionDateTime,CaseNo,Court,claimants,Respondent,Skip
            123 , /test/data/test-case.pdf , .pdf , 2025-01-15 09:00:00 , IA/2025/001,UKUT-IAC , Smith , Secretary of State for the Home Department,
            124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,
            125,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,skip
            """;

        var csvWithMissingColumn = validCsvWithAllRequiredColumns.Replace(missingColumn, "missing_column");

        using var csvStream = new StringReader(
            csvWithMissingColumn
        );

        var result = csvMetadataReader.Read(csvStream, out _, out var csvParseErrors, out _);

        Assert.Empty(result);
        Assert.All(csvParseErrors, csvParseError => Assert.Contains(missingColumn, csvParseError));
    }

    [Theory]
    [InlineData("", new string[] { })]
    [InlineData("     ", new string[] { })]
    [InlineData(",   ,  ", new string[] { })]
    [InlineData("\"Community,Environment\"", new[] { "Community", "Environment" })]
    [InlineData("\"Community, Environment,Other , Another ,\"",
        new[] { "Community", "Environment", "Other", "Another" })]
    [InlineData("\"Community, Environment,,  ,Other , Another ,\"",
        new[] { "Community", "Environment", "Other", "Another" })]
    [InlineData("\"Environment\"", new[] { "Environment" })]
    [InlineData("Environment", new[] { "Environment" })]
    public void Read_WithJurisdictions_StoresTrimmedNonEmptyJurisdictions(string csvJurisdictions,
        string[] expectedJurisdictions)
    {
        using var csvStream = new StringReader(
            $"""
             id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,jurisdictions,skip
             125,/test/data/test-case4.docx,.docx,2025-01-19 13:00:00,IA/2025/004,UKUT-IAC,Taylor,Home Office,{csvJurisdictions},
             """
        );

        var result = csvMetadataReader.Read(csvStream, out _, out _, out _);

        var line = Assert.Single(result);
        Assert.Equal(expectedJurisdictions, line.Jurisdictions);
    }

    [Fact]
    public void Read_WithExtraColumns_StoresAllCsvDataInFullCsvLineContents()
    {
        using var csvStream = new StringReader(
            """
            id,extra column,FilePath,Extension, Other extra Column,decision_datetime,CaseNo,court,claimants,respondent,skip
            123,with data,/test/data/test-case.pdf,.pdf,   ,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State for the Home Department,
            124,,/test/data/test-case2.docx,.docx,some data here,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,
            125,data here,/test/data/test-case3.docx,.docx,and data here,2025-01-17 11:00:00,IA/2025/003,UKFTT-TC,Jones,HMRC,
            """
        );

        var result = csvMetadataReader.Read(csvStream, out _, out _, out _);

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

        // Act
        var result = csvMetadataReader.Read(csvStream, out var skippedCsvLineIdentifiers, out _, out _);

        // Assert - results
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
            },
            new CsvLine
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
            }
        );

        // Assert - marked as skip lines
        var skippedLine = Assert.Single(skippedCsvLineIdentifiers);
        Assert.Equal("Line 11", skippedLine);
    }

    [Fact]
    public void Read_WithInvalidLines_ReturnsBothCsvAndCustomValidationErrors()
    {
        using var csvStream = new StringReader(
            """
            id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,skip
            121,Valid.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Secretary of State for the Home Department,,,,,
            122,AlsoValid.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Secretary of State for the Home Department,My category,,,,
            123,NoClaimants.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,,Secretary of State for the Home Department,,,,,
            completely invalid line
            124,MissingAComma.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,Bad subcategory,,
            125,MissingMainCategory.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,Bad main subcategory,,,
            126,MissingSecondaryCategory.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,,,Bad secondary subcategory,
            999,ValidOneAtTheEnd.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Secretary of State for the Home Department,,,,,
            """
        );

        var validLines = csvMetadataReader.Read(csvStream, out _, out var failedToParseLines, out _);

        Assert.Equal(3, validLines.Count);
        Assert.Equal(5, failedToParseLines.Count);

        Assert.Equivalent(
            new List<string>
            {
                "Line 4: Id 123 - Must have either claimants or appellants. At least one is required. [123,NoClaimants.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,,Secretary of State for the Home Department,,,,,]",
                "Line 5: Field at index '5' does not exist. You can ignore missing fields by setting MissingFieldFound to null. [completely invalid line]",
                "Line 6: Field at index '12' does not exist. You can ignore missing fields by setting MissingFieldFound to null. [124,MissingAComma.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,Bad subcategory,,]",
                "Line 7: Id 125 - main_subcategory 'Bad main subcategory' cannot exist without main_category being defined [125,MissingMainCategory.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,Bad main subcategory,,,]",
                "Line 8: Id 126 - sec_subcategory 'Bad secondary subcategory' cannot exist without sec_category being defined [126,MissingSecondaryCategory.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,,,Bad secondary subcategory,]"
            },
            failedToParseLines);
    }

    [Fact]
    public void Read_WithMixedCaseHeaders_ParsesCorrectly()
    {
        const string csvContent =
            @"ID,FilePath,extension,DECISION_DATETIME,CaseNo,coUrt,appellants,CLAIMANTS,respondent,MAIN_CATEGORY,main_subcategory,SEC_CATEGORY,sec_subcategory,HEADNOTE_SUMMARY,jurisdictions,NCN,webarchiving,uUiD,Skip
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,,Smith,Secretary of State for the Home Department,Immigration,Appeal Rights,Administrative Law,Judicial Review,This is a test headnote summary,,,,,
124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,,HMRC,Tax,VAT Appeals,Employment,Tribunal Procedure,Another test case,,,,,skip me";

        // Arrange - set up stream reader
        using var csvStream = new StringReader(csvContent);

        //Act
        var result = csvMetadataReader.Read(csvStream, out var skippedCsvLineIdentifiers, out _, out _);

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
            }
        );

        Assert.Equal(["Line 3"], skippedCsvLineIdentifiers);
    }

    [Theory]
    [InlineData("2025-01-15", 2025, 01, 15, 0, 0, 0)]
    [InlineData("2025/01/15", 2025, 01, 15, 0, 0, 0)]
    [InlineData("2025 01 15", 2025, 01, 15, 0, 0, 0)]
    [InlineData("2025-1-15", 2025, 01, 15, 0, 0, 0)]
    [InlineData("2025-01-1", 2025, 01, 01, 0, 0, 0)]
    [InlineData("2025-02-28", 2025, 02, 28, 0, 0, 0)]
    [InlineData("2024-02-29", 2024, 02, 29, 0, 0, 0)] // Leap year
    [InlineData("2025-01-15 09:00:00", 2025, 01, 15, 09, 00, 00)]
    [InlineData("2025-01-15T09:00:00Z", 2025, 01, 15, 09, 00, 00)]
    [InlineData("  2025-01-15  ", 2025, 01, 15, 0, 0, 0)]
    public void Read_WithValidDecisionDates_ParsesCorrectDate(string dateString, int year, int month, int day, int hour,
        int minute, int second)
    {
        using var csvStream = new StringReader(
            $"""
             id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,skip
             123,/test/data/test.pdf,.pdf,{dateString},IA/2025/001,UKUT-IAC,Smith,HMRC,
             """
        );

        var result =
            csvMetadataReader.Read(csvStream, out var skippedCsvLineIdentifiers, out var csvParseErrors, out _);

        Assert.Empty(csvParseErrors);
        var line = Assert.Single(result);
        Assert.Equal(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc), line.DecisionDateTime);
    }

    [Theory]
    [InlineData("9315-01-2025",
        "Could not convert field `DecisionDateTime` with value \"9315-01-2025\" to type `DateTime`")]
    [InlineData("15-01-2025",
        "\"15-01-2025\" failed validation with message: Unsupported decision date. Ensure dates are in yyyy-MM-dd format")]
    [InlineData("01/15/2025",
        "\"01/15/2025\" failed validation with message: Unsupported decision date. Ensure dates are in yyyy-MM-dd format")]
    [InlineData("not-a-date",
        "\"not-a-date\" failed validation with message: Unsupported decision date. Ensure dates are in yyyy-MM-dd format")]
    [InlineData("202",
        "\"202\" failed validation with message: Unsupported decision date. Ensure dates are in yyyy-MM-dd format")]
    [InlineData("", "\"\" failed validation with message: Decision date must be provided")]
    [InlineData("    ", "\"\" failed validation with message: Decision date must be provided")]
    public void Read_WithInvalidDecisionDates_ReturnsParseError(string dateString, string expectedErrorMessage)
    {
        var line = $"123,/test/data/test.pdf,.pdf,{dateString},IA/2025/001,UKUT-IAC,Smith,HMRC,";
        expectedErrorMessage = $"Line 2: {expectedErrorMessage} [{line}]";

        using var csvStream = new StringReader(
            $"""
             id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,skip
             {line}
             """
        );

        var result = csvMetadataReader.Read(csvStream, out _, out var csvParseErrors, out _);

        Assert.Empty(result);
        var error = Assert.Single(csvParseErrors);
        Assert.Equal(expectedErrorMessage, error);
    }

    [Fact]
    public void Read_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange - Create helper with non-existent CSV path
        var nonExistentPath = Path.Combine(testDataDirectory, "does-not-exist.csv");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
        {
            _ = csvMetadataReader.Read(nonExistentPath, out _, out _, out _);
        });
    }

    [Fact]
    public void Read_WithEmptyFile_ReturnsEmptyList()
    {
        // Arrange - Create empty CSV file with just headers
        var emptyCsvPath =
            MakeRealCsvFile(
                "id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary");

        // Act
        var lines = csvMetadataReader.Read(emptyCsvPath, out _, out _, out _);

        // Assert
        Assert.Empty(lines);
    }

    [Fact]
    public void Read_FromPath_PopulatesCsvPropertiesWithNameAndHash()
    {
        // Arrange
        const string csvContent = "id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,skip\n" +
                                  "123,/test/data/test.pdf,.pdf,2025-01-15,IA/2025/001,UKUT-IAC,Smith,HMRC,";
        var csvPath = MakeRealCsvFile(csvContent);
        var expectedHash = BacklogParserWorker.Hash(File.ReadAllBytes(csvPath));

        // Act
        var result = csvMetadataReader.Read(csvPath, out _, out _, out _);

        // Assert
        var line = Assert.Single(result);
        Assert.Equal("metadata.csv", line.CsvProperties.Name);
        Assert.Equal(expectedHash, line.CsvProperties.Hash);
    }

    [Fact]
    public void Read_FromTextReader_PopulatesCsvPropertiesWithUnknown()
    {
        // Arrange
        const string csvContent = "id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent,skip\n" +
                                  "123,/test/data/test.pdf,.pdf,2025-01-15,IA/2025/001,UKUT-IAC,Smith,HMRC,";
        using var reader = new StringReader(csvContent);

        // Act
        var result = csvMetadataReader.Read(reader, out _, out _, out _);

        // Assert
        var line = Assert.Single(result);
        Assert.Equal("unknown.csv", line.CsvProperties.Name);
        Assert.Equal("unknown", line.CsvProperties.Hash);
    }
}
