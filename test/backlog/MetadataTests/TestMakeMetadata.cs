#nullable enable

using System;

using Backlog.Csv;

using Backlog.Src;

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestMakeMetadata
{
    [Fact]
    public void MakeMetadata_WithBasicLine_CreatesCorrectMetadata()
    {
        // Arrange
        var line = new CsvLine
        {
            id = "123",
            court = "UKFTT-GRC",
            decision_datetime = new DateTime(2023, 01, 14,  14, 30, 00, DateTimeKind.Utc),
            CaseNo = "ABC/2023/001",
            claimants = "John Smith",
            respondent = "HMRC",
            headnote_summary = "This is a test headnote summary",
            main_category = "Immigration",
            main_subcategory = "Asylum",
            sec_category = "Human Rights",
            sec_subcategory = "Article 8",
            FilePath = "/path/to/test-document.pdf",
            Extension = ".pdf"
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(JudgmentType.Decision, result.Type);
        Assert.Equal(Courts.FirstTierTribunal_GRC, result.Court);
        Assert.Equal("2023-01-14", result.Date.Date);
        Assert.Equal("decision", result.Date.Name);
        Assert.Equal("John Smith v HMRC", result.Name);
        Assert.NotNull(result.CaseNumbers);
        var caseNumber = Assert.Single(result.CaseNumbers);
        Assert.Equal("ABC/2023/001", caseNumber);
        Assert.Equal("application/pdf", result.SourceFormat);
        Assert.Equal(2, result.Parties.Count);

        var firstParty = result.Parties.Find(p => p.Role == PartyRole.Claimant);
        var secondParty = result.Parties.Find(p => p.Role == PartyRole.Respondent);

        Assert.NotNull(firstParty);
        Assert.Equal("John Smith", firstParty.Name);
        Assert.NotNull(secondParty);
        Assert.Equal("HMRC", secondParty.Name);
    }

    [Fact]
    public void MakeMetadata_WithAppellants_CreatesCorrectMetadata()
    {
        // Arrange
        var line = new CsvLine
        {
            id = "124",
            FilePath = "/test/data/test.pdf",
            court = "UKFTT-GRC",
            decision_datetime = new DateTime(2023, 01, 14,  14, 30, 00, DateTimeKind.Utc),
            CaseNo = "ABC/2023/002",
            appellants = "Jane Doe",
            respondent = "Home Office",
            main_category = "Immigration",
            main_subcategory = "Asylum",
            Extension = ".pdf"
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(JudgmentType.Decision, result.Type);
        Assert.Equal(Courts.FirstTierTribunal_GRC, result.Court);
        Assert.Equal("2023-01-14", result.Date.Date);
        Assert.Equal("decision", result.Date.Name);
        Assert.Equal("Jane Doe v Home Office", result.Name);
        Assert.NotNull(result.CaseNumbers);
        var caseNumber = Assert.Single(result.CaseNumbers);
        Assert.Equal("ABC/2023/002", caseNumber);
        Assert.Equal("application/pdf", result.SourceFormat);
        Assert.Equal(2, result.Parties.Count);

        var firstParty = result.Parties.Find(p => p.Role == PartyRole.Appellant);
        var secondParty = result.Parties.Find(p => p.Role == PartyRole.Respondent);

        Assert.NotNull(firstParty);
        Assert.Equal("Jane Doe", firstParty.Name);
        Assert.NotNull(secondParty);
        Assert.Equal("Home Office", secondParty.Name);
    }

    [Fact]
    public void MakeMetadata_WithDocxFile_SetsCorrectSourceFormat()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Extension = ".docx"
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.SourceFormat);
    }

    [Fact]
    public void MakeMetadata_WithDocFile_SetsCorrectSourceFormat()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Extension = ".doc"
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.SourceFormat);
    }

    [Fact]
    public void MakeMetadata_WithUnsupportedExtension_ThrowsException()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Extension = ".txt"
        };

        // Act & Assert
        var ex = Assert.Throws<Exception>(() => MetadataTransformer.MakeMetadata(line));
        Assert.Equal("Unexpected extension .txt", ex.Message);
    }

    [Fact]
    public void MakeMetadata_WithNewDate_UsesFirstTierTribunal()
    {
        // Arrange - Date on or after 2010-01-18
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            court = "UKFTT-GRC",
            decision_datetime = new DateTime(2010, 01, 10,  14, 30, 00, DateTimeKind.Utc)
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.Equal(Courts.FirstTierTribunal_GRC, result.Court);
    }

    [Fact]
    public void MakeMetadata_WithComplexFileNumbers_CreatesCorrectCaseNumber()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            CaseNo = "IA/12345/2023",
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.Equal("IA/12345/2023", result.CaseNumbers[0]);
    }

    [Fact]
    public void MakeMetadata_DecisionDate_ParsesCorrectly()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            decision_datetime = new DateTime(2023, 12, 25,  15, 45, 30, DateTimeKind.Utc),
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.Equal("2023-12-25", result.Date.Date);
        Assert.Equal("decision", result.Date.Name);
    }

    [Fact]
    public void MakeMetadata_WithNCN_SetsNCNProperty()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            ncn = "[2023] UKUT 123 (IAC)"
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("[2023] UKUT 123 (IAC)", result.NCN);
    }

    [Fact]
    public void MakeMetadata_WithoutNCN_NCNPropertyIsNull()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            // ncn is not set
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.NCN);
    }

    [Fact]
    public void MakeMetadata_WithWebArchiving_SetsWebArchivingLinkProperty()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            webarchiving = "http://webarchivelink"
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.Equal("http://webarchivelink", result.WebArchivingLink);
    }

    [Fact]
    public void MakeMetadata_WithJurisdiction_SetsJurisdictionProperty()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
           Jurisdictions = ["Transport"]
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        var actualJurisdiction = Assert.Single(result.Jurisdictions);
        Assert.Equal("Transport", actualJurisdiction.ShortName);
    }

    [Fact]
    public void MakeMetadata_WithoutJurisdiction_JurisdictionPropertyIsEmpty()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLineWithClaimants with
        {
            Jurisdictions = []
        };

        // Act
        var result = MetadataTransformer.MakeMetadata(line);

        // Assert
        Assert.Empty(result.Jurisdictions);
    }
}
