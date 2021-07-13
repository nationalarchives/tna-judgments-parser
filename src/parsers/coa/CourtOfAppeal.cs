
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class CourtOfAppealParser : AbstractParser {

    public static Judgment Parse(WordprocessingDocument doc) {
        return new CourtOfAppealParser(doc).Parse();
    }

    private CourtOfAppealParser(WordprocessingDocument doc) : base(doc) { }

    protected override List<IBlock> Header() {
        List<IBlock> header = new List<IBlock>();
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (e.InnerText == "Approved Judgment")
                break;
            if (e.InnerText == "(Approved)" && elements.ElementAt(i-1).InnerText == "JUDGMENT")
                break;
            if (e is Paragraph p && p.ParagraphProperties?.ParagraphStyleId?.Val == "CoverDesc" && e.InnerText.StartsWith("Judgment Approved by the court"))
                break;
            AddBlock(e, header);
        }
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (e is Paragraph && IsTitledJudgeName(e.InnerText)) {
                return header;
            }
            AddBlock(e, header);
        }
        return null;
    }

    private static readonly string[] titledJudgeNamePatterns = {
        @"^MRS? JUSTICE [A-Z]+$",
        @"^(Lord|Lady|Mrs?|The Honourable Mrs?) Justice ([A-Z][a-z]* )?[A-Z][a-z]+(-[A-Z][a-z]+)?( VP)?$",
        @"^Mrs? ([A-Z]\.){1,3} [A-Z][a-z]+$",
        @"^[A-Z][a-z]+ [A-Z][a-z]+ QC, Deputy High Court Judge$"
    };
    IEnumerable<Regex> titledJudgeNameRegexes = titledJudgeNamePatterns
        .Select(p => new Regex(p));

    private bool IsTitledJudgeName(OpenXmlElement e) {
        if (e is not Paragraph)
            return false;
        return IsTitledJudgeName(e.InnerText);
    }
    private bool IsTitledJudgeName(string text) {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.EndsWith(":"))
            text = text.Substring(0 , text.Length - 1).Trim();
        foreach (Regex re in titledJudgeNameRegexes)
            if (re.IsMatch(text))
                return true;
        return false;
    }

    protected override  List<IDecision> Body() {
        List<IDecision> decisions = new List<IDecision>();
        while (i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            if (IsFirstLineOfAnnex(e))
                break;
            int save = i;
            IDecision decision = Decision();
            if (decision is null) {
                i = save;
                break;
            }
            decisions.Add(decision);
        }
        return decisions;
    }

    /* enrich */

    private List<Enricher> coverPageEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger()
    };

    protected override IEnumerable<IBlock> EnrichCoverPage(IEnumerable<IBlock> coverPage) {
        return Enricher.Enrich(coverPage, coverPageEnrichers);
    }

    private List<Enricher> headerEnrichers = new List<Enricher>() {
        new RemoveTrailingWhitespace(),
        new Merger(),
        new NetrualCitation(),
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