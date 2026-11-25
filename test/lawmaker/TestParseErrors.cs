#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker.Api;

using Xunit;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Failures outside block parsing used to escape Helper.Parse, which the Word Import Lambda
/// reported only as "error parsing document" (LCO-5192).
/// </summary>
public class TestParseErrors
{

    private static readonly LegislationClassifier Classifier = new(DocName.UKSI, "order", "made");
    private static readonly LanguageService Languages = new(["en"]);

    [Fact]
    public void Parse_UnreadableDocument_ReturnsErrorNamingTheStage()
    {
        Response response = Helper.Parse([1, 2, 3], Classifier, Languages);

        response.Error.ShouldNotBeNull();
        response.Error.Stage.ShouldBe(ParseStageException.ReadingDocument);
        response.Error.BlockNumber.ShouldBeNull();
        response.Error.Describe().ShouldStartWith("Parsing failed while reading the document (");
    }

    [Fact]
    public void Parse_TableStartingWithMergedContinuationCell_ReturnsErrorWithTableText()
    {
        var docx = CreateDocx(body => body.AppendChild(new Table(
            new TableGrid(new GridColumn { Width = "2000" }),
            new TableRow(new TableCell(
                new TableCellProperties(new W.VerticalMerge()),
                new Paragraph(new Run(new Text("Development area node point"))))))));

        Response response = Helper.Parse(docx, Classifier, Languages);

        response.Error.ShouldNotBeNull();
        response.Error.Stage.ShouldBe(ParseStageException.BuildingTable);
        response.Error.BlockText.ShouldBe("Development area node point");
        response.Error.Message.ShouldStartWith("ArgumentOutOfRangeException: ");
    }

    [Fact]
    public void ExtractQuotes_FailingQuotedStructure_ReportsItsFirstLineAndBlock()
    {
        // A branch with no children fails when the end quote is extracted, because
        // LastLine.Replace reads its null Children, so it stands in for any failure in this stage
        WLine line = CreateLine("“The undertaker must maintain the bank.");
        var quotedStructure = new BlockQuotedStructure { Contents = [new UnnumberedBranch { Intro = [line] }] };
        List<IDivision> body = [new UnnumberedLeaf { Contents = [quotedStructure] }];

        var ex = Should.Throw<ParseStageException>(() =>
            LegislationParser.ExtractAllQuotesAndAppendTexts(body, located => located == line ? 42 : null));

        ex.Stage.ShouldBe(ParseStageException.ExtractingQuotes);
        ex.BlockNumber.ShouldBe(42);
        ex.BlockText.ShouldBe("The undertaker must maintain the bank.");
    }

    // Six of the LCO-5192 sample documents failed here, because a paragraph had been parsed
    // as a quoted structure even though it does not start with a quote mark
    [Fact]
    public void ExtractQuotes_QuotedStructureWithoutStartQuote_DoesNotFail()
    {
        WLine line = CreateLine("Provided that the undertaker is not required to maintain the bank.");
        var quotedStructure = new BlockQuotedStructure { Contents = [new UnnumberedLeaf { Contents = [line] }] };
        List<IDivision> body = [new UnnumberedLeaf { Contents = [quotedStructure] }];

        LegislationParser.ExtractAllQuotesAndAppendTexts(body);

        quotedStructure.StartQuote.ShouldBeNull();
        FirstText(quotedStructure).ShouldBe("Provided that the undertaker is not required to maintain the bank.");
    }

    [Fact]
    public void ExtractQuotes_HeadingFirstQuotedStructureWithoutStartQuote_DoesNotFail()
    {
        WLine heading = CreateLine("On Street Apparatus");
        WLine line = CreateLine("The undertaker must not remove apparatus.");
        var quotedStructure = new BlockQuotedStructure
        {
            Contents = [new UnnumberedLeaf { HeadingPrecedesNumber = true, Heading = heading, Contents = [line] }]
        };
        List<IDivision> body = [new UnnumberedLeaf { Contents = [quotedStructure] }];

        LegislationParser.ExtractAllQuotesAndAppendTexts(body);

        quotedStructure.StartQuote.ShouldBeNull();
        heading.TextContent.ShouldBe("On Street Apparatus");
    }

