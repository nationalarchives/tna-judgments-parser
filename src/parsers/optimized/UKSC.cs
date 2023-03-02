
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using Microsoft.Extensions.Logging;

using AttachmentPair = System.Tuple<DocumentFormat.OpenXml.Packaging.WordprocessingDocument, UK.Gov.Legislation.Judgments.AttachmentType>;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class OptimizedUKSCParser : OptimizedParser {

    private static ILogger logger = Logging.Factory.CreateLogger<OptimizedUKSCParser>();

    public static Judgment Parse(WordprocessingDocument doc, WordDocument preParsed, IOutsideMetadata meta = null, IEnumerable<AttachmentPair> attachments = null) {
        return new OptimizedUKSCParser(doc, preParsed, meta, attachments).ProtectedParse(JudgmentType.Judgment);
    }

    private OptimizedUKSCParser(WordprocessingDocument doc, WordDocument preParsed, IOutsideMetadata meta = null, IEnumerable<AttachmentPair> attachments = null) : base(doc, preParsed, meta, attachments) { }

    /* second page break or first page break if first break is followed by a 'speech' */
    protected override List<IBlock> Header() {
        List<IBlock> header = new List<IBlock>();
        bool firstPageBreakFound = false;
        while (i < PreParsed.Body.Count) {
            logger.LogTrace("parsing element " + i);
            BlockWithBreak b = PreParsed.Body.ElementAt(i);
            if (firstPageBreakFound && b.LineBreakBefore)
                return header;
            if (b.LineBreakBefore && IsTitledJudgeName(b.Block))
                return header;
            header.Add(b.Block);
            i += 1;
            if (b.LineBreakBefore)
                firstPageBreakFound = true;
        }
        return null;
    }

    private List<Enricher> headerEnrichers = new List<Enricher>() {
        // new UKSC.SealRemover(),
        new RestrictionsEnricher(),
        new UK.Gov.Legislation.Judgments.Parse.UKSC.CiteEnricher(),
        new UK.Gov.Legislation.Judgments.Parse.UKSC.DateEnricher(),
        new UK.Gov.Legislation.Judgments.Parse.UKSC.PartyEnricher(),
        new UK.Gov.Legislation.Judgments.Parse.UKSC.LocationEnricher(),
        new UK.Gov.Legislation.Judgments.Parse.UKSC.JudgeEnricher()
    };

    protected override IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        return Enricher.Enrich(header, headerEnrichers);
    }

    /* body */

    protected override List<IDecision> Body() {
        List<IDecision> opinions = Decisions();
        if (opinions is null)
            opinions = new List<IDecision>(1);
        List<IDivision> remainder = ParagraphsUntilEndOfBody();
        if (remainder.Count > 0) {
            IDecision dummy = new Decision() { Contents = remainder };
            opinions.Add(dummy);
        }
        return opinions;
    }

    protected override bool IsTitledJudgeName(IBlock b) {
        if (b is not WLine line)
            return false;
        if (line.Style == "NewSpeech")
            return true;
        string text = line.NormalizedContent;
        if (text.Split().Length > 3)
            return false;
        if (text.StartsWith("LORD ", StringComparison.InvariantCultureIgnoreCase))
            return true;
        if (text.StartsWith("LADY ", StringComparison.InvariantCultureIgnoreCase))
            return true;
        return false;
    }

    protected override bool IsFirstLineOfCrossHeading(IBlock b) {
        if (b is not WLine line)
            return false;
        // if (line.Style is null)
        //     return false;
        if (line.Style == "Paraheading 2")
            return true;
        return false;
    }

}

}
