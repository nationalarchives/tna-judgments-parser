#nullable enable

using Xunit;
namespace test.backlog.Metadata;

public class TestMakeMetadata
{
    private Backlog.Src.Metadata.Line CreateLineWith(
        string id = "123",
        string court = "UKFTT-GRC",
        string[]? jurisdictions = null,
        string decisionDatetime = "2023-01-14 14:30:00",
        string caseNo = "ABC/2023/001",
        string claimants = "John Smith",
        string respondent = "HMRC",
        string mainCategory = "Immigration",
        string extension = ".pdf"
    )
    {
        return new Backlog.Src.Metadata.Line
        {
            id = id,
            court = court,
            Jurisdictions = jurisdictions ?? [],
            decision_datetime = decisionDatetime,
            CaseNo = caseNo,
            claimants = claimants,
            respondent = respondent,
            main_category = mainCategory,
            Extension = extension
        };
    }

    [Fact]
    public void MakeMetadata_WithJurisdiction_SetsJurisdictionProperty()
    {
        // Arrange
        var line = CreateLineWith(jurisdictions: ["Transport"]);

        // Act
        var result = Backlog.Src.Metadata.MakeMetadata(line);

        // Assert
        var actualJurisdiction = Assert.Single(result.Jurisdictions);
        Assert.Equal("Transport", actualJurisdiction.ShortName);
    }

    [Fact]
    public void MakeMetadata_WithoutJurisdiction_JurisdictionPropertyIsEmpty()
    {
        // Arrange
        var line = CreateLineWith(jurisdictions: []);

        // Act
        var result = Backlog.Src.Metadata.MakeMetadata(line);

        // Assert
        Assert.Empty(result.Jurisdictions);
    }

    [Fact]
    public void MakeMetadata_WithWhitespaceJurisdiction_JurisdictionPropertyIsEmpty()
    {
        // Arrange
        var line = CreateLineWith(jurisdictions: ["   "]);

        // Act
        var result = Backlog.Src.Metadata.MakeMetadata(line);

        // Assert
        Assert.Empty(result.Jurisdictions);
    }

    [Fact]
    public void MakeMetadata_WithWhitespaceAndNonWhitespaceJurisdictions_IgnoresBlankJurisdictions()
    {
        // Arrange
        var line = CreateLineWith(jurisdictions: ["   ", "Transport", ""]);

        // Act
        var result = Backlog.Src.Metadata.MakeMetadata(line);

        // Assert
        var actualJurisdiction = Assert.Single(result.Jurisdictions);
        Assert.Equal("Transport", actualJurisdiction.ShortName);
    }
}
