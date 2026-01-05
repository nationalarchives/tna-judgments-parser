#nullable enable

using System;
using System.ComponentModel.DataAnnotations;

using Backlog.Src;

using Xunit;

namespace test.backlog.MetadataTests;

public class TestCategoryValidationAttribute
{
    private readonly CategoryValidationAttribute categoryValidationAttribute = new();

    [Theory]
    [InlineData("Equal Pay Act", "Equal value", null, null, true, null)]
    [InlineData("Equal Pay Act", null, null, null, true, null)]
    [InlineData(null, "Equal value", null, null, false,
        "Id 125 - main_subcategory 'Equal value' cannot exist without main_category being defined")]
    [InlineData(null, null, null, null, true, null)]
    [InlineData(null, null, "Equal Pay Act", "Equal value", true, null)]
    [InlineData(null, null, "Equal Pay Act", null, true, null)]
    [InlineData(null, null, null, "Equal value", false,
        "Id 125 - sec_subcategory 'Equal value' cannot exist without sec_category being defined")]
    [InlineData("Equal Pay Act", "Equal value", "Practice and Procedure", "Costs", true, null)]
    [InlineData("Equal Pay Act", "Equal value", "Practice and Procedure", null, true, null)]
    [InlineData("Equal Pay Act", null, "Practice and Procedure", "Costs", true, null)]
    public void IsValid_WithLine_ValidatesThatClaimantsOrAppellantsArePresent(string? mainCategory,
        string? mainSubcategory, string? secondaryCategory, string? secondarySubcategory,
        bool expectedResult, string? expectedErrorMessage)
    {
        // Arrange
        var line = new Metadata.Line
        {
            id = "125",
            main_category = mainCategory,
            main_subcategory = mainSubcategory,
            sec_category = secondaryCategory,
            sec_subcategory = secondarySubcategory
        };

        // Act & Assert
        var result = categoryValidationAttribute.GetValidationResult(line, new ValidationContext(line));
        Assert.Equal(expectedResult, result == ValidationResult.Success);
        Assert.Equal(expectedErrorMessage, result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithOtherObject_throwsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            categoryValidationAttribute.IsValid($"Not a {nameof(Metadata.Line)}"));

        Assert.Equal($"CategoryValidationAttribute can only be used on a {nameof(Metadata.Line)}", exception.Message);
    }
}
