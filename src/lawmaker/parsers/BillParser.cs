
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

        private readonly CaseLaw.WordDocument Document;
        private int i = 0;

        private NIPublicBill Parse()
        {

            ParseHeader();
            ParseBody();

            if (i != Document.Body.Count)
                Logger.LogWarning("parsing did not complete: {}", i);

            var styles = DOCX.CSS.Extract(Document.Docx.MainDocumentPart, "#bill");

            return new NIPublicBill {
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
