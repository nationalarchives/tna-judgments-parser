#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Lawmaker.Headers;

using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw.Parse;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;


namespace UK.Gov.Legislation.Lawmaker;

using DocumentStyle = Dictionary<string, Dictionary<string, string>>;
public partial class LegislationParser
{

    // We may need to hold this information in the Frames, but that may be tricky. For now we store them
    // at the root.
    private readonly DocName docName;

    /*
        This class takes a list of "pre-parsed" blocks, and arranges them into a bill structure.
        The pre-parsed list contains blocks of only four types:
        - WLine (a line of text, corresponding to a Word "paragraph") (won't have a number unless subclassed)
        - WOldNumberedParagraph (a subclass of WLine, with a number)
        = WTable
        - WTableOfContents
    */
    public static Document Parse(byte[] docx, LegislationClassifier classifier, LanguageService languageService)
    {
        WordprocessingDocument doc = AkN.Parser.Read(docx);
        CaseLaw.WordDocument simple = new CaseLaw.PreParser().Parse(doc);
        WImage.Get(doc);
        return new LegislationParser(simple, classifier, languageService) { LanguageService = languageService }.Parse();
    }

    public static Document Parse(WordprocessingDocument doc, LegislationClassifier classifier, LanguageService languageService)
    {
        CaseLaw.WordDocument simple = ParseStageException.Wrap(ParseStageException.ReadingDocument, () => new CaseLaw.PreParser().Parse(doc));
        ParseStageException.Wrap(ParseStageException.ExtractingImages, () => WImage.Get(doc));
        LegislationParser parser = ParseStageException.Wrap(ParseStageException.ReadingDocument, () => new LegislationParser(simple, classifier, languageService) { LanguageService = languageService });
        return parser.Parse();
    }

    private LegislationParser(CaseLaw.WordDocument doc, LegislationClassifier classifier, LanguageService languageService) : this(
            doc.Body.Select(b => b.Block).ToList(),
            DOCX.CSS.Extract(doc.Docx.MainDocumentPart, "#bill"),
            classifier,
            languageService
        )
    { }

    private LegislationParser(IEnumerable<IBlock> contents, DocumentStyle? style, LegislationClassifier classifier, LanguageService languageService) : base(contents)
    {
        // We can safely discard the `BlockWithBreak` added boolean here, we don't need it
        Styles = style;
        docName = classifier.DocName;
        frames = new Frames(classifier.DocName, classifier.GetContext());
        provisionRecords = new ProvisionRecords();
        LanguageService = languageService;
    }

    private readonly ILogger Logger = Logging.Factory.CreateLogger<LegislationParser>();
    private readonly ProvisionRecords provisionRecords;
    private readonly Frames frames;
    private Dictionary<string, Dictionary<string, string>>? Styles { get; init; }


    int parseDepth = 0;
    int parseDepthMax = 0;
    int parseAndMemoizeDepth = 0;
    int parseAndMemoizeDepthMax = 0;

    private Lawmaker.Document Parse()
    {
        try
        {
            Logger.LogDebug($"Begin parse");
            Logger.LogDebug($"Parsing header...");
            ParseAndEnrichHeader();
            Logger.LogDebug($"Parsing body...");
            ParseBody();
            Logger.LogDebug($"Parsing conclusions...");
            ParseConclusions();
        }
        catch (Exception ex)
        {
            var blockText = (i >= 0 && i < Body.Count) ? Body[i]?.ToString() : null;
            throw new BlockParsingException(i, blockText, ex);
        }

        if (i != Body.Count)
            Logger.LogWarning("parsing did not complete: {}", i);

        Logger.LogInformation($"Maximum ParseAndMemoize depth reached: {parseAndMemoizeDepthMax}");
        Logger.LogInformation($"Maximum Parse depth reached: {parseDepthMax}");

        // Handle start and end quotes after parsing is complete, because it alters the
        // contents of parsed results which does not work well with memoization
        ParseStageException.Wrap(ParseStageException.ExtractingQuotes, () => ExtractAllQuotesAndAppendTexts(body, LocateBlock));
        QuotationEnricher quotationEnricher = new(LanguageService, $"(?:{{.*?}})?{StartQuotePattern()}", EndQuotePattern());
        ParseStageException.Wrap(ParseStageException.EnrichingQuotations, () => quotationEnricher.EnrichDivisions(body));

        FootnoteEnricher footnoteEnricher = new();
        FootnoteHeaderVisitor footnoteHeaderVisitor = new()
        {
            FootnoteEnricher = footnoteEnricher,
        };

        ParseStageException.Wrap(ParseStageException.EnrichingFootnotes, () =>
        {
            header = header?.Visit(footnoteHeaderVisitor, new HeaderVisitorContext(docName));
            footnoteEnricher.EnrichDivisions(body);
        });


        return new Lawmaker.Document
        {
            Type = docName,
            Styles = Styles,
            Metadata = new(),
            Header = header,
            Body = body,
            Schedules = [],
            Conclusions = conclusions
        };
    }

    // Finds the position in the document of a line from the parsed structure, for error reporting.
    // Lines are usually the same objects as in Body, but some are rebuilt during parsing with their
    // quote marks removed, so fall back to comparing the text without quotes or whitespace.
    private int? LocateBlock(WLine line)
    {
        var index = Body.FindIndex(block => ReferenceEquals(block, line)
            || (block is ILineable lineable && ReferenceEquals(lineable.Lines.FirstOrDefault(), line)));
        if (index < 0)
        {
            var text = WithoutQuotesOrWhitespace(line.NormalizedContent);
            if (!string.IsNullOrEmpty(text))
                index = Body.FindIndex(block => block is WLine candidate && WithoutQuotesOrWhitespace(candidate.NormalizedContent) == text);
            if (index < 0 && !string.IsNullOrEmpty(text))
                index = Body.FindIndex(block => block is WLine candidate && WithoutQuotesOrWhitespace(candidate.NormalizedContent).Contains(text));
        }
        return index < 0 ? null : index;
    }

    private static string WithoutQuotesOrWhitespace(string? text) =>
        text is null ? "" : Regex.Replace(text, "[\\s\"'‘’“”]", "");

}
