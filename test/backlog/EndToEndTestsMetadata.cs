#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Backlog.Src;

using test;
using test.backlog;

using UK.Gov.NationalArchives.CaseLaw;

using Xunit;
using Xunit.Abstractions;
namespace Backlog.Test;

public class EndToEndTestsMetadata : IDisposable
{
    private readonly MockS3Client mockS3Client = new();
    private readonly ITestOutputHelper testOutputHelper;
    private string bulkNumbersPath;
    private string courtMetadataPath;
    private string dataDir;
    private string outputPath;
    private string? tempDataDir;
    private string trackerPath;

    public EndToEndTestsMetadata(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;

        Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");
        Bucket.Configure(mockS3Client.Object, MockS3Client.TestBucket);
    }

    public void Dispose()
    {
        // Clean up environment variables
        Environment.SetEnvironmentVariable("COURT_METADATA_PATH", null);
        Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", null);
        Environment.SetEnvironmentVariable("TRACKER_PATH", null);
        Environment.SetEnvironmentVariable("OUTPUT_PATH", null);
        Environment.SetEnvironmentVariable("BUCKET_NAME", null);
        Environment.SetEnvironmentVariable("AWS_REGION", null);
        Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", null);
        Environment.SetEnvironmentVariable("HMCTS_FILES_PATH", null);

        // Only clean up temp data, output files and tracker, leave test data intact
        if (File.Exists(trackerPath))
            File.Delete(trackerPath);
        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);
        if (tempDataDir is not null && Directory.Exists(tempDataDir))
            Directory.Delete(tempDataDir, true);
    }


    private void ConfigureTestEnvironment(int testJudgmentNumber, string originalFileName)
    {
        const string uuid = "test-uuid-12345";

        var (courtDocumentsDir, tdrMetadataDir) = CreateTempDirectories();
        WriteEmbeddedFileToTempFolder(courtDocumentsDir, uuid, DocumentHelpers.ReadDocx(testJudgmentNumber));
        WriteTransferMetaDataCsv(uuid, tdrMetadataDir, originalFileName);

        // Work out other paths
        dataDir = tempDataDir;
        trackerPath = Path.Combine(dataDir, "uploaded-production.csv");
        outputPath = Path.Combine(dataDir, "output");
        bulkNumbersPath = Path.Combine(dataDir, "bulk_numbers.csv");

        // Create the output directory
        Directory.CreateDirectory(outputPath);

        // Set environment variables for this test
        Environment.SetEnvironmentVariable("COURT_METADATA_PATH", courtMetadataPath);
        Environment.SetEnvironmentVariable("DATA_FOLDER_PATH", dataDir);
        Environment.SetEnvironmentVariable("TRACKER_PATH", trackerPath);
        Environment.SetEnvironmentVariable("OUTPUT_PATH", outputPath);
        Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", bulkNumbersPath);
    }

    private (string courtDocumentsDir, string tdrMetadataDir) CreateTempDirectories()
    {
        tempDataDir = Path.Combine(Path.GetTempPath(), $"FilesTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDataDir);

        courtMetadataPath = Path.Combine(tempDataDir, "court_metadata.csv");

        var courtDocumentsDir = Path.Combine(tempDataDir, "court_documents");
        Directory.CreateDirectory(courtDocumentsDir);

        var tdrMetadataDir = Path.Combine(tempDataDir, "tdr_metadata");
        Directory.CreateDirectory(tdrMetadataDir);

        return (courtDocumentsDir, tdrMetadataDir);
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

    private void WriteCourtMetadataCsv(int testJudgementNumber, string originalFileName, params Metadata.Line[] metadataLines)
    {
        const string judgmentsFilePath = @"JudgmentFiles\";
        Environment.SetEnvironmentVariable("JUDGMENTS_FILE_PATH", judgmentsFilePath);

        var headerLine = "id,FilePath,Extension,decision_datetime,CaseNo,court,appellants,claimants,respondent,jurisdictions";
        var csvMetadataLines = new List<string> { headerLine };
        csvMetadataLines.AddRange(metadataLines.Select(metadataLine =>
        {
            var jurisdictions = string.Join(',', metadataLine.Jurisdictions);
            if (metadataLine.Jurisdictions.Count() > 1)
                jurisdictions = $"\"{jurisdictions}\"";

            return $"{testJudgementNumber},{judgmentsFilePath}{originalFileName},{metadataLine.Extension},{metadataLine.decision_datetime},{metadataLine.CaseNo},{metadataLine.court},{metadataLine.appellants},{metadataLine.claimants},{metadataLine.respondent},{jurisdictions}";
        }));
        File.WriteAllLines(courtMetadataPath, csvMetadataLines);
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
            respondent = "new respondent",
            Jurisdictions = ["new jurisdiction"]
        };
        WriteCourtMetadataCsv(docId, originalFileName, metadataLine);

        // Act
        var exitCode = Src.Program.Main([]);

        // Assert program finished successfully
        Assert.Equal(0, exitCode);

        var actualXml = mockS3Client.GetFileFromCapturedContent(".xml");
        PrintToOutputWithNumberedLines(actualXml);

        var doc = new XmlDocument();
        doc.LoadXml(actualXml);

        // Assert xml is as expected
        doc.HasSingleNodeMatching("proprietary")
            .With().ChildrenMatching(
                child => child.ShouldMatch("uk:court", "UKFTT-GRC"),
                child => child.ShouldMatch("uk:year", "2023"),
                child => child.ShouldMatch("uk:number", "916"),
                child => child.ShouldMatch("uk:cite", "[2023] UKFTT 916 (GRC)"),
                child => child.ShouldMatch("uk:caseNumber", "new case number"),
                child => child.ShouldMatch("uk:jurisdiction", "new jurisdiction"),
                child => child.ShouldMatch("uk:party", "new appellants", ("role", "Appellant")),
                child => child.ShouldMatch("uk:party", "new respondent", ("role", "Respondent")),
                child => child.ShouldMatch("uk:sourceFormat", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
                child => child.ShouldHaveName("uk:parser"),
                child => child.ShouldMatch("uk:hash", "134025e65cf965cd195d28246b1713ee78d93731fe751d724fc236b90626f9bc")
            );

        doc.HasSingleNodeMatching("references")
            .With().ChildrenMatching(
                node => node.ShouldHaveName("TLCOrganization")
                    .And().Attributes.ThatMatch(
                        ("eId", "ukftt-grc"),
                        ("href", "https://www.gov.uk/courts-tribunals/first-tier-tribunal-general-regulatory-chamber"),
                        ("showAs", "United Kingdom First-tier Tribunal (General Regulatory Chamber)")),
                node => node.ShouldHaveName("TLCOrganization")
                    .And().Attributes.ThatMatch(
                        ("eId", "tna"),
                        ("href", "https://www.nationalarchives.gov.uk/"),
                        ("showAs", "The National Archives")),
                node => node.ShouldHaveName("TLCEvent")
                    .And().Attributes.ThatMatch(
                        ("eId", "decision"),
                        ("href", "#"),
                        ("showAs", "decision")),
                node => node.ShouldHaveName("TLCConcept")
                    .And().Attributes.ThatMatch(
                        ("eId", "jurisdiction-informationrights"),
                        ("href", "/jurisdiction/informationrights"),
                        ("showAs", "Information Rights"),
                        ("shortForm", "InformationRights"))
            );

        doc.HasSingleNodeMatching("docJurisdiction")
            .Which().ShouldHaveValueMatching("Information Rights")
            .And().Attributes.ThatMatch(
                ("refersTo", "#jurisdiction-informationrights"),
                ("style", "font-weight:bold;font-family:Arial"));
    }
    private void PrintToOutputWithNumberedLines(string textToPrint)
    {
        PrintToOutputWithNumberedLines(textToPrint.Split(Environment.NewLine));
    }
    private void PrintToOutputWithNumberedLines(IEnumerable<string> lines)
    {
        var currentLineNumber = 1;
        foreach (var line in lines) {
            var numberedLine = $"{currentLineNumber}: {line}";
            testOutputHelper.WriteLine(numberedLine);
            currentLineNumber++;
        }
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
        var exitCode = Src.Program.Main([]);

        // Assert program finished successfully
        Assert.Equal(0, exitCode);

        var actualXml = mockS3Client.GetFileFromCapturedContent(".xml");

        testOutputHelper.WriteLine(actualXml);

        var doc = new XmlDocument();
        doc.LoadXml(actualXml);

        // Assert xml is as expected
        doc.HasSingleNodeMatching("proprietary")
            .With().ChildrenMatching(
                child => child.ShouldMatch("uk:court", "UKFTT-GRC"),
                child => child.ShouldMatch("uk:year", "2023"),
                child => child.ShouldMatch("uk:number", "916"),
                child => child.ShouldMatch("uk:cite", "[2023] UKFTT 916 (GRC)"),
                child => child.ShouldMatch("uk:caseNumber", "EA/2023/0132"),
                child => child.ShouldMatch("uk:jurisdiction", "InformationRights"),
                child => child.ShouldMatch("uk:party", "NIGEL RAWLINS", ("role", "Appellant")),
                child => child.ShouldMatch("uk:party", "THE INFORMATION COMMISSIONER", ("role", "Respondent")),
                child => child.ShouldMatch("uk:sourceFormat", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
                child => child.ShouldHaveName("uk:parser"),
                child => child.ShouldMatch("uk:hash", "134025e65cf965cd195d28246b1713ee78d93731fe751d724fc236b90626f9bc")
            );

        doc.HasSingleNodeMatching("references")
            .With().ChildrenMatching(
                node => node.ShouldHaveName("TLCOrganization")
                    .And().Attributes.ThatMatch(
                        ("eId", "ukftt-grc"),
                        ("href", "https://www.gov.uk/courts-tribunals/first-tier-tribunal-general-regulatory-chamber"),
                        ("showAs", "United Kingdom First-tier Tribunal (General Regulatory Chamber)")),
                node => node.ShouldHaveName("TLCOrganization")
                    .And().Attributes.ThatMatch(
                        ("eId", "tna"),
                        ("href", "https://www.nationalarchives.gov.uk/"),
                        ("showAs", "The National Archives")),
                node => node.ShouldHaveName("TLCEvent")
                    .And().Attributes.ThatMatch(
                        ("eId", "decision"),
                        ("href", "#"),
                        ("showAs", "decision")),
                node => node.ShouldHaveName("TLCConcept")
                    .And().Attributes.ThatMatch(
                        ("eId", "jurisdiction-informationrights"),
                        ("href", "/jurisdiction/informationrights"),
                        ("showAs", "Information Rights"),
                        ("shortForm", "InformationRights"))
            );

        doc.HasSingleNodeMatching("docJurisdiction")
            .Which().ShouldHaveValueMatching("Information Rights")
            .And().Attributes.ThatMatch(
                ("refersTo", "#jurisdiction-informationrights"),
                ("style", "font-weight:bold;font-family:Arial"));
    }
}
