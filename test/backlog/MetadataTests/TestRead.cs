#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Backlog.Src;

using test.Mocks;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestRead: IDisposable
{
    private readonly Metadata csvMetadataReader = new(new MockLogger<Metadata>().Object);
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
            @"id,FilePath,Extension,decision_datetime,CaseNo,court,claimants,respondent
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Smith,Secretary of State for the Home Department
124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC"
        );

        var result = csvMetadataReader.Read(csvStream, out _);

        Assert.Collection(result,
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "123",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case.pdf",
                    Extension = ".pdf",
                    decision_datetime = new DateTime(2025, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/001",
                    Jurisdictions = [],
                    claimants = "Smith",
                    appellants = null,
                    respondent = "Secretary of State for the Home Department",
                    main_category = null,
                    main_subcategory = null,
                    sec_category = null,
                    sec_subcategory = null,
                    ncn = null,
                    headnote_summary = null
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "124",
                    court = "UKFTT-TC",
                    FilePath = "/test/data/test-case2.docx",
                    Extension = ".docx",
                    decision_datetime = new DateTime(2025, 01, 16, 10, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/002",
                    Jurisdictions = [],
                    claimants = "Jones",
                    appellants = null,
                    respondent = "HMRC",
                    main_category = null,
                    main_subcategory = null,
                    sec_category = null,
                    sec_subcategory = null,
                    ncn = null,
                    headnote_summary = null
                }, line)
        );
    }

    [Fact]
    public void Read_WithAllPossibleColumns_ParsesCsvIntoLines()
    {
        // Arrange - This CSV content should have ALL possible columns in it
        const string csvContent =
            @"id,FilePath,Extension,decision_datetime,CaseNo,court,appellants,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary,jurisdictions,ncn,webarchiving,uuid,skip
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,,Smith,Secretary of State for the Home Department,Immigration,Appeal Rights,Administrative Law,Judicial Review,This is a test headnote summary,,,,,
124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,,HMRC,Tax,VAT Appeals,Employment,Tribunal Procedure,Another test case,,,,,
125,/test/data/test-case3.pdf,.pdf,2025-01-17 11:00:00,GRC/2025/003,UKFTT-GRC,,Williams,DWP,Social Security,Employment Support Allowance,Benefits,Appeals Procedure,Benefits case,,[2023] EWCA Civ 123 & 124,,,
123,/test/data/test-case4.pdf,.pdf,2025-01-18 12:00:00,IA/2025/004,UKUT-IAC,Brown,,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Duplicate ID case,,,,,
126,/test/data/test-case5.docx,.docx,2025-01-19 13:00:00,IA/2025/005,UKUT-IAC,,Taylor,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Multiple Jurisdictions,""Community,Environment"",,,,
127,/test/data/test-case6.docx,.docx,2025-01-19 13:00:00,IA/2025/006,UKUT-IAC,,Taylor,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Multiple Jurisdictions with spaces,""Community, Environment,Other , Another ,"",,,,
128,/test/data/test-case7.docx,.docx,2025-01-19 13:00:00,IA/2025/007,UKUT-IAC,,Davies,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,One Jurisdiction,Environment,,,,
129,/test/data/test-case8.pdf,.pdf,2025-01-20 14:00:00,IA/2025/008,UKUT-IAC,,Berry,Home Office,,,,,With web archiving link,,,http://webarchivinglink,,
130,/test/data/test-case9.pdf,.pdf,2025-01-20 14:00:00,IA/2025/009,UKUT-IAC,,Berry,Home Office,,,,,With UUID,,,,ba2c15ca-6d3d-4550-8975-b516e3c0ed2d,n
131,/test/data/test-case10.pdf,.pdf,2025-01-20 14:00:00,IA/2025/009,UKUT-IAC,,Berry,Home Office,,,,,With Skip,,,,,skip me";

        // Arrange - Double check that csv input has all columns in case new ones are added
        var publicPropertiesInLineClass = typeof(Backlog.Src.Metadata.Line).GetProperties().Select(p => p.Name);
        var csvHeaderParts = csvContent.Split(Environment.NewLine)[0].Split(",");
        foreach (var publicProperty in publicPropertiesInLineClass)
        {
            Assert.Contains(publicProperty, csvHeaderParts, StringComparer.InvariantCultureIgnoreCase);
        }

        // Arrange - set up stream reader
        using var csvStream = new StringReader(csvContent);

        //Act
        var result = csvMetadataReader.Read(csvStream, out _);

        Assert.Collection(result,
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "123",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case.pdf",
                    Extension = ".pdf",
                    decision_datetime = new DateTime(2025, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/001",
                    Jurisdictions = [],
                    claimants = "Smith",
                    appellants = "",
                    respondent = "Secretary of State for the Home Department",
                    main_category = "Immigration",
                    main_subcategory = "Appeal Rights",
                    sec_category = "Administrative Law",
                    sec_subcategory = "Judicial Review",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "This is a test headnote summary",
                    Uuid = "",
                    Skip = false
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "124",
                    court = "UKFTT-TC",
                    FilePath = "/test/data/test-case2.docx",
                    Extension = ".docx",
                    decision_datetime = new DateTime(2025, 01, 16, 10, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/002",
                    Jurisdictions = [],
                    claimants = "",
                    appellants = "Jones",
                    respondent = "HMRC",
                    main_category = "Tax",
                    main_subcategory = "VAT Appeals",
                    sec_category = "Employment",
                    sec_subcategory = "Tribunal Procedure",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "Another test case",
                    Uuid = "",
                    Skip = false
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "125",
                    court = "UKFTT-GRC",
                    FilePath = "/test/data/test-case3.pdf",
                    Extension = ".pdf",
                    decision_datetime = new DateTime(2025, 01, 17, 11, 00, 00, DateTimeKind.Utc),
                    CaseNo = "GRC/2025/003",
                    Jurisdictions = [],
                    claimants = "Williams",
                    appellants = "",
                    respondent = "DWP",
                    main_category = "Social Security",
                    main_subcategory = "Employment Support Allowance",
                    sec_category = "Benefits",
                    sec_subcategory = "Appeals Procedure",
                    ncn = "[2023] EWCA Civ 123 & 124",
                    webarchiving = "",
                    headnote_summary = "Benefits case",
                    Uuid = "",
                    Skip = false
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "123",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case4.pdf",
                    Extension = ".pdf",
                    decision_datetime = new DateTime(2025, 01, 18, 12, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/004",
                    Jurisdictions = [],
                    claimants = "",
                    appellants = "Brown",
                    respondent = "Home Office",
                    main_category = "Immigration",
                    main_subcategory = "Entry Clearance",
                    sec_category = "Administrative Law",
                    sec_subcategory = "Case Management",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "Duplicate ID case",
                    Uuid = "",
                    Skip = false
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "126",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case5.docx",
                    Extension = ".docx",
                    decision_datetime = new DateTime(2025, 01, 19, 13, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/005",
                    Jurisdictions = ["Community", "Environment"],
                    claimants = "Taylor",
                    appellants = "",
                    respondent = "Home Office",
                    main_category = "Immigration",
                    main_subcategory = "Entry Clearance",
                    sec_category = "Administrative Law",
                    sec_subcategory = "Case Management",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "Multiple Jurisdictions",
                    Uuid = "",
                    Skip = false
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "127",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case6.docx",
                    Extension = ".docx",
                    decision_datetime = new DateTime(2025, 01, 19, 13, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/006",
                    Jurisdictions = ["Community", "Environment", "Other", "Another"],
                    claimants = "Taylor",
                    appellants = "",
                    respondent = "Home Office",
                    main_category = "Immigration",
                    main_subcategory = "Entry Clearance",
                    sec_category = "Administrative Law",
                    sec_subcategory = "Case Management",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "Multiple Jurisdictions with spaces",
                    Uuid = "",
                    Skip = false
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "128",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case7.docx",
                    Extension = ".docx",
                    decision_datetime = new DateTime(2025, 01, 19, 13, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/007",
                    Jurisdictions = ["Environment"],
                    claimants = "Davies",
                    appellants = "",
                    respondent = "Home Office",
                    main_category = "Immigration",
                    main_subcategory = "Entry Clearance",
                    sec_category = "Administrative Law",
                    sec_subcategory = "Case Management",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "One Jurisdiction",
                    Uuid = ""
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "129",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case8.pdf",
                    Extension = ".pdf",
                    decision_datetime = new DateTime(2025, 01, 20, 14, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/008",
                    Jurisdictions = [],
                    claimants = "Berry",
                    appellants = "",
                    respondent = "Home Office",
                    main_category = "",
                    main_subcategory = "",
                    sec_category = "",
                    sec_subcategory = "",
                    ncn = "",
                    webarchiving = "http://webarchivinglink",
                    headnote_summary = "With web archiving link",
                    Uuid = "",
                    Skip = false
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "130",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case9.pdf",
                    Extension = ".pdf",
                    decision_datetime = new DateTime(2025, 01, 20, 14, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/009",
                    Jurisdictions = [],
                    claimants = "Berry",
                    appellants = "",
                    respondent = "Home Office",
                    main_category = "",
                    main_subcategory = "",
                    sec_category = "",
                    sec_subcategory = "",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "With UUID",
                    Uuid = "ba2c15ca-6d3d-4550-8975-b516e3c0ed2d",
                    Skip = false
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "131",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case10.pdf",
                    Extension = ".pdf",
                    decision_datetime = new DateTime(2025, 01, 20, 14, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/009",
                    Jurisdictions = [],
                    claimants = "Berry",
                    appellants = "",
                    respondent = "Home Office",
                    main_category = "",
                    main_subcategory = "",
                    sec_category = "",
                    sec_subcategory = "",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "With Skip",
                    Uuid = "",
                    Skip = true
                }, line)
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
            1221,,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,No filepath,My category,,,
            1222,  	 ,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Still no filepath,My category,,,
            1223,NoExtension,,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Still no filepath,My category,,,
            1224,StillNoExtension, 	  ,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,Carter,Still no filepath,My category,,,
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
        Assert.Equal(9, failedToParseLines.Count);

        Assert.Equivalent(
            new List<string>
            {
                "Line 4: The FilePath field is required.",
                "Line 5: The FilePath field is required.",
                "Line 6: The Extension field is required.",
                "Line 7: The Extension field is required.",
                "Line 8: Id 123 - Must have either claimants or appellants. At least one is required.",
                "Line 9: Field at index '5' does not exist. You can ignore missing fields by setting MissingFieldFound to null. [completely invalid line]",
                "Line 10: Field at index '11' does not exist. You can ignore missing fields by setting MissingFieldFound to null. [124,MissingAComma.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,HMRC,,Bad subcategory,]",
                "Line 11: Id 125 - main_subcategory 'Bad main subcategory' cannot exist without main_category being defined",
                "Line 12: Id 126 - sec_subcategory 'Bad secondary subcategory' cannot exist without sec_category being defined"
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

        Assert.Collection(result,
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "123",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case.pdf",
                    Extension = ".pdf",
                    decision_datetime = new DateTime(2025, 01, 15, 09, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/001",
                    Jurisdictions = [],
                    claimants = "Smith",
                    appellants = "",
                    respondent = "Secretary of State for the Home Department",
                    main_category = "Immigration",
                    main_subcategory = "Appeal Rights",
                    sec_category = "Administrative Law",
                    sec_subcategory = "Judicial Review",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "This is a test headnote summary",
                    Uuid = ""
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "124",
                    court = "UKFTT-TC",
                    FilePath = "/test/data/test-case2.docx",
                    Extension = ".docx",
                    decision_datetime = new DateTime(2025, 01, 16, 10, 00, 00, DateTimeKind.Utc),
                    CaseNo = "IA/2025/002",
                    Jurisdictions = [],
                    claimants = "",
                    appellants = "Jones",
                    respondent = "HMRC",
                    main_category = "Tax",
                    main_subcategory = "VAT Appeals",
                    sec_category = "Employment",
                    sec_subcategory = "Tribunal Procedure",
                    ncn = "",
                    webarchiving = "",
                    headnote_summary = "Another test case",
                    Uuid = ""
                }, line)
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
            line => Assert.Equal(new DateTime(2025, 01, 15, 0, 0, 0, DateTimeKind.Utc), line.decision_datetime),
            line => Assert.Equal(new DateTime(2025, 01, 15, 0, 0, 0, DateTimeKind.Utc), line.decision_datetime),
            line => Assert.Equal(new DateTime(2025, 01, 15, 0, 0, 0, DateTimeKind.Utc), line.decision_datetime),
            line => Assert.Equal(new DateTime(2025, 01, 16, 10, 00, 00, DateTimeKind.Utc), line.decision_datetime)
        );

        Assert.Equivalent(
            new List<string>
            {
                "Line 6: Field '  01/16/2025 10:00:00  ' is not valid. [125,bad.pdf,.pdf,  01/16/2025 10:00:00  ,IA/2025/002,UKFTT-TC,Jones,,HMRC]",
                "Line 7: Field '  31/01/2025 10:00:00  ' is not valid. [125,bad.pdf,.pdf,  31/01/2025 10:00:00  ,IA/2025/002,UKFTT-TC,Jones,,HMRC]"
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
