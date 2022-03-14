
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT {

class Parser : AbstractParser {

    private Parser(WordprocessingDocument doc, IOutsideMetadata meta, IEnumerable<Tuple<WordprocessingDocument,AttachmentType>> attachments) : base(doc, meta, attachments) { }

    public static Judgment Parse(WordprocessingDocument doc, IOutsideMetadata meta, IEnumerable<Tuple<WordprocessingDocument,AttachmentType>> attachments) {
        return new Parser(doc, meta, attachments).Parse(JudgmentType.Decision);
    }

    private static ILogger logger = Logging.Factory.CreateLogger<CourtOfAppealParser>();

    string[] titles = new string[] {
        "DECISION",
        "DECISION AND REASONS",
        "DECISION ON APPLICATION FOR PERMISSION TO APPEAL",
        "DECISION ON APPLICATION TO DISCHARGE ANONYMITY ORDER",
        "DECISION ON PRELIMINARY ISSUE",
        "DECISION ON ERROR OF LAW",
        "DECISION AND REASONS ON ERROR OF LAW",
        "DECISION AND REMITTAL",
        "DECISION AND DIRECTIONS",

        // "Decision: the application for judicial review is refused",

        "DETERMINATION AND REASONS",

        // "FINDINGS OF THE UPPER TRIBUNAL EXERCISING ITS HAMID JURISDICTION",

        // "REASONS FOR DECISION",

        "JUDGMENT",
        "APPROVED JUDGMENT"
    };

    protected bool IsTitleParagraph(OpenXmlElement e) {
        string text = Regex.Replace(e.InnerText, @"\s+", " ").Trim();
        foreach (string title in titles)
            if (text.Equals(title, StringComparison.InvariantCultureIgnoreCase))
                return true;
        return false;
    }

    protected bool IsDashedOrSolidLine(OpenXmlElement e) {
        if (Regex.IsMatch(e.InnerText, @"^ ?-( -)+ ?$"))
            return true;
        if (Regex.IsMatch(e.InnerText, @"^_+$"))
            return true;
        return false;
    }

    protected override List<IBlock> Header() {
        int save = i;
        List<IBlock> header = Header1();
        if (header is not null)
            return header;
        i = save;
        header = Header2();
        if (header is not null)
            return header;
        i = save;
        header = Header3();
        if (header is not null)
            return header;
        i = save;
        header = Header4();
        if (header is not null)
            return header;
        return null;
    }
    private List<IBlock> Header1() {
        List<IBlock> header = new List<IBlock>();
        while (i < elements.Count) {
            logger.LogTrace("parsing element " + i);
            OpenXmlElement e = elements.ElementAt(i);
            AddBlock(e, header);
            if (IsTitleParagraph(e))
                return header;
            if (e is Table table) {
                var last = table.Descendants<Paragraph>().Where(p => !string.IsNullOrWhiteSpace(p.InnerText)).LastOrDefault();
                if (last is not null && IsTitleParagraph(last))
                    return header;
            }
        }
        return null;
    }
    private List<IBlock> Header2() {
        List<IBlock> header = new List<IBlock>();
        while (i < elements.Count) {
            logger.LogTrace("parsing element " + i);
            OpenXmlElement e = elements.ElementAt(i);
            AddBlock(e, header);
            if (Regex.Replace(e.InnerText, @"\s+", " ").Trim() == "J U D G M E N T") {
                if (i < elements.Count) {
                    OpenXmlElement next = elements.ElementAt(i);
                    if (IsSkippable(next) && i < elements.Count) {
                        i += 1;
                        next = elements.ElementAt(i);
                    }
                    if (next is Paragraph && IsDashedOrSolidLine(next)) {
                        AddBlock(next, header);
                        return header;
                    }
                }
            }
        }
        return null;
    }
    private List<IBlock> Header3() {
        List<IBlock> header = new List<IBlock>();
        while (i < elements.Count) {
            logger.LogTrace("parsing element " + i);
            OpenXmlElement e = elements.ElementAt(i);
            if (e.InnerText == "Introduction" || e.InnerText == "INTRODUCTION")
                return header;
            AddBlock(e, header);
        }
        return null;
    }
    private List<IBlock> Header4() {
        List<IBlock> header = new List<IBlock>();
        while (i < elements.Count) {
            logger.LogTrace("parsing element " + i);
            OpenXmlElement e = elements.ElementAt(i);
            AddBlock(e, header);
            if (Util.IsSectionBreak(e))
                return header;
        }
        return null;
    }

    private List<Enricher> headerEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger(),
        new UKUT.CourtType(),
        new UKUT.Citation(),
        new UKUT.CourtType2(),
        new UKUT.Date1(),
        new PartyEnricher()
    };

    protected override IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        return Enricher.Enrich(header, headerEnrichers);
    }

    protected override List<IDecision> Body() {
        List<IDivision> contents = Divisions();
        if (contents is null || contents.Count == 0)
            return null;
        Decision decision = new Decision { Author = null, Contents = contents };
        return new List<IDecision>(1) { decision };
    }

    protected override IEnumerable<IDecision> EnrichBody(IEnumerable<IDecision> body) {
        IEnumerable<IDecision> enriched = base.EnrichBody(body);
        return Enricher.Enrich(enriched, new List<Enricher>(1) { new UKUT.Date2() });
    }

    protected override IEnumerable<IBlock> EnrichConclusions(IEnumerable<IBlock> conclusions) {
        IEnumerable<IBlock> enriched = base.EnrichConclusions(conclusions);
        return Enricher.Enrich(enriched, new List<Enricher>(1) { new UKUT.Date2() });
    }


    override protected List<IDivision> ParagraphsUntilAnnex() {
        OpenXmlElementList elements = doc.MainDocumentPart.Document.Body.ChildElements;
        List<IDivision> paragraphs = new List<IDivision>();
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (IsFirstLineOfAnnex(e))
                break;
            IDivision para = ParseParagraph();
            if (para is null)
                continue;
            paragraphs.Add(para);
        }
        return paragraphs;
    }

}

}