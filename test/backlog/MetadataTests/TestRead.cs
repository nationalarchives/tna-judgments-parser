#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Backlog.Src;

using test.Mocks;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestRead
{
    private readonly Metadata csvMetadataReader = new(new MockLogger<Metadata>().Object);

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
                    decision_datetime = "2025-01-15 09:00:00",
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
                    decision_datetime = "2025-01-16 10:00:00",
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
            @"id,FilePath,Extension,decision_datetime,CaseNo,court,appellants,claimants,respondent,main_category,main_subcategory,sec_category,sec_subcategory,headnote_summary,jurisdictions,ncn,webarchiving
123,/test/data/test-case.pdf,.pdf,2025-01-15 09:00:00,IA/2025/001,UKUT-IAC,,Smith,Secretary of State for the Home Department,Immigration,Appeal Rights,Administrative Law,Judicial Review,This is a test headnote summary,,,
124,/test/data/test-case2.docx,.docx,2025-01-16 10:00:00,IA/2025/002,UKFTT-TC,Jones,,HMRC,Tax,VAT Appeals,Employment,Tribunal Procedure,Another test case,,,
125,/test/data/test-case3.pdf,.pdf,2025-01-17 11:00:00,GRC/2025/003,UKFTT-GRC,,Williams,DWP,Social Security,Employment Support Allowance,Benefits,Appeals Procedure,Benefits case,,[2023] EWCA Civ 123 & 124,
123,/test/data/test-case4.pdf,.pdf,2025-01-18 12:00:00,IA/2025/004,UKUT-IAC,Brown,,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Duplicate ID case,,,
126,/test/data/test-case5.docx,.docx,2025-01-19 13:00:00,IA/2025/005,UKUT-IAC,,Taylor,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Multiple Jurisdictions,""Community,Environment"",,
127,/test/data/test-case6.docx,.docx,2025-01-19 13:00:00,IA/2025/006,UKUT-IAC,,Taylor,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,Multiple Jurisdictions with spaces,""Community, Environment,Other , Another ,"",,
128,/test/data/test-case7.docx,.docx,2025-01-19 13:00:00,IA/2025/007,UKUT-IAC,,Davies,Home Office,Immigration,Entry Clearance,Administrative Law,Case Management,One Jurisdiction,Environment,,
129,/test/data/test-case8.pdf,.pdf,2025-01-20 14:00:00,IA/2025/008,UKUT-IAC,,Berry,Home Office,,,,,With web archiving link,,,http://webarchivinglink";

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
                    decision_datetime = "2025-01-15 09:00:00",
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
                    headnote_summary = "This is a test headnote summary"
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "124",
                    court = "UKFTT-TC",
                    FilePath = "/test/data/test-case2.docx",
                    Extension = ".docx",
                    decision_datetime = "2025-01-16 10:00:00",
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
                    headnote_summary = "Another test case"
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "125",
                    court = "UKFTT-GRC",
                    FilePath = "/test/data/test-case3.pdf",
                    Extension = ".pdf",
                    decision_datetime = "2025-01-17 11:00:00",
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
                    headnote_summary = "Benefits case"
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "123",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case4.pdf",
                    Extension = ".pdf",
                    decision_datetime = "2025-01-18 12:00:00",
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
                    headnote_summary = "Duplicate ID case"
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "126",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case5.docx",
                    Extension = ".docx",
                    decision_datetime = "2025-01-19 13:00:00",
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
                    headnote_summary = "Multiple Jurisdictions"
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "127",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case6.docx",
                    Extension = ".docx",
                    decision_datetime = "2025-01-19 13:00:00",
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
                    headnote_summary = "Multiple Jurisdictions with spaces"
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "128",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case7.docx",
                    Extension = ".docx",
                    decision_datetime = "2025-01-19 13:00:00",
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
                    headnote_summary = "One Jurisdiction"
                }, line),
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "129",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case8.pdf",
                    Extension = ".pdf",
                    decision_datetime = "2025-01-20 14:00:00",
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
                    headnote_summary = "With web archiving link"
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

        Assert.Collection(result,
            line => Assert.Equivalent(
                new Backlog.Src.Metadata.Line
                {
                    id = "123",
                    court = "UKUT-IAC",
                    FilePath = "/test/data/test-case.pdf",
                    Extension = ".pdf",
                    decision_datetime = "2025-01-15 09:00:00",
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
                    decision_datetime = "2025-01-16 10:00:00",
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
}
