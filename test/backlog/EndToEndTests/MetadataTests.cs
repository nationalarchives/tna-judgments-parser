#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Backlog.Src;

using Xunit;

namespace test.backlog.EndToEndTests;

public class MetadataTests(ITestOutputHelper testOutputHelper) : BaseEndToEndTests(testOutputHelper)
{
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

    private void ConfigureTestEnvironment(int testJudgmentNumber, string originalFileName)
    {
        // Create directories
        tempDataDir = Path.Combine(Path.GetTempPath(), $"FilesTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDataDir);

        var outputPath = Path.Combine(tempDataDir, "output");
        Directory.CreateDirectory(outputPath);

        var courtDocumentsDir = Path.Combine(tempDataDir, "court_documents");
        Directory.CreateDirectory(courtDocumentsDir);

        var tdrMetadataDir = Path.Combine(tempDataDir, "tdr_metadata");
        Directory.CreateDirectory(tdrMetadataDir);

        // Create files
        const string uuid = "test-uuid-12345";
        WriteEmbeddedFileToTempFolder(courtDocumentsDir, uuid, DocumentHelpers.ReadDocx(testJudgmentNumber));
        WriteTransferMetaDataCsv(uuid, tdrMetadataDir, originalFileName);

        // Set environment variables
        courtMetadataPath = Path.Combine(tempDataDir, "court_metadata.csv");
        var trackerPath = Path.Combine(tempDataDir, "uploaded-production.csv");
        var bulkNumbersPath = Path.Combine(tempDataDir, "bulk_numbers.csv");

        SetPathEnvironmentVariables(tempDataDir, outputPath, courtMetadataPath, trackerPath, bulkNumbersPath);
    }

    private static void WriteEmbeddedFileToTempFolder(string directoryPath, string fileName, byte[] contents)
    {
        var filePath = Path.Combine(directoryPath, fileName);
        File.WriteAllBytes(filePath, contents);
    }

    private static void WriteTransferMetaDataCsv(string uuid, string tdrMetadataDir, string originalFileName)
    {
        const string hmctsFilePath = "data/HMCTS_Judgment_Files/";
        Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", hmctsFilePath);
        var transferMetadataContent =
            $@"file_reference,file_name,file_type,file_size,clientside_original_filepath,rights_copyright,legal_status,held_by,date_last_modified,closure_type,closure_start_date,closure_period,foi_exemption_code,foi_exemption_asserted,title_closed,title_alternate,description,description_closed,description_alternate,language,end_date,file_name_translation,original_filepath,parent_reference,former_reference_department,UUID
TEST1,{originalFileName},File,1024,{hmctsFilePath}{originalFileName},Crown Copyright,Public Record(s),""The National Archives, Kew"",2023-01-01T00:00:00,Open,,,,,false,,,false,,English,,,,,,{uuid}";
        var transferMetadataPath = Path.Combine(tdrMetadataDir, "file-metadata.csv");
        File.WriteAllText(transferMetadataPath, transferMetadataContent);
    }

    private void WriteCourtMetadataCsv(int testJudgementNumber, string originalFileName,
        params Metadata.Line[] metadataLines)
    {
        const string judgmentsFilePath = @"JudgmentFiles\";
        Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", judgmentsFilePath);

        var headerLine = "id,FilePath,Extension,decision_datetime,CaseNo,court,appellants,claimants,respondent";
        var csvMetadataLines = new List<string> { headerLine };
        csvMetadataLines.AddRange(metadataLines.Select(metadataLine =>
            $"{testJudgementNumber},{judgmentsFilePath}{originalFileName},{metadataLine.Extension},{metadataLine.decision_datetime},{metadataLine.CaseNo},{metadataLine.court},{metadataLine.appellants},{metadataLine.claimants},{metadataLine.respondent}"));

        var metadataPath = courtMetadataPath ??
                           throw new InvalidOperationException($"{nameof(courtMetadataPath)} must be set");
        File.WriteAllLines(metadataPath, csvMetadataLines);
        PrintToOutputWithNumberedLines(csvMetadataLines);
    }

    [Fact]
    public void ProcessBacklogTribunal_WithMetaDataOverrides_OverridesMetadata()
    {
        // Test 70 has judgments
        const int docId = 70;
        var originalFileName = $"test{docId}.docx";

        ConfigureTestEnvironment(docId, originalFileName);

        // Metadata
        var metadataLine = new Metadata.Line
        {
            Extension = ".docx",
            decision_datetime = "2099-01-31 00:00:00",
            CaseNo = "new case number",
            court = "UKFTT-GRC",
            appellants = "new appellants",
            respondent = "new respondent"
        };
        WriteCourtMetadataCsv(docId, originalFileName, metadataLine);

        // Act
        var exitCode = Backlog.Src.Program.Main([]);

        //Assert
        AssertProgramExitedSuccessfully(exitCode);

        var doc = GetXmlDocumentFromS3();

        // Assert xml is as expected
        doc.HasSingleNodeWithName("proprietary")
           .Which().HasChildrenMatching(
               child => child.Should().Match("uk:court", "UKFTT-GRC"),
               child => child.Should().Match("uk:year", "2023"),
               child => child.Should().Match("uk:number", "916"),
               child => child.Should().Match("uk:cite", "[2023] UKFTT 916 (GRC)"),
               child => child.Should().Match("uk:caseNumber", "new case number"),
               child => child.Should().Match("uk:jurisdiction", "InformationRights"),
               child => child.Should().Match("uk:party", "new appellants", ("role", "Appellant")),
               child => child.Should().Match("uk:party", "new respondent", ("role", "Respondent")),
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
        // Test 70 has judgments
        const int docId = 70;
        var originalFileName = $"test{docId}.docx";

        ConfigureTestEnvironment(docId, originalFileName);

        // Metadata
        var metadataLine = new Metadata.Line
        {
            Extension = ".docx",
            decision_datetime = "2023-11-01 00:00:00",
            CaseNo = "EA/2023/0132",
            court = "UKFTT-GRC",
            appellants = "NIGEL RAWLINS",
            respondent = "THE INFORMATION COMMISSIONER"
        };
        WriteCourtMetadataCsv(docId, originalFileName, metadataLine);

        // Act
        var exitCode = Backlog.Src.Program.Main([]);

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