    [Fact]
    public void ExtractQuotes_QuotedStructureWithStartQuote_StillExtractsIt()
    {
        WLine line = CreateLine("“The undertaker must maintain the bank.”");
        var quotedStructure = new BlockQuotedStructure { Contents = [new UnnumberedLeaf { Contents = [line] }] };
        List<IDivision> body = [new UnnumberedLeaf { Contents = [quotedStructure] }];

        LegislationParser.ExtractAllQuotesAndAppendTexts(body);

        quotedStructure.StartQuote.ShouldBe("“");
        quotedStructure.EndQuote.ShouldBe("”");
        FirstText(quotedStructure).ShouldBe("The undertaker must maintain the bank.");
    }

    private static string? FirstText(BlockQuotedStructure quotedStructure) =>
        LegislationParser.FirstLine(quotedStructure)?.TextContent;

    [Fact]
    public void Describe_WithBlock_KeepsTheFormatLawmakerReads()
    {
        var error = new ParseError
        {
            Stage = ParseStageException.ExtractingQuotes,
            BlockNumber = 1424,
            BlockText = "Provided that",
            Message = "InvalidOperationException: Sequence contains no matching element"
        };

        error.Describe().ShouldBe("Parsing failed at block 1424 while extracting quotes (InvalidOperationException: Sequence contains no matching element). \nBlock text: Provided that");
    }

    [Fact]
    public void Describe_WithoutBlockOrText_OmitsThem()
    {
        var error = new ParseError { Stage = ParseStageException.SerializingXml, Message = "XmlException: bad" };

        error.Describe().ShouldBe("Parsing failed while serializing the XML (XmlException: bad).");
    }

    // The same patterns as react-ldapp's DocxImportErrorUtils.ts, which fills in the
    // Parser position and Content snippet in the error details modal. Lawmaker currently
    // removes the line break entirely, which is the empty string case.
    [Theory]
    [InlineData("\n")]
    [InlineData("")]
    public void Describe_StillMatchesLawmakerPatterns_WithOrWithoutTheLineBreak(string lineBreak)
    {
        var error = new ParseError
        {
            Stage = ParseStageException.ExtractingQuotes,
            BlockNumber = 1424,
            BlockText = "Provided that",
            Message = "InvalidOperationException: Sequence contains no matching element"
        };
        var message = "Failed to import: Word Import tool failed: " + error.Describe().Replace("\n", lineBreak);

        var block = Regex.Match(message, @"at block\s+(\d+)", RegexOptions.IgnoreCase);
        var snippet = Regex.Match(message, @"block text\s*:\s*([\s\S]*)$", RegexOptions.IgnoreCase);

        block.Groups[1].Value.ShouldBe("1424");
        snippet.Groups[1].Value.Trim().ShouldBe("Provided that");
        message.ShouldContain("element). ");
        message.ShouldNotContain("element).Block");
    }

    private static byte[] CreateDocx(System.Action<Body> build)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            MainDocumentPart main = doc.AddMainDocumentPart();
            var body = new Body();
            build(body);
            body.AppendChild(new SectionProperties());
            main.Document = new W.Document(body);
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(new DocDefaults(new RunPropertiesDefault(
                new RunPropertiesBaseStyle(new RunFonts { Ascii = "Times New Roman" }, new FontSize { Val = "24" }))));
        }
        return stream.ToArray();
    }

    private static WLine CreateLine(string text)
    {
        using var stream = new MemoryStream();
        using var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        MainDocumentPart main = doc.AddMainDocumentPart();
        main.Document = new W.Document(new Body());
        return new WLine(main, new Paragraph(new Run(new Text(text))));
    }

}
