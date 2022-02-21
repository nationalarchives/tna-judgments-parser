using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.Parse {

class SupremeCourtParser : AbstractParser {

    private static ILogger logger = Logging.Factory.CreateLogger<SupremeCourtParser>();

    public static Judgment Parse(WordprocessingDocument doc) {
        return new SupremeCourtParser(doc).Parse();
    }
    public static Judgment Parse2(WordprocessingDocument doc, IOutsideMetadata meta) {
        return new SupremeCourtParser(doc, meta).Parse();
    }
    public static Judgment Parse3(WordprocessingDocument doc, IOutsideMetadata meta, IEnumerable<WordprocessingDocument> attachments) {
        return new SupremeCourtParser(doc, meta, attachments).Parse();
    }

    private SupremeCourtParser(WordprocessingDocument doc, IOutsideMetadata meta = null, IEnumerable<WordprocessingDocument> attachments = null) : base(doc, meta, attachments) { }

    /* second page break or first page break if first break is followed by a 'speech' */
    protected override List<IBlock> Header() {
        List<IBlock> header = new List<IBlock>();
        bool firstPageBreakFound = false;
        while (i < elements.Count) {
            logger.LogTrace("parsing element " + i);
            OpenXmlElement e = elements.ElementAt(i);
            AddBlock(e, header);    // increments i
            if (Util.IsSectionOrPageBreak(e)) {
                if (firstPageBreakFound)
                    return header;
                while (i < elements.Count && IsSkippable(elements.ElementAt(i)))
                    i += 1;
                if (i == elements.Count)
                    return null;
                OpenXmlElement next = elements.ElementAt(i);
                if (IsTitledJudgeName(next))
                    return header;
                firstPageBreakFound = true;
            }
        }
        return null;
    }

    private List<Enricher> headerEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger(),
        // new UKSC.SealRemover(),
        new RestrictionsEnricher(),
        new UKSC.CiteEnricher(),
        new UKSC.DateEnricher(),
        new UKSC.PartyEnricher(),
        new UKSC.LocationEnricher(),
        new UKSC.JudgeEnricher()
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

    protected override bool IsTitledJudgeName(OpenXmlElement e) {
        if (e is not Paragraph p)
            return false;
        Style style = DOCX.Styles.GetStyle(main, p);
        if (style?.StyleName?.Val?.Value == "NewSpeech")
            return true;
        string text = Util.NormalizeSpace(e.InnerText);
        if (text.Split().Length > 3)
            return false;
        if (text.StartsWith("LORD ", StringComparison.InvariantCultureIgnoreCase))
            return true;
        if (text.StartsWith("LADY ", StringComparison.InvariantCultureIgnoreCase))
            return true;
        return false;
    }

    protected override bool IsFirstLineOfCrossHeading(OpenXmlElement e) {
        if (e is not Paragraph p)
            return false;
        Style style = DOCX.Styles.GetStyle(main, p);
        if (style?.StyleName?.Val?.Value is null)
            return false;
        if (style.StyleName.Val.Value == "Paraheading 2")
            return true;
        return false;
    }

}

}
