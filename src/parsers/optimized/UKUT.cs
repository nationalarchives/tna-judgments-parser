using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using AttachmentPair = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;
using UKUT = UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

namespace UK.Gov.NationalArchives.CaseLaw.Parse;

internal class OptimizedUKUTParser : OptimizedParser
{
    private static readonly ILogger logger = Logging.Factory.CreateLogger<OptimizedUKSCParser>();

    public static Judgment Parse(WordprocessingDocument doc, WordDocument preParsed, IOutsideMetadata meta = null,
        IEnumerable<AttachmentPair> attachments = null)
    {
        return new OptimizedUKUTParser(doc, preParsed, meta, attachments).ProtectedParse(JudgmentType.Decision);
    }

    private OptimizedUKUTParser(WordprocessingDocument doc, WordDocument preParsed, IOutsideMetadata meta = null, IEnumerable<AttachmentPair> attachments = null) : base(doc, preParsed, meta, attachments) { }


    private readonly string[] titles =
    [
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
        "DECISIONS OF THE FIRST-TIER TRIBUNAL",

        "Interlocutory Decision", // [2016] UKUT 337 (IAC)
        "DECISION ON APPLICATIONS", // [2016] UKUT 559 (IAC)
        "DECISION NOTICE", // [2024] UKFTT 721 (GRC)
        "Decision: The appeal is refused", // [2024] UKFTT 635 (GRC) -- StartsWith("Decision: ") below won't work b/c of "REASONS" here

        "DETERMINATION AND REASONS",

        "RULING AND DIRECTIONS", // [2012] UKUT 218 (IAC)

        "JUDGMENT",
        "APPROVED JUDGMENT",

        "REASONS",   // must go here b/c "Decision" might be the heading of the final section: [2022] UKFTT 282 (GRC)
        "OPEN REASONS", // [2023] UKFTT 412 (GRC)
        "REASONS FOR DECISION", // [2024] UKUT 127 (AAC)
        "SUMMARY of REASONS" // [2024] UKFTT 696 (GRC)
    ];

    private readonly Regex[] titles2 =
    [
        new(@"^Ruling on [a-z]+$", RegexOptions.IgnoreCase),
        new(@"^\d+ DECISION$")
    ];

    protected bool IsTitleParagraph(IBlock block)
    {
        if (block is not WLine line)
            return false;
        var text = line.NormalizedContent;
        foreach (var title in titles)
            if (text.Equals(title, StringComparison.InvariantCultureIgnoreCase))
                return true;
        foreach (var title in titles2)
            if (title.IsMatch(text))
                return true;
        return false;
    }

    protected bool IsDashedOrSolidLine(IBlock block)
    {
        if (block is not WLine line)
            return false;
        var text = line.TextContent;
        if (Regex.IsMatch(text, @"^ ?-( -)+ ?$"))
            return true;
        if (Regex.IsMatch(text, @"^_+$"))
            return true;
        return false;
    }

    protected override List<IBlock> Header()
    {
        var header = Header1();
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

    private List<IBlock> Header1()
    {
        var header = new List<IBlock>();
        foreach (var b in PreParsed.Body.Skip(i).Select(bb => bb.Block))
        {
            header.Add(b);
            if (IsTitleParagraph(b))
                return header;
            if (IsFirstLineOfAnnex(b))  // [2022] UKFTT 00416 (GRC)
                return null;
            if (b is WTable table)
            {
                var last = Util.Descendants<WLine>(table).Where(line => !string.IsNullOrWhiteSpace(line.TextContent)).LastOrDefault();
                if (last is not null && IsTitleParagraph(last))
                    return header;
            }
        }
        return null;
    }

    private List<IBlock> Header2()
    {
        var header = new List<IBlock>();
        var enumerator = PreParsed.Body.Skip(i).Select(bb => bb.Block).GetEnumerator();
        while (enumerator.MoveNext())
        {
            var b = enumerator.Current;
            header.Add(b);
            if (b is not WLine line)
                continue;
            if (line.NormalizedContent == "J U D G M E N T")
            {
                if (enumerator.MoveNext())
                {
                    var next = enumerator.Current;
                    if (IsDashedOrSolidLine(next))
                    {
                        header.Add(next);
                        return header;
                    }
                }
            }
            if (line.NormalizedContent.StartsWith("Decision: "))  // [2016] UKUT 00507 (IAC)
                return header;
        }
        return null;
    }

    private List<IBlock> Header3()
    {
        var header = new List<IBlock>();
        foreach (var b in PreParsed.Body.Skip(i).Select(bb => bb.Block))
        {
            if (b is not WLine line)
            {
                header.Add(b);
                continue;
            }

            var trimmed = line.TextContent.Trim();
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
            header.Add(b);
        }
        return null;
    }

    private List<IBlock> Header4()
    {
        var header = new List<IBlock>();
        foreach (var b in PreParsed.Body.Skip(i).SkipLast(10))
        {
            header.Add(b.Block);
            if (b.LineBreakBefore)
                return header;
            if (b.Block is not WLine line)
                continue;
            if (Regex.IsMatch(line.TextContent, @"© CROWN COPYRIGHT \d{4}"))
                return header;
        }
        return null;
    }

    private readonly List<Enricher> coverPageEnrichers = new()
    {
        // new RemoveTrailingWhitespace(),
        // new Merger(),
        new UKUT.Citation()
    };

    private readonly List<Enricher> headerEnrichers =
    [
        // new RemoveTrailingWhitespace(),
        // new Merger(),
        new UKUT.AppealNumberEnricher(),
        new UKUT.CourtType(),
        new UKUT.Citation(),
        new UKUT.CourtType2(),
        new UKUT.CaseNo(),
        new UKUT.Date1(),
        new PartyEnricher(),
        new UKUT.JudgeNames()
    ];

    protected override IEnumerable<IBlock> EnrichCoverPage(IEnumerable<IBlock> coverPage)
    {
        return Enricher.Enrich(coverPage, coverPageEnrichers);
    }

    protected override IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header)
    {
        return Enricher.Enrich(header, headerEnrichers);
    }

    protected override List<IDecision> Body()
    {
        // should allow for multiple decisions
        var contents = Divisions();
        if (contents is null)
            contents = [];
        contents.AddRange(ParagraphsUntilEndOfBody());
        var decision = new Decision { Author = null, Contents = contents };
        return [decision];
    }

    protected override IEnumerable<IDecision> EnrichBody(IEnumerable<IDecision> body)
    {
        var enriched = base.EnrichBody(body);
        return Enricher.Enrich(enriched, new List<Enricher>(1) { new UKUT.Date2() });
    }

    protected override IEnumerable<IBlock> EnrichConclusions(IEnumerable<IBlock> conclusions)
    {
        var enriched = base.EnrichConclusions(conclusions);
        return Enricher.Enrich(enriched, new List<Enricher>(1) { new UKUT.Date2() });
    }

    protected override bool IsFirstLineOfConclusions(IBlock block)
    {
        if (block is not WLine line)
            return false;
        if (line.TextContent.StartsWith("Signed: "))
            return true;
        return false;
    }

    protected override List<IDivision> ParagraphsUntilAnnex()
    {
        var paragraphs = new List<IDivision>();
        while (i < PreParsed.Body.Count)
        {
            var block = PreParsed.Body.ElementAt(i).Block;
            if (IsFirstLineOfAnnex(block))
                break;
            var para = ParseParagraph();
            if (para is null)
                continue;
            paragraphs.Add(para);
        }
        return paragraphs;
    }

}
