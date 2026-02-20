#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

using Backlog.Csv;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestAppellantsOrClaimantsPresentValidationAttribute
{
    private readonly AppellantsOrClaimantsPresentValidationAttribute appellantsOrClaimantsPresentValidationAttribute =
        new();

    [Theory]
    [InlineData(null, "Jane Doe", true, null)]
    [InlineData("John Smith", null, true, null)]
    [InlineData("John Smith", "Jane Doe", false,
        "Id 125 - Cannot have both claimants and appellants. Please provide only one.")]
    [InlineData(null, null, false, "Id 125 - Must have either claimants or appellants. At least one is required.")]
    public void IsValid_WithLine_ValidatesThatClaimantsOrAppellantsArePresent(string? claimants, string? appellants,
        bool expectedResult, string? expectedErrorMessage)
    {
        // Arrange
        var line = CsvMetadataLineHelper.DummyLine with
        {
            id = "125",
            claimants = claimants,
            appellants = appellants
        };

        // Act & Assert
        var result = appellantsOrClaimantsPresentValidationAttribute.GetValidationResult(line, new ValidationContext(line));
        Assert.Equal(expectedResult, result == ValidationResult.Success);
        Assert.Equal(expectedErrorMessage, result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithOtherObject_throwsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            appellantsOrClaimantsPresentValidationAttribute.IsValid($"Not a {nameof(CsvLine)}"));
        
        Assert.Equal($"AppellantsOrClaimantsPresentValidationAttribute can only be used on a {nameof(CsvLine)}",
            exception.Message);
    }
}
