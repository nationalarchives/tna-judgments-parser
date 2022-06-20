
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
        "DECISION in PRINCIPLE",

        "DECISION OF THE UPPER TRIBUNAL",

        // "Decision: the application for judicial review is refused",

        "DETERMINATION AND REASONS",

        // "FINDINGS OF THE UPPER TRIBUNAL EXERCISING ITS HAMID JURISDICTION",

        // "REASONS FOR DECISION",

        "JUDGMENT",
        "APPROVED JUDGMENT"
    };

    Regex[] titles2 = new Regex[] {
        new Regex(@"\d+ DECISION")
    };

    protected bool IsTitleParagraph(OpenXmlElement e) {
        string text = Regex.Replace(e.InnerText, @"\s+", " ").Trim();
        foreach (string title in titles)
            if (text.Equals(title, StringComparison.InvariantCultureIgnoreCase))
                return true;
        foreach (Regex title in titles2)
            if (title.IsMatch(text))
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
        List<OpenXmlElement> header1 = Header1();
        if (header1 is null)
            header1 = Header2();
        if (header1 is null)
            header1 = Header3();
        if (header1 is null)
            header1 = Header4();
        if (header1 is null)
            return null;
        List<IBlock> header2 = new List<IBlock>(header1.Count);
        foreach (var e in header1)
            AddBlock(e, header2);
        return header2;
    }
    private List<OpenXmlElement> Header1() {
        List<OpenXmlElement> header = new List<OpenXmlElement>();
        foreach (var e in elements.Skip(i)) {
            header.Add(e);
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
    private List<OpenXmlElement> Header2() {
        List<OpenXmlElement> header = new List<OpenXmlElement>();
        var enumerator = elements.Skip(i).GetEnumerator();
        while (enumerator.MoveNext()) {
            var e = enumerator.Current;
            header.Add(e);
            if (Regex.Replace(e.InnerText, @"\s+", " ").Trim() == "J U D G M E N T") {
                if (enumerator.MoveNext()) {
                    OpenXmlElement next = enumerator.Current;
                    if (IsSkippable(next) && enumerator.MoveNext())
                        next = enumerator.Current;
                    if (next is Paragraph && IsDashedOrSolidLine(next)) {
                        header.Add(next);
                        return header;
                    }
                }
            }
        }
        return null;
    }
    private List<OpenXmlElement> Header3() {
        List<OpenXmlElement> header = new List<OpenXmlElement>();
        foreach (var e in elements.Skip(i)) {
            string trimmed = e.InnerText.Trim();
            if (trimmed == "Introduction" || trimmed == "INTRODUCTION")
                return header;
            if (trimmed == "DECISION Introduction")
                return header;
            if (trimmed == "DECISION INTRODUCTION AND SUMMARY")
                return header;
            if (trimmed == "INTRODUCTION AND OVERVIEW")
                return header;
            if (trimmed == "DIRECTIONS") // [2018] UKFTT 709 (TC)
                return header;
            if (trimmed == "IT IS DIRECTED that")   // ukftt/tc/2018/249
                return header;
            if (trimmed == "REASONS")   // [2022] UKFTT 00152 (GRC)
                return header;
            header.Add(e);
        }
        return null;
    }
    private List<OpenXmlElement> Header4() {
        List<OpenXmlElement> header = new List<OpenXmlElement>();
        foreach (var e in elements.Skip(i).SkipLast(10)) {
            header.Add(e);
            if (Util.IsSectionBreak(e))
                return header;
            if (Regex.IsMatch(e.InnerText, @"© CROWN COPYRIGHT \d{4}"))
                return header;
        }
        return null;
    }

    private List<Enricher> coverPageEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger(),
        new UKUT.Citation()
    };

    private List<Enricher> headerEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger(),
        new UKUT.CourtType(),
        new UKUT.Citation(),
        new UKUT.CourtType2(),
        new UKUT.CaseNo(),
        new UKUT.Date1(),
        new PartyEnricher()
    };

    protected override IEnumerable<IBlock> EnrichCoverPage(IEnumerable<IBlock> coverPage) {
        return Enricher.Enrich(coverPage, coverPageEnrichers);
    }

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