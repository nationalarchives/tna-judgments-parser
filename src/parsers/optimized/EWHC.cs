
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;


using AttachmentPair = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class OptimizedEWHCParser : OptimizedParser {

    private static ILogger logger = Logging.Factory.CreateLogger<OptimizedEWHCParser>();

    public static Judgment Parse(WordprocessingDocument doc, WordDocument preParsed, IOutsideMetadata meta = null, IEnumerable<AttachmentPair> attachments = null) {
        return new OptimizedEWHCParser(doc, preParsed, meta, attachments).ProtectedParse(JudgmentType.Judgment);
    }

    private OptimizedEWHCParser(WordprocessingDocument doc, WordDocument preParsed, IOutsideMetadata meta = null, IEnumerable<AttachmentPair> attachments = null) : base(doc, preParsed, meta, attachments) { }

    ISet<string> titles = new HashSet<string>() {
        "Judgment", "JUDGMENT", "J U D G M E N T",
        "Judgement",
        "Approved Judgment", "Judgment Approved", "JUDGMENT (As Approved)", "Approved judgment",
        "Approved Judgement",   // [2022] EWHC 544 (Comm)
        "APPROVED JUDGMENT", "APPROVED JUDGEMENT",
        "APPROVED J U D G M E N T",
        "JUDGMENT (Approved)", "J U D G M E N T (Approved)",
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
        "SUBSTANTIVE JUDGMENT", // [2023] EWHC 323 (Ch)
        "REDACTED JUDGMENT",  // [2023] EWHC 654 (Ch)

        /* EAT */
        "TRANSCRIPT OF ORAL JUDGMENT",
        "TRANSRIPT OF ORAL JUDGMENT"
    };

    ISet<string> rawTitles = new HashSet<string>() {
        "A P P R O V E D  J U D G M E N T" // [2022] EWCA Crim 381 (has two spaces between words)
    };

    Regex[] titleRegexes = new Regex[] {
        new Regex(@"^Judgement of [A-Z][a-z]+ [A-Z][a-z]+ KC$"), // [2022] EWFC 172
        new Regex(@"^© CROWN COPYRIGHT \d{4}$")
    };

    protected override List<IBlock> Header() {
        List<IBlock> header = Header1();
        if (header is null)
            header = Header2();
        if (header is null)
            header = Header3();
        if (header is null)
            header = Header4();
        if (header is null)
            return null;
        i += header.Count;
        return header;
    }
    private List<IBlock> Header1() {
        List<IBlock> header = new List<IBlock>();
        foreach (var b in PreParsed.Body.Skip(i)) {
            header.Add(b.Block);
            if (b.Block is not WLine line)
                continue;
            string text = line.NormalizedContent;
            if (titles.Contains(text)) {
                logger.LogInformation("found title: {0}", line.NormalizedContent);
                break;
            }
            if (rawTitles.Contains(line.TextContent)) {
                logger.LogInformation("found title: {0}", line.NormalizedContent);
                break;
            }
        }
        if (i + header.Count >= PreParsed.Body.Count)
            logger.LogWarning("could not find title");
        foreach (var b in PreParsed.Body.Skip(i + header.Count)) {
            if (b.LineBreakBefore)
                return header;
            if (b.Block is WOldNumberedParagraph np)
                return header;
            if (b.Block is ILine line) {
                if (StartsWithTitledJudgeName(line))
                    return header;
            }
            header.Add(b.Block);
        }
        return null;
    }
    private List<IBlock> Header2() {
        List<IBlock> header = new List<IBlock>();
        foreach (var b in PreParsed.Body.Skip(i).Select(bb => bb.Block)) {
            if (b is not WLine line) {
                header.Add(b);
                continue;
            }
            string trimmed = line.TextContent.Trim();
            if (trimmed == "Introduction" || trimmed == "INTRODUCTION" || trimmed == "Contents" || trimmed == "CONTENTS") {
                logger.LogDebug("ending header at " + trimmed);
                return header;
            }
            header.Add(b);
            if (trimmed.StartsWith("Approved Ruling on "))
                return header;
        }
        return null;
    }
    private List<IBlock> Header3() {
        List<IBlock> header = new List<IBlock>();
        foreach (var b in PreParsed.Body.Skip(i)) {
            if (b.LineBreakBefore)
                return header;
            header.Add(b.Block);
            if (b.Block is not WLine line)
                continue;
            foreach (Regex regex in titleRegexes)
                if (regex.IsMatch(line.NormalizedContent))
                    return header;
        }
        return null;
    }
    private List<IBlock> Header4() {
        List<IBlock> header = new List<IBlock>();
        foreach (var b in PreParsed.Body.Skip(i).Select(bb => bb.Block)) {
            if (b is WOldNumberedParagraph)
                return header;
            header.Add(b);
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