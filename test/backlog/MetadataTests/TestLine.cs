#nullable enable

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestLine
{

    [Fact]
    public void Line_FirstPartyName_WithClaimants_ReturnsClaimants()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLine with { claimants = "John Smith", respondent = "HMRC" };

        // Act
        var result = line.FirstPartyName;

        // Assert
        Assert.Equal("John Smith", result);
    }

    [Fact]
    public void Line_FirstPartyName_WithAppellants_ReturnsAppellants()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLine with { appellants = "Jane Doe", respondent = "HMRC" };

        // Act
        var result = line.FirstPartyName;

        // Assert
        Assert.Equal("Jane Doe", result);
    }

    [Fact]
    public void Line_FirstPartyRole_WithClaimants_ReturnsClaimant()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLine with { claimants = "John Smith", respondent = "HMRC" };

        // Act
        var result = line.FirstPartyRole;

        // Assert
        Assert.Equal(PartyRole.Claimant, result);
    }

    [Fact]
    public void Line_FirstPartyRole_WithAppellants_ReturnsAppellant()
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLine with { appellants = "Jane Doe", respondent = "HMRC" };

        // Act
        var result = line.FirstPartyRole;

        // Assert
        Assert.Equal(PartyRole.Appellant, result);
    }
}
