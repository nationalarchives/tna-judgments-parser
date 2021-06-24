
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class EmploymentTribunalParser : AbstractParser {

    public static Judgment Parse(WordprocessingDocument doc) {
        return new EmploymentTribunalParser(doc).Parse();
    }

    private EmploymentTribunalParser(WordprocessingDocument doc) : base(doc) { }

    static bool Matches(WordprocessingDocument doc) {
        throw new NotImplementedException();
    }

    // EMPLOYMENT TRIBUNALS
    // THE EMPLOYMENT TRIBUNAL
    // EMPLOYMENT TRIBUNALS (SCOTLAND) -- sometimes (SCOTLAND) is the following line


    private readonly ISet<string> titles = new HashSet<string>() {
        "JUDGMENT",
        "FINAL JUDGMENT",
        "RESERVED JUDGMENT", "RESERVED JUDGMENT ON LIABILITY",
        "JUDGMENT OF THE EMPLOYMENT TRIBUNAL",
        "JUDGMENT ON AN OPEN PRELIMINARY HEARING"
    };

    protected override List<IBlock> Header() {
        List<IBlock> header = new List<IBlock>();
        while (i < 50 && i < elements.Count) {
            OpenXmlElement e = elements.ElementAt(i);
            AddBlock(e, header);
            if (titles.Contains(e.InnerText.Trim()))
                break;
        }
        if (i == elements.Count)
            return null;
        // OpenXmlElement next = elements.ElementAt(i + 1);
        // if (next.InnerText.TrimStart().StartsWith()) {

        // }
        return header;
    }

    protected override IEnumerable<IBlock> EnrichCoverPage(IEnumerable<IBlock> coverPage) {
        Enricher courtType = new EmploymentTribunalCourtType();
        Enricher caseNo = new CaseNo();
        return coverPage
            .Select(block => Merger.Singleton.Enrich(block))
            .Select(block => caseNo.Enrich(block));
    }

    protected override IEnumerable<IBlock> EnrichHeader(IEnumerable<IBlock> header) {
        Enricher courtType = new EmploymentTribunalCourtType();
        Enricher caseNo = new CaseNo();
        Enricher party = new PartyEnricher();
        return header
            .Select(block => Merger.Singleton.Enrich(block))
            .Select(block => courtType.Enrich(block))
            .Select(block => caseNo.Enrich(block))
            .Select(block => party.Enrich(block));
    }

    protected override List<IDecision> Body() {
        List<IDivision> contents = ParagraphsUntilEndOfBody();
        IDecision decision = new Decision() { Contents = contents };
        return new List<IDecision>(1) { decision };
    }

    protected override bool IsFirstLineOfConclusions(OpenXmlElement e) {
        return Regex.IsMatch(e.InnerText, @"^\s*Employment Judge:\s+[A-Z]");
    }

    protected override IEnumerable<IBlock> EnrichConclusions(IEnumerable<IBlock> conclusions) {
        Enricher dateOfJudgment = new EmploymentTribunalDateOfJudgment();
        return conclusions
            .Select(block => Merger.Singleton.Enrich(block))
            .Select(block => dateOfJudgment.Enrich(block));
    }

}

class EmploymentTribunalCourtType : Enricher {

    internal override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (line.Count() == 0)
            return line;
        IInline first = line.First();
        if (first is WText text) {
            string t = Regex.Replace(text.Text, @"\s+", " ").Trim();
            if (t.Equals("Employment Tribunals", StringComparison.OrdinalIgnoreCase) || t.Equals("Employment Tribunals (Scotland)", StringComparison.OrdinalIgnoreCase) || t.Equals("The Employment Tribunal", StringComparison.OrdinalIgnoreCase)) {
                WCourtType caseType = new WCourtType(text.Text, text.properties) { Code = Courts.EmploymentTribunal.Code };
                return line.Skip(1).Prepend(caseType);
            }
        }
        if (first is WImageRef || first is WLineBreak) {
            IInline second = line.Skip(1).FirstOrDefault();
            if (second is WText text2) {
                string t = Regex.Replace(text2.Text, @"\s+", " ").Trim();
                if (t.Equals("Employment Tribunals", StringComparison.OrdinalIgnoreCase) || t.Equals("Employment Tribunals (Scotland)", StringComparison.OrdinalIgnoreCase) || t.Equals("The Employment Tribunal", StringComparison.OrdinalIgnoreCase)) {
                    WCourtType caseType = new WCourtType(text2.Text, text2.properties) { Code = Courts.EmploymentTribunal.Code };
                    return line.Skip(2).Prepend(caseType).Prepend(first);
                }
            }
        }
        return line;
    }

}

class EmploymentTribunalDateOfJudgment : Enricher {

    internal override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (line.Count() == 0)
            return line;
        string pattern = @"^\s*Date of Judgment:\s+\d{1,2} (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}\s*$";
        Regex re = new Regex(pattern);
        if (!re.IsMatch(NormalizeLine(line)))
            return line;
        pattern = @"(\d{1,2})\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{4})";
        re = new Regex(pattern);
        return line.SelectMany<IInline, IInline>(i => {
            if (i is WText text) {
                Match match = re.Match(text.Text);
                if (match.Success) {
                    string before = text.Text.Substring(0, match.Index);
                    string during = text.Text.Substring(match.Index, match.Length);
                    CultureInfo culture = new CultureInfo("en-GB");
                    DateTime date = DateTime.Parse(during, culture);
                    string after = text.Text.Substring(match.Index + match.Length);
                    return new List<IInline>() {
                        new WText(before, text.properties),
                        new WDocDate(new List<WText>(1) { new WText(during, text.properties) }, date),
                        new WText(after, text.properties),
                    };
                } else {
                    return new List<IInline>(1) { text };
                }
            } else {
                return new List<IInline>(1) { i };
            }
        });
    }

}

}
