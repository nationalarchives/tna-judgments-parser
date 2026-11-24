#nullable enable

using Shouldly;

using UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

using Xunit;

namespace test.parsers.ukut;

public class TestProceedingsIdentifierTypeLabelParser
{
    private readonly ProceedingsIdentifierTypeLabelParser parser = new();

    [Theory]
    [InlineData("Appeal No ", "Appeal No")]
    [InlineData("Appeal No. ", "Appeal No")]
    [InlineData("Appeal No.: ", "Appeal No")]
    [InlineData("Appeal No - ", "Appeal No")]
    [InlineData("Appeal No – ", "Appeal No")]
    [InlineData("Appeal No:- ", "Appeal No")]
    [InlineData("Appeal No:: ", "Appeal No")]
    [InlineData("Appeal No:. ", "Appeal No")]
    [InlineData("Appeal No.s: ", "Appeal No")]
    [InlineData("Appeal Nos.: ", "Appeal No")]
    [InlineData("Appeal No's: ", "Appeal No")]
    [InlineData("Appeal No’s: ", "Appeal No")]
    [InlineData("Appeal  No: ", "Appeal No")]
    [InlineData("Appeal  No ", "Appeal No")]
    [InlineData("Appeal number: ", "Appeal number")]
    [InlineData("Appeal reference: ", "Appeal reference")]
    [InlineData("Appeal references: ", "Appeal reference")]
    [InlineData("Case Reference: ", "Case Reference")]
    [InlineData("UTLC Case Numbers: ", "UTLC Case Number")]
    [InlineData("UTLC  Case Numbers: ", "UTLC Case Number")]
    [InlineData("Case Number:", "Case Number")]
    [InlineData("Case  Number: ", "Case Number")]
    [InlineData("Case Num:", "Case Num")]
    [InlineData("Case Ref/No:", "Case Ref/No")]
    [InlineData("Case Ref No.", "Case Ref No")]
    [InlineData("Tribunal ref:", "Tribunal ref")]
    [InlineData("  Tribunal ref:", "Tribunal ref")]
    [InlineData("\tTribunal ref: ", "Tribunal ref")]
    [InlineData("Tribunal ref   :   ", "Tribunal ref")]
    public void ParseFromPrecedingText_TextEndingInLabel_ReturnsSingularLabel(string textBeforeIdentifier,
        string expectedLabel)
    {
        var label = parser.ParseFromPrecedingText(textBeforeIdentifier);

        label.ShouldBe(expectedLabel);
    }

    [Theory]
    [InlineData("Neutral citation number: [2026] UKUT 00123 (LC)                               UTLC No: ", "UTLC No")]
    [InlineData("(Immigration and Asylum Chamber) Appeal Number: ", "Appeal Number")]
    [InlineData("IN THE UPPER TRIBUNAL    Appeal No’s. ", "Appeal No")]
    [InlineData("IN THE UPPER TRIBUNAL\tAppeal No.  ", "Appeal No")]
    [InlineData(", Case Number: ", "Case Number")]
    [InlineData("; Case Number: ", "Case Number")]
    [InlineData(", Tribunal ref: ", "Tribunal ref")]
    [InlineData("; Tribunal ref: ", "Tribunal ref")]
    [InlineData("Some other text\t\t\tTribunal ref: ", "Tribunal ref")]
    public void ParseFromPrecedingText_EarlierTextOnSameLine_ExcludesEarlierTextFromLabel(string textBeforeIdentifier,
        string expectedLabel)
    {
        var label = parser.ParseFromPrecedingText(textBeforeIdentifier);

        label.ShouldBe(expectedLabel);
    }

    [Fact]
    public void ParseFromPrecedingText_TabBetweenLabelAndIdentifier_ReturnsLabel()
    {
        var label = parser.ParseFromPrecedingText("Appeal No:\t");

        label.ShouldBe("Appeal No");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(", ")]
    [InlineData(" & ")]
    [InlineData(" and ")]
    [InlineData(" AND ")]
    [InlineData(" or ")]
    [InlineData(" and/or ")]
    [InlineData("Neutral Citation: [2026] UKUT 00123 (TCC) ")]
    public void ParseFromPrecedingText_TextNotEndingInLabel_ReturnsNull(string textBeforeIdentifier)
    {
        var label = parser.ParseFromPrecedingText(textBeforeIdentifier);

        label.ShouldBeNull();
    }
}
