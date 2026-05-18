#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Backlog.Csv;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace test.backlog.EndToEndTests;

public class MetadataTests(ITestOutputHelper testOutputHelper) : BaseEndToEndTests(testOutputHelper)
{
    private const int DocIdWithJurisdiction = 70;
    private const string Uuid = "c2d8f30f-7b43-4fdc-a54f-ac4a526fedda";
    private string? courtMetadataPath;
    private string? tempDataDir;

    protected override void Dispose(bool disposing)
    {
        if (tempDataDir is not null && Directory.Exists(tempDataDir))
        {
            Directory.Delete(tempDataDir, true);
        }

        base.Dispose(disposing);
    }

    private void ConfigureTestEnvironment(int? testJudgmentNumber)
    {
        // Create directories
        tempDataDir = Path.Combine(Path.GetTempPath(), $"FilesTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDataDir);

        var outputPath = Path.Combine(tempDataDir, "output");
        Directory.CreateDirectory(outputPath);

        var courtDocumentsDir = Path.Combine(tempDataDir, "court_documents");
        Directory.CreateDirectory(courtDocumentsDir);

        // Create files
        var contents = testJudgmentNumber is not null ? DocumentHelpers.ReadDocx(testJudgmentNumber.Value) : [1,2,3,4];
        File.WriteAllBytes(Path.Combine(courtDocumentsDir, Uuid), contents);

        // Set environment variables
        courtMetadataPath = Path.Combine(tempDataDir, "court_metadata.csv");
        var trackerPath = Path.Combine(tempDataDir, "uploaded-production.csv");

        SetPathEnvironmentVariables(tempDataDir, outputPath, courtMetadataPath, trackerPath);
    }

    private void WriteCourtMetadataCsv(params CsvLine[] metadataLines)
    {
        var headerLine =
            "id,FilePath,Extension,decision_datetime,CaseNo,court,appellants,claimants,respondent,jurisdictions,webarchiving,skip,NCN,UUID";
        var csvMetadataLines = new List<string> { headerLine };
        csvMetadataLines.AddRange(metadataLines.Select(metadataLine =>
        {
            var jurisdictions = string.Join(',', metadataLine.Jurisdictions);
            if (metadataLine.Jurisdictions.Count() > 1)
            {
                jurisdictions = $"\"{jurisdictions}\"";
            }

            var caseNumbers = string.Join(',', metadataLine.CaseNo);
            if (metadataLine.CaseNo.Length > 1)
            {
                caseNumbers = $"\"{caseNumbers}\"";
            }

            return $"{metadataLine.id},{metadataLine.FilePath},{metadataLine.Extension},{metadataLine.DecisionDateTime:yyyy-MM-dd},{caseNumbers},{metadataLine.Court},{metadataLine.Appellants},{metadataLine.Claimants},{metadataLine.Respondent},{jurisdictions},{metadataLine.WebArchiving},{(metadataLine.Skip ? "skip" : "")},{metadataLine.Ncn},{metadataLine.Uuid}";
        }));

        var metadataPath = courtMetadataPath ??
                           throw new InvalidOperationException($"{nameof(courtMetadataPath)} must be set");
        File.WriteAllLines(metadataPath, csvMetadataLines);
        PrintToOutputWithNumberedLines(csvMetadataLines);
    }

    [Fact]
    public void ProcessBacklogTribunal_DocxWithExternalMetaData_AddsMetadata()
    {
        const int docWithoutJurisdictionsId = 21;
        var originalFileName = $"test{docWithoutJurisdictionsId}.docx";

        ConfigureTestEnvironment(docWithoutJurisdictionsId);

        var metadataLine = new CsvLine
        {
            id = docWithoutJurisdictionsId.ToString(),
            FilePath = originalFileName,
            Extension = ".docx",
            DecisionDateTime = new DateTime(2099, 01, 31, 00, 00, 00, DateTimeKind.Utc),
            CaseNo = ["new case number"],
            Court = "UKUT-LC",
            Claimants = "new claimants",
            Respondent = "new respondent",
            Jurisdictions = ["new jurisdiction"],
            WebArchiving = "my web archiving link",
            Ncn = "[1989] UKUT 1234 (LC)",
            Uuid = Uuid
        };
        WriteCourtMetadataCsv(metadataLine);

        // Act
        var exitCode = Backlog.Program.Main([]);

        //Assert
        AssertProgramExitedSuccessfully(exitCode);

        var doc = GetXmlDocumentFromS3();

        // Assert xml is as expected
        doc.HasSingleNodeWithName("proprietary")
           .Which().HasChildrenMatching(
               child => child.Should().Match("uk:court", "UKUT-LC"),
               child => child.Should().Match("uk:caseNumber", "new case number"),
               child => child.Should().Match("uk:party", "new claimants", ("role", "Claimant")),
               child => child.Should().Match("uk:party", "new respondent", ("role", "Respondent")),
               child => child.Should().Match("uk:jurisdiction", "new jurisdiction"),
               child => child.Should().Match("uk:sourceFormat",
                   "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
               child => child.Should().HaveName("uk:parser"),
               child => child.Should().Match("uk:hash", "4684bfd014fadda75dc2bd683fb4edf8df0f42656a2ac85013bb3dfb14ca512e"),
               child => child.Should().Match("uk:year", "1989"),
               child => child.Should().Match("uk:number", "1234"),
               child => child.Should().Match("uk:cite", "[1989] UKUT 1234 (LC)"),
               child => child.Should().Match("uk:webarchiving", "my web archiving link")
           );

        doc.HasSingleNodeWithName("references")
           .Which().DoesNotHaveChildWithName("docJurisdiction");
    }

    [Fact]
    public void ProcessBacklogTribunal_PdfWithExternalMetaData_AddsMetadata()
    {
        var originalFileName = "test.pdf";

        ConfigureTestEnvironment(null);

        var metadataLine = new CsvLine
        {
            id = "42",
            FilePath = originalFileName,
            Extension = ".pdf",
            DecisionDateTime = new DateTime(2099, 01, 31, 00, 00, 00, DateTimeKind.Utc),
            CaseNo = ["new case number"],
            Court = "UKUT-LC",
            Ncn = "new ncn",
            Claimants = "new claimants",
            Respondent = "new respondent",
            Jurisdictions = ["new jurisdiction"],
            WebArchiving = "my web archiving link",
            Uuid = Uuid
        };
        WriteCourtMetadataCsv(metadataLine);

        // Act
        var exitCode = Backlog.Program.Main([]);

        //Assert
        AssertProgramExitedSuccessfully(exitCode);

        var doc = GetXmlDocumentFromS3();

        // Assert xml is as expected
        doc.HasSingleNodeWithName("proprietary")
           .Which().HasChildrenMatching(
               child => child.Should().Match("uk:court", "UKUT-LC"),
               child => child.Should().Match("uk:cite", "new ncn"),
               child => child.Should().Match("uk:caseNumber", "new case number"),
               child => child.Should().Match("uk:party", "new claimants", ("role", "Claimant")),
               child => child.Should().Match("uk:party", "new respondent", ("role", "Respondent")),
               child => child.Should().Match("uk:jurisdiction", "new jurisdiction"),
               child => child.Should().Match("uk:sourceFormat", "application/pdf"),
               child => child.Should().HaveName("uk:parser"),
               child => child.Should().Match("uk:year", "2099"),
               child => child.Should().Match("uk:webarchiving", "my web archiving link")
           );

        doc.HasSingleNodeWithName("references")
           .Which().DoesNotHaveChildWithName("docJurisdiction");
    }

    [Fact]
    public void ProcessBacklogTribunal_WithConflictingJurisdictionMetaData_Fails()
    {
        var originalFileName = $"test{DocIdWithJurisdiction}.docx";

        ConfigureTestEnvironment(DocIdWithJurisdiction);

        // Metadata
        var metadataLine = new CsvLine
        {
            id = DocIdWithJurisdiction.ToString(),
            FilePath = originalFileName,
            Extension = ".docx",
            DecisionDateTime = new DateTime(2099, 01, 31, 00, 00, 00, DateTimeKind.Utc),
            CaseNo = ["new case number"],
            Court = "UKFTT-GRC",
            Appellants = "new appellants",
            Respondent = "new respondent",
            Jurisdictions = ["A jurisdiction which is not in the original document"],
            Uuid = Uuid
        };
        WriteCourtMetadataCsv(metadataLine);

        // Act
        var exitCode = Backlog.Program.Main([]);

        //Assert
        Assert.True(exitCode != 0, "Expected program to error but it exited successfully");
        ConsolidatedLogger.VerifyLog<MetadataConflictException>("Jurisdictions found in document are missing in supplied outside metadata", LogLevel.Error);
    }

    [Fact]
    public void ProcessBacklogTribunal_WithAdditionalJurisdictionMetaData_AddsExtraJurisdiction()
    {
        var originalFileName = $"test{DocIdWithJurisdiction}.docx";

        ConfigureTestEnvironment(DocIdWithJurisdiction);

        // Metadata
        var metadataLine = new CsvLine
        {
            id = DocIdWithJurisdiction.ToString(),
            FilePath = originalFileName,
            Extension = ".docx",
            DecisionDateTime = new DateTime(2023, 11, 01, 00, 00, 00, DateTimeKind.Utc),
            CaseNo = ["EA/2023/0132"],
            Court = "UKFTT-GRC",
            Appellants = "NIGEL RAWLINS",
            Respondent = "THE INFORMATION COMMISSIONER",
            Jurisdictions = ["InformationRights", "new jurisdiction"],
            Uuid = Uuid
        };
        WriteCourtMetadataCsv(metadataLine);

        // Act
        var exitCode = Backlog.Program.Main([]);

        // Assert program finished successfully
        AssertProgramExitedSuccessfully(exitCode);

        var doc = GetXmlDocumentFromS3();

        // Assert xml is as expected
        doc.HasSingleNodeWithName("proprietary")
           .Which().HasChildMatching("uk:jurisdiction", "InformationRights")
           .And().HasChildMatching("uk:jurisdiction", "new jurisdiction");

        doc.HasSingleNodeWithName("references")
           .Which().HasChildrenMatching(
               node => node.Should().HaveName("TLCOrganization")
                           .And().Attributes.ThatMatch(
                               ("eId", "ukftt-grc"),
                               ("href",
                                   "https://www.gov.uk/courts-tribunals/first-tier-tribunal-general-regulatory-chamber"),
                               ("showAs", "United Kingdom First-tier Tribunal (General Regulatory Chamber)")),
               node => node.Should().HaveName("TLCOrganization")
                           .And().Attributes.ThatMatch(
                               ("eId", "tna"),
                               ("href", "https://www.nationalarchives.gov.uk/"),
                               ("showAs", "The National Archives")),
               node => node.Should().HaveName("TLCEvent")
                           .And().Attributes.ThatMatch(
                               ("eId", "decision"),
                               ("href", "#"),
                               ("showAs", "decision")),
               node => node.Should().HaveName("TLCConcept")
                           .And().Attributes.ThatMatch(
                               ("eId", "jurisdiction-informationrights"),
                               ("href", "/jurisdiction/informationrights"),
                               ("showAs", "Information Rights"),
                               ("shortForm", "InformationRights"))
           );

        doc.HasSingleNodeWithName("docJurisdiction")
           .Which().Should().HaveValueMatching("Information Rights")
           .And().Attributes.ThatMatch(
               ("refersTo", "#jurisdiction-informationrights"),
               ("style", "font-weight:bold;font-family:Arial"));
    }

    private XmlDocument GetXmlDocumentFromS3()
    {
        var key = mockS3Client.CapturedKeys.Single();
        var actualXml = ZipFileHelpers.GetFileFromZippedContent(mockS3Client.GetCapturedContent(key), @"\.xml$");
        PrintToOutputWithNumberedLines(actualXml);
        var doc = new XmlDocument();
        doc.LoadXml(actualXml);
        return doc;
    }

    [Fact]
    public void ProcessBacklogTribunal_WithSameMetaDataAsInDocument_DoesNotChangeOutput()
    {
        var originalFileName = $"test{DocIdWithJurisdiction}.docx";

        ConfigureTestEnvironment(DocIdWithJurisdiction);

        // Metadata
        var metadataLine = new CsvLine
        {
            id = DocIdWithJurisdiction.ToString(),
            FilePath = originalFileName,
            Extension = ".docx",
            DecisionDateTime = new DateTime(2023, 11, 01, 00, 00, 00, DateTimeKind.Utc),
            CaseNo = ["EA/2023/0132"],
            Ncn = "[2023] UKFTT 916 (GRC)",
            Court = "UKFTT-GRC",
            Appellants = "NIGEL RAWLINS",
            Respondent = "THE INFORMATION COMMISSIONER",
            Jurisdictions = ["InformationRights"],
            Uuid = Uuid
        };
        WriteCourtMetadataCsv(metadataLine);

        // Act
        var exitCode = Backlog.Program.Main([]);

        // Assert
        AssertProgramExitedSuccessfully(exitCode);

        var doc = GetXmlDocumentFromS3();

        doc.HasSingleNodeWithName("proprietary")
           .Which().HasChildrenMatching(
               child => child.Should().Match("uk:court", "UKFTT-GRC"),
               child => child.Should().Match("uk:year", "2023"),
               child => child.Should().Match("uk:number", "916"),
               child => child.Should().Match("uk:cite", "[2023] UKFTT 916 (GRC)"),
               child => child.Should().Match("uk:caseNumber", "EA/2023/0132"),
               child => child.Should().Match("uk:jurisdiction", "InformationRights"),
               child => child.Should().Match("uk:party", "NIGEL RAWLINS", ("role", "Appellant")),
               child => child.Should().Match("uk:party", "THE INFORMATION COMMISSIONER", ("role", "Respondent")),
               child => child.Should().Match("uk:sourceFormat",
                   "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
               child => child.Should().HaveName("uk:parser"),
               child => child.Should().Match("uk:hash",
                   "134025e65cf965cd195d28246b1713ee78d93731fe751d724fc236b90626f9bc")
           );

        doc.HasSingleNodeWithName("references")
           .Which().HasChildrenMatching(
               node => node.Should().HaveName("TLCOrganization")
                           .And().Attributes.ThatMatch(
                               ("eId", "ukftt-grc"),
                               ("href",
                                   "https://www.gov.uk/courts-tribunals/first-tier-tribunal-general-regulatory-chamber"),
                               ("showAs", "United Kingdom First-tier Tribunal (General Regulatory Chamber)")),
               node => node.Should().HaveName("TLCOrganization")
                           .And().Attributes.ThatMatch(
                               ("eId", "tna"),
                               ("href", "https://www.nationalarchives.gov.uk/"),
                               ("showAs", "The National Archives")),
               node => node.Should().HaveName("TLCEvent")
                           .And().Attributes.ThatMatch(
                               ("eId", "decision"),
                               ("href", "#"),
                               ("showAs", "decision")),
               node => node.Should().HaveName("TLCConcept")
                           .And().Attributes.ThatMatch(
                               ("eId", "jurisdiction-informationrights"),
                               ("href", "/jurisdiction/informationrights"),
                               ("showAs", "Information Rights"),
                               ("shortForm", "InformationRights"))
           );

        doc.HasSingleNodeWithName("docJurisdiction")
           .Which().Should().HaveValueMatching("Information Rights")
           .And().Attributes.ThatMatch(
               ("refersTo", "#jurisdiction-informationrights"),
               ("style", "font-weight:bold;font-family:Arial"));
    }
}
