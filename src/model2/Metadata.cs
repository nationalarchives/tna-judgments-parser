
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse {

class WMetadata : IMetadata {

    private readonly MainDocumentPart main;
    private readonly Judgment judgment;

    internal WMetadata(MainDocumentPart main, Judgment judgment) {
        this.main = main;
        this.judgment = judgment;
    }

    public Court? Court() {
        WCourtType caseType = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<WCourtType>().FirstOrDefault();
        string code = caseType?.Code;
        if (code is null)
            return null;
        return Courts.ByCode[code];
    }

    public string DocumentId() {
        INeutralCitation cite = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<INeutralCitation>().FirstOrDefault();
        if (cite is not null) {
            Match match1;
            match1 = Regex.Match(cite.Text, @"^\[(\d{4})\] (EWCA) (Civ|Crim) (\d+)$");
            if (match1.Success) {
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[3].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + match1.Groups[4].Value;
            }
            match1 = Regex.Match(cite.Text, @"^\[(\d{4})\] (EWHC) (\d+) \([A-Z][a-z]+\)$");
            if (match1.Success) {
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + match1.Groups[3].Value;
            }
            match1 = Regex.Match(cite.Text, @"^\[(\d{4})\] (EWHC) (\d+) \([A-Z]+\)$");
            if (match1.Success) {
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + match1.Groups[3].Value;
            }
            throw new System.Exception();
        }
        WCourtType courtType = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<WCourtType>().FirstOrDefault();
        if (courtType is null)
            return null;
        string caseNo = CaseNo();
        if (caseNo is null)
            return null;
        Match match = Regex.Match(caseNo, @"(\d{5,})[/\.](\d{4})");
        if (match.Success)
            return match.Groups[2] + "/" + courtType.Code.ToLower() + "/" + match.Groups[1];
        match = Regex.Match(caseNo, @"(\d{5,})[/\.](\d\d)");
        if (match.Success) {
            int twoDigitYear = int.Parse(match.Groups[2].Value);
            int fourDigitYear = new CultureInfo("en-GB").Calendar.ToFourDigitYear(twoDigitYear);
            return fourDigitYear + "/" + courtType.Code.ToLower() + "/" + match.Groups[1];
        }
        return null;
    }

    public string CaseNo() {
        ICaseNo caseNo = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<ICaseNo>().FirstOrDefault();
        if (caseNo is not null)
            return caseNo.Text;
        caseNo = judgment.CoverPage?.OfType<ILine>().SelectMany(line => line.Contents).OfType<ICaseNo>().FirstOrDefault();
        if (caseNo is not null)
            return caseNo.Text;
        return null;
    }

    public string ComponentId() {
        return DocumentId();
    }

    public string Date() {
        IDocDate date = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<IDocDate>().FirstOrDefault();
        if (date is not null)
            return date.Date;
        date = judgment.Conclusions?.OfType<ILine>().SelectMany(line => line.Contents).OfType<IDocDate>().FirstOrDefault();
        if (date is not null)
            return date.Date;
        return null;
    }

    public Dictionary<string, Dictionary<string, string>> CSSStyles() {
        return DOCX.CSS.Extract(main);
    }

}

}
