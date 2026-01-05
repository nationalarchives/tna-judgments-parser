using System;

using Backlog.Src;

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestLine
{
    [Fact]
    public void Line_DecisionDate_Property_ParsesCorrectly()
    {
        // Arrange
        var line = new Metadata.Line { court = "UKFTT-GRC", decision_datetime = "2023-07-04 09:15:22" };

        // Act
        var decisionDate = line.DecisionDate;

        // Assert
        Assert.Equal("2023-07-04", decisionDate);
    }


    [Fact]
    public void Line_DecisionDate_Property_WithInvalidDate_ThrowsException()
    {
        // Arrange
        var line = new Metadata.Line { court = "UKFTT-GRC", decision_datetime = "invalid-date" };

        // Act & Assert
        Assert.Throws<FormatException>(() => _ = line.DecisionDate);
    }

    [Fact]
    public void Line_FirstPartyName_WithClaimants_ReturnsClaimants()
    {
        // Arrange
        var line = new Metadata.Line { court = "UKFTT-GRC", claimants = "John Smith", respondent = "HMRC" };

        // Act
        var result = line.FirstPartyName;

        // Assert
        Assert.Equal("John Smith", result);
    }

    [Fact]
    public void Line_FirstPartyName_WithAppellants_ReturnsAppellants()
    {
        // Arrange
        var line = new Metadata.Line { court = "UKFTT-GRC", appellants = "Jane Doe", respondent = "HMRC" };

        // Act
        var result = line.FirstPartyName;

        // Assert
        Assert.Equal("Jane Doe", result);
    }

    [Fact]
    public void Line_FirstPartyRole_WithClaimants_ReturnsClaimant()
    {
        // Arrange
        var line = new Metadata.Line { court = "UKFTT-GRC", claimants = "John Smith", respondent = "HMRC" };

        // Act
        var result = line.FirstPartyRole;

        // Assert
        Assert.Equal(PartyRole.Claimant, result);
    }

    [Fact]
    public void Line_FirstPartyRole_WithAppellants_ReturnsAppellant()
    {
        // Arrange
        var line = new Metadata.Line { court = "UKFTT-GRC", appellants = "Jane Doe", respondent = "HMRC" };

        // Act
        var result = line.FirstPartyRole;

        // Assert
        Assert.Equal(PartyRole.Appellant, result);
    }
}
