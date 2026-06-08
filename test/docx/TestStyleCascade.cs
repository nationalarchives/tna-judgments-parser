using System.Collections.Generic;

using Xunit;

using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.DOCX.Test {

/// <summary>
/// Direct tests of <see cref="WText.MergeParagraphStyleOntoCharacterStyle"/>
/// — the run-style/paragraph-style cascade resolver.
/// <para>
/// Documents the deliberate split decided in commit 24f4d98c: colour
/// properties follow the correct CSS/Word cascade (character style wins),
/// every other property keeps the legacy paragraph-style-wins behaviour.
/// </para>
/// </summary>
public class TestStyleCascade {

    private static Dictionary<string, string> Merge(
            Dictionary<string, string> adHoc,
            Dictionary<string, string> paragraph,
            Dictionary<string, string> character) {
        WText.MergeParagraphStyleOntoCharacterStyle(adHoc, paragraph, character);
        return adHoc;
    }

    [Theory]
    [InlineData("color")]
    [InlineData("background-color")]
    public void ColourPropertiesTakeTheCharacterStyleValue(string property) {
        var result = Merge(
            adHoc: new(),
            paragraph: new() { [property] = "#ffffff" },
            character: new() { [property] = "#000000" });
        Assert.Equal("#000000", result[property]);
    }

    [Theory]
    [InlineData("font-weight", "bold", "normal")]
    [InlineData("font-style", "italic", "normal")]
    [InlineData("text-decoration", "underline", "none")]
    public void NonColourPropertiesKeepTheParagraphStyleValue(string property, string paragraphValue, string characterValue) {
        var result = Merge(
            adHoc: new(),
            paragraph: new() { [property] = paragraphValue },
            character: new() { [property] = characterValue });
        Assert.Equal(paragraphValue, result[property]);
    }

    [Fact]
    public void AdHocInlineFormattingIsNeverOverridden() {
        var result = Merge(
            adHoc: new() { ["color"] = "#ff0000" },
            paragraph: new() { ["color"] = "#ffffff" },
            character: new() { ["color"] = "#000000" });
        Assert.Equal("#ff0000", result["color"]);
    }

    [Fact]
    public void PropertyAbsentFromParagraphStyleIsNotAdded() {
        var result = Merge(
            adHoc: new(),
            paragraph: new(),
            character: new() { ["color"] = "#000000" });
        Assert.False(result.ContainsKey("color"));
    }

    [Fact]
    public void AgreeingValuesAreANoOp() {
        var result = Merge(
            adHoc: new(),
            paragraph: new() { ["color"] = "#000000" },
            character: new() { ["color"] = "#000000" });
        Assert.False(result.ContainsKey("color"));
    }

}

}
