
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

using AttachmentPair = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;

namespace UK.Gov.Legislation.Judgments.Parse {

class CourtOfAppealParser : AbstractParser {

    private static ILogger logger = Logging.Factory.CreateLogger<CourtOfAppealParser>();

    public static Judgment Parse(WordprocessingDocument doc) {
        return new CourtOfAppealParser(doc).Parse();
    }
    public static Judgment Parse2(WordprocessingDocument doc, IOutsideMetadata meta) {
        return new CourtOfAppealParser(doc, meta).Parse();
    }
    public static Judgment Parse3(WordprocessingDocument doc, IOutsideMetadata meta, IEnumerable<AttachmentPair> attachments) {
        return new CourtOfAppealParser(doc, meta, attachments).Parse();
    }

    protected CourtOfAppealParser(WordprocessingDocument doc, IOutsideMetadata meta = null, IEnumerable<AttachmentPair> attachments = null) : base(doc, meta, attachments) { }

    ISet<string> titles = new HashSet<string>() {
        "Judgment", "JUDGMENT", "J U D G M E N T",
        "Judgement",
        "Approved Judgment", "Judgment Approved", "JUDGMENT (As Approved)", "Approved judgment",
        "Approved Judgement",   // [2022] EWHC 544 (Comm)
        "APPROVED JUDGMENT", "APPROVED JUDGEMENT",
        "APPROVED J U D G M E N T",
        "J U D G M E N T (Approved)", // EWCA/Crim/2017/1012
        // "J U D G M E N T  (Approved)",
        "J U D G M E N T (As approved)", // EWCA/Crim/2015/1870
        "J U D G M E N T (As Approved by the Court)",   // EWCA/Crim/2016/681
        "J U D G M E N T (As approved by the Court)",   // EWCA/Crim/2016/798
        "Judgment As Approved by the Court",    //  EWCA/Crim/2016/700
        "JUDGMENT: APPROVED BY THE COURT", // EWHC/Admin/2003/1321
        "APPROVED CORRECTED JUDGMENT",  // EWHC/Ch/2016/3302
        "Final Judgment",   // EWHC/Admin/2021/1234
        "Final Approved Judgment",  // [2021] EWHC 3455 (QB)
        "Costs Judgment", "COSTS JUDGMENT",
        "Judgment Approved by the court",   // [2021] EWCA Crim 1786
        "Judgment Approved by the courtfor handing down",
        "JUDGMENT : APPROVED BY THE COURT FOR HANDING DOWN (SUBJECT TO EDITORIAL CORRECTIONS)",  // EWCA/Civ/2003/494
        "Judgment Approved by the court for handing down (subject to editorial corrections)",    // EWCA/Civ/2017/320
        "Judgment Approved by the courtfor handing down (subject to editorial corrections)",    // EWCA/Civ/2017/320, line break between court / for
        "DRAFT JUDGMENT",    // EWCA/Civ/2003/952
        "APPROVED JUDGMENT ON A COSTS ISSUE",    // EWCA/Civ/2021/13
        "JUDGMENT on the ISSUE OF DAMAGES", // [2022] EWHC 1183 (Admin)
        "RULING ON THE COSTS OF THE APPLICATION FOR A COSTS CAPPING ORDER", //
        "Determination as to Venue",    // [2022] EWHC 152 (Admin)
        "Approved Consequentials Judgment", // [2022] EWHC 629 (Ch)

        /* EAT */
        "TRANSCRIPT OF ORAL JUDGMENT",
        "TRANSRIPT OF ORAL JUDGMENT"
    };

    ISet<string> rawTitles = new HashSet<string>() {
        "A P P R O V E D  J U D G M E N T" // [2022] EWCA Crim 381 (has two spaces between words)
    };

    protected override List<IBlock> Header() {
        List<OpenXmlElement> header1 = Header1();
        if (header1 is null)
            header1 = Header2();
        if (header1 is null)
            header1 = Header3();
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
            string text = Regex.Replace(e.InnerText, @"\s+", " ").Trim();
            if (titles.Contains(text))
                break;
            if (rawTitles.Contains(e.InnerText))
                break;
            Predicate<SectionProperties> isNewSection = (sectPr) => {
                return sectPr.ChildElements.OfType<PageNumberType>().Where(pgNumType => pgNumType.Start.HasValue).Any();
            };
            // [2022] EWFC 1
            if (e is Paragraph p && p.Descendants<SectionProperties>().Where(sectPr => isNewSection(sectPr)).Any())
                return header;
            header.Add(e);
        }
        if (i + header.Count < elements.Count)
            logger.LogInformation("found title: " + header.Last().InnerText);
        else
            logger.LogCritical("could not find title");
        foreach (var e in elements.Skip(i + header.Count)) {
            if (e is Paragraph p) {
                if (p.Descendants<SectionProperties>().Any())
                    return header;
                if (p.Descendants<PageBreakBefore>().Any()) // EWCA/Civ/2004/254
                    return header;
                if (DOCX.Numbering.HasNumberOrMarker(main, p))
                    return header;
                if (StartsWithTitledJudgeName(p))
                    return header;
                if (IsFirstLineOfBigLevel(p))   // [2022] EWHC 207 (Ch)
                    return header;
            }
            header.Add(e);
        }
        return null;
    }
    private List<OpenXmlElement> Header2() {
        List<OpenXmlElement> header = new List<OpenXmlElement>();
        foreach (var e in elements.Skip(i)) {
            string trimmed = e.InnerText.Trim();
            if (trimmed == "Introduction" || trimmed == "INTRODUCTION") {
                logger.LogDebug("ending header at " + trimmed);
                return header;
            }
            header.Add(e);
        }
        return null;
    }
    private List<OpenXmlElement> Header3() {
        List<OpenXmlElement> header = new List<OpenXmlElement>();
        foreach (var e in elements.Skip(i)) {
            header.Add(e);
            if (Util.IsSectionBreak(e))
                return header;
            if (Regex.IsMatch(e.InnerText, @"© CROWN COPYRIGHT \d{4}"))
                return header;
        }
        return null;
    }

    protected override List<IDecision> Body() {
        List<IDecision> decisions = Decisions();
        List<IDivision> remainder = ParagraphsUntilEndOfBody();
        if (decisions is null || decisions.Count == 0) {
            IDecision decision = new Decision() { Contents = remainder };
            decisions = new List<IDecision>(1) { decision };
        } else if (remainder.Count > 0) {
            IDecision dummy = new Decision() { Contents = remainder };
            decisions.Add(dummy);
        }
        return decisions;
    }

    /* enrich */

    private List<Enricher> headerEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger(),
        new RestrictionsEnricher(),
        new NetrualCitation(),
        new CaseNo(),
        new CourtType(),
        new DocDate(),
        new PartyEnricher(),
        new Judge(),
        new LawyerEnricher()
    };

    protected override IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        return Enricher.Enrich(header, headerEnrichers);
    }

}

}