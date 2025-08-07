#nullable enable
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw.Parse;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        // We may need to hold this information in the Frames, but that may be tricky. For now we store them
        // at the root.
        private readonly string? subType;
        private readonly string? procedure;
        private readonly DocName docName;
        /*
          This class takes a list of "pre-parsed" blocks, and arranges them into a bill structure.
          The pre-parsed list contains blocks of only four types:
           - WLine (a line of text, corresponding to a Word "paragraph") (won't have a number unless subclassed)
           - WOldNumberedParagraph (a subclass of WLine, with a number)
           = WTable
           - WTableOfContents
        */
        public static Document Parse(byte[] docx, LegislationClassifier classifier)
        {
            WordprocessingDocument doc = AkN.Parser.Read(docx);
            CaseLaw.WordDocument simple = new CaseLaw.PreParser().Parse(doc);
            return new LegislationParser(simple, classifier).Parse();
        }

        private LegislationParser(CaseLaw.WordDocument doc, LegislationClassifier classifier)
        {
            Document = doc;
            docName = classifier.DocName;
            frames = new Frames(classifier.DocName,  Context.BODY);
        }

        private readonly ILogger Logger = Logging.Factory.CreateLogger<LegislationParser>();
        private Frames frames;
        private readonly CaseLaw.WordDocument Document;
        private int i = 0;

        int parseDepth = 0;
        int parseDepthMax = 0;
        int parseAndMemoizeDepth = 0;
        int parseAndMemoizeDepthMax = 0;

        private Lawmaker.Document Parse()
        {
            ParseAndEnrichHeader();
            ParseBody();

            if (i != Document.Body.Count)
                Logger.LogWarning("parsing did not complete: {}", i);

            Logger.LogInformation($"Maximum ParseAndMemoize depth reached: {parseAndMemoizeDepthMax}");
            Logger.LogInformation($"Maximum Parse depth reached: {parseDepthMax}");

            // do this after parsing is complete, because it alters the contents of parsed results
            // which does not work well with memoization
            ExtractAllQuotesAndAppendTexts(body);
            QuotedTextEnricher quotedTextEnricher = new($"(?:{{.*?}})?{StartQuotePattern()}", EndQuotePattern());
            quotedTextEnricher.EnrichDivisions(body);

            var styles = DOCX.CSS.Extract(Document.Docx.MainDocumentPart, "#bill");

            return new Lawmaker.Document
            {
                Type = docName,
                Styles = styles,
                CoverPage = coverPage,
                Preface = preface,
                Preamble = preamble,
                Body = body,
                Schedules = []
            };
        }

    }

}
