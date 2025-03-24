
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using CaseLaw = UK.Gov.NationalArchives.CaseLaw.Parse;
using DOCX = UK.Gov.Legislation.Judgments.DOCX;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        /*
          This class takes a list of "pre-parsed" blocks, and arranges them into a bill structure.
          The pre-parsed list contains blocks of only four types:
           - WLine (a line of text, corresponding to a Word "paragraph") (won't have a number unless subclassed)
           - WOldNumberedParagraph (a subclass of WLine, with a number)
           = WTable
           - WTableOfContents
        */
        public static Bill Parse(byte[] docx)
        {
            WordprocessingDocument doc = AkN.Parser.Read(docx);
            CaseLaw.WordDocument simple = new CaseLaw.PreParser().Parse(doc);
            return new BillParser(simple).Parse();
        }

        private BillParser(CaseLaw.WordDocument doc)
        {
            Document = doc;
        }

        private readonly ILogger Logger = Logging.Factory.CreateLogger<BillParser>();
        private Frames frames = new Frames(DocName.NIA, Context.BODY);
        private readonly CaseLaw.WordDocument Document;
        private int i = 0;

        private NIPublicBill Parse()
        {
            ParseAndEnrichHeader();
            ParseBody();

            if (i != Document.Body.Count)
                Logger.LogWarning("parsing did not complete: {}", i);

            // Handle start and end quotes after parsing is complete, because it alters the
            // contents of parsed results which does not work well with memoization
            ExtractAllQuotesAndAppendTexts(body);
            QuotedTextEnricher quotedTextEnricher = new($"(?:{{.*?}})?{StartQuotePattern()}", EndQuotePattern());
            quotedTextEnricher.EnrichDivisions(body);

            var styles = DOCX.CSS.Extract(Document.Docx.MainDocumentPart, "#bill");

            return new NIPublicBill
            {
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
