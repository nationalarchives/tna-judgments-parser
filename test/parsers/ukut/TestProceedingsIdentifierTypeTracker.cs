#nullable enable

using Moq;

using Shouldly;

using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

using Xunit;

namespace test.parsers.ukut;

public class TestProceedingsIdentifierTypeTracker : ParserTestBase
{
    private readonly Mock<IProceedingsIdentifierTypeLabelParser> labelParser = new();
    private readonly ProceedingsIdentifierTypeTracker tracker;

    public TestProceedingsIdentifierTypeTracker()
    {
        tracker = new ProceedingsIdentifierTypeTracker(labelParser.Object);
    }

    private void SetupLabelParserWith(string precedingText, string? label)
    {
        labelParser.Setup(p => p.ParseFromPrecedingText(precedingText))
                   .Returns(label);
    }

    [Fact]
    public void GetCurrentType_NothingRecorded_ReturnsNull()
    {
        tracker.GetCurrentType().ShouldBeNull();
    }

    [Fact]
    public void GetCurrentType_TextRecordedAcrossInlines_AsksLabelParserAboutAllOfIt()
    {
        SetupLabelParserWith("Appeal No. ", "Appeal No");

        tracker.Record(Text("Appeal "));
        tracker.Record(Text("No. "));

        tracker.GetCurrentType().ShouldBe("Appeal No");
    }

    [Fact]
    public void GetCurrentType_TabRecorded_PassesTabCharacterToLabelParser()
    {
        SetupLabelParserWith("Appeal No:\t", "Appeal No");

        tracker.Record(Text("Appeal No:"));
        tracker.Record(Tab());

        tracker.GetCurrentType().ShouldBe("Appeal No");
    }

    [Fact]
    public void GetCurrentType_InlineIsNeitherTextNorTab_IsIgnored()
    {
        SetupLabelParserWith("Appeal No: ", "Appeal No");

        tracker.Record(Text("Appeal No: "));
        tracker.Record(new WLineBreak());

        tracker.GetCurrentType().ShouldBe("Appeal No");
    }

    [Fact]
    public void GetCurrentType_CalledTwice_SecondCallOnlySeesTextRecordedSinceFirst()
    {
        SetupLabelParserWith("Appeal No: ", "Appeal No");
        SetupLabelParserWith(", ", null);

        tracker.Record(Text("Appeal No: "));
        tracker.GetCurrentType();
        tracker.Record(Text(", "));
        tracker.GetCurrentType();

        labelParser.Verify(p => p.ParseFromPrecedingText(", "), Times.Once);
    }

    [Theory]
    [InlineData(", ", null, "Appeal reference")]
    [InlineData("Case Number: ", "Case Number", "Case Number")]
    public void GetCurrentType_AfterEarlierLabel_ReturnsLatestLabelFound(string laterText, string? laterLabel,
        string expectedType)
    {
        SetupLabelParserWith("Appeal references: ", "Appeal reference");
        SetupLabelParserWith(laterText, laterLabel);
        tracker.Record(Text("Appeal references: "));
        tracker.GetCurrentType();

        tracker.Record(Text(laterText));

        tracker.GetCurrentType().ShouldBe(expectedType);
    }

    [Fact]
    public void StartLine_AfterTextRecorded_DiscardsThatText()
    {
        SetupLabelParserWith("Heard on 3 May 2022.", "Heard on 3 May 2022");
        tracker.Record(Text("Heard on 3 May 2022."));

        tracker.StartLine();

        tracker.GetCurrentType().ShouldBeNull();
        labelParser.Verify(p => p.ParseFromPrecedingText("Heard on 3 May 2022."), Times.Never);
    }

    [Fact]
    public void Reset_AfterTextRecorded_DiscardsThatText()
    {
        SetupLabelParserWith("Heard on 3 May 2022.", "Heard on 3 May 2022");
        tracker.Record(Text("Heard on 3 May 2022."));

        tracker.Reset();

        tracker.GetCurrentType().ShouldBeNull();
        labelParser.Verify(p => p.ParseFromPrecedingText("Heard on 3 May 2022."), Times.Never);
    }

    [Fact]
    public void StartLine_AfterLabelFound_KeepsMostRecentLabel()
    {
        SetupLabelParserWith("Appeal No: ", "Appeal No");
        tracker.Record(Text("Appeal No: "));
        tracker.GetCurrentType();

        tracker.StartLine();

        tracker.GetCurrentType().ShouldBe("Appeal No");
    }

    [Fact]
    public void Reset_AfterLabelFound_ForgetsMostRecentLabel()
    {
        SetupLabelParserWith("Appeal No: ", "Appeal No");
        tracker.Record(Text("Appeal No: "));
        tracker.GetCurrentType();

        tracker.Reset();

        tracker.GetCurrentType().ShouldBeNull();
    }
}
