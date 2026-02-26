#nullable enable

using System;

using UK.Gov.Legislation.Judgments;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestCsvLine
{

    [Fact]
    public void FirstPartyName_WithClaimants_ReturnsClaimants()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { claimants = "John Smith", respondent = "HMRC" };

        // Act
        var result = csvLine.FirstPartyName;

        // Assert
        Assert.Equal("John Smith", result);
    }

    [Fact]
    public void FirstPartyName_WithAppellants_ReturnsAppellants()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { appellants = "Jane Doe", respondent = "HMRC" };

        // Act
        var result = csvLine.FirstPartyName;

        // Assert
        Assert.Equal("Jane Doe", result);
    }

    [Fact]
    public void FirstPartyRole_WithClaimants_ReturnsClaimant()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { claimants = "John Smith", respondent = "HMRC" };

        // Act
        var result = csvLine.FirstPartyRole;

        // Assert
        Assert.Equal(PartyRole.Claimant, result);
    }

    [Fact]
    public void FirstPartyRole_WithAppellants_ReturnsAppellant()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { appellants = "Jane Doe", respondent = "HMRC" };

        // Act
        var result = csvLine.FirstPartyRole;

        // Assert
        Assert.Equal(PartyRole.Appellant, result);
    }
    
    [Fact]
    public void Parties_WithClaimantsAndRespondent_ReturnsClaimantThenRespondent()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { claimants = "Alice", appellants = null, respondent = "HMRC" };

        // Act
        var parties = csvLine.Parties;

        // Assert
        Assert.Equal(2, parties.Length);
        Assert.Equal("Alice", parties[0].Name);
        Assert.Equal(PartyRole.Claimant, parties[0].Role);
        Assert.Equal("HMRC", parties[1].Name);
        Assert.Equal(PartyRole.Respondent, parties[1].Role);
    }

    [Fact]
    public void Parties_WithAppellantsAndRespondent_ReturnsAppellantThenRespondent()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { appellants = "Bob", claimants = null, respondent = "HMRC" };

        // Act
        var parties = csvLine.Parties;

        // Assert
        Assert.Equal(2, parties.Length);
        Assert.Equal("Bob", parties[0].Name);
        Assert.Equal(PartyRole.Appellant, parties[0].Role);
        Assert.Equal("HMRC", parties[1].Name);
        Assert.Equal(PartyRole.Respondent, parties[1].Role);
    }

    [Fact]
    public void Parties_WithBothClaimantsAndAppellants_Throws()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { claimants = "Alice", appellants = "Bob", respondent = "HMRC" };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => { _ = csvLine.Parties; });
    }

    // Categories tests

    [Fact]
    public void Categories_WithNoCategories_ReturnsEmpty()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine;

        // Act
        var cats = csvLine.Categories;

        // Assert
        Assert.Empty(cats);
    }

    [Fact]
    public void Categories_WithMainOnly_ReturnsMain()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { main_category = "Equal Pay Act" };

        // Act
        var categories = csvLine.Categories;

        // Assert
        Assert.Single(categories);
        Assert.Equal("Equal Pay Act", categories[0].Name);
    }

    [Fact]
    public void Categories_WithMainAndSub_ReturnsMainThenSub_WithParentOnSub()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with
        {
            main_category = "Equal Pay Act",
            main_subcategory = "Equal value"
        };

        // Act
        var categories = csvLine.Categories;

        // Assert
        Assert.Equal(2, categories.Length);
        Assert.Equal("Equal Pay Act", categories[0].Name);
        Assert.Equal("Equal value", categories[1].Name);
        Assert.Equal("Equal Pay Act", categories[1].Parent);
    }

    [Fact]
    public void Categories_WithSecondaryOnly_ReturnsSecondary()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with { sec_category = "Practice and Procedure" };

        // Act
        var categories = csvLine.Categories;

        // Assert
        Assert.Single(categories);
        Assert.Equal("Practice and Procedure", categories[0].Name);
    }

    [Fact]
    public void Categories_WithSecondaryAndSub_ReturnsSecondaryThenSub_WithParentOnSub()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with
        {
            sec_category = "Practice and Procedure",
            sec_subcategory = "Costs"
        };

        // Act
        var categories = csvLine.Categories;

        // Assert
        Assert.Equal(2, categories.Length);
        Assert.Equal("Practice and Procedure", categories[0].Name);
        Assert.Equal("Costs", categories[1].Name);
        Assert.Equal("Practice and Procedure", categories[1].Parent);
    }

    [Fact]
    public void Categories_WithBothSets_ReturnsAllInOrder()
    {
        // Arrange
        var csvLine = CsvMetadataLineHelper.DummyLine with
        {
            main_category = "Equal Pay Act",
            main_subcategory = "Equal value",
            sec_category = "Practice and Procedure",
            sec_subcategory = "Costs"
        };

        // Act
        var categories = csvLine.Categories;

        // Assert
        Assert.Equal(4, categories.Length);
        Assert.Equal("Equal Pay Act", categories[0].Name);
        Assert.Equal("Equal value", categories[1].Name);
        Assert.Equal("Equal Pay Act", categories[1].Parent);
        Assert.Equal("Practice and Procedure", categories[2].Name);
        Assert.Equal("Costs", categories[3].Name);
        Assert.Equal("Practice and Procedure", categories[3].Parent);
    }
}
