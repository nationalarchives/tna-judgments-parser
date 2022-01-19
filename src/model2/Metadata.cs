
using System;
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
        ExternalAttachments = Enumerable.Empty<IExternalAttachment>();
    }
    protected WMetadata(MainDocumentPart main, Judgment judgment, IEnumerable<IExternalAttachment> attachments) {
        this.main = main;
        this.judgment = judgment;
        ExternalAttachments = attachments;
    }

    virtual public Court? Court() {
        WCourtType courtType1 = Util.Descendants<WCourtType>(judgment.Header).FirstOrDefault();
        if (courtType1 is not null) {
            if (courtType1.Code is null)
                return null;
            return Courts.ByCode[courtType1.Code];
        }
        WCourtType2 courtType2 = Util.Descendants<WCourtType2>(judgment.Header).FirstOrDefault();
        if (courtType2 is not null) {
            if (courtType2.Code is null)
                return null;
            return Courts.ByCode[courtType2.Code];
        }
        string cite = this.Cite;
        if (cite is not null) {
            Court? c = UK.Gov.Legislation.Judgments.Courts.ExtractFromCitation(cite);
            if (c is not null)
                return c;
        }
        return null;
    }

    virtual public int? Year { get {
        string id = DocumentId();
        if (id is null)
            return null;
        return int.Parse(Regex.Match(id, @"/(\d+)/\d+$").Groups[1].Value);
    } }

    virtual public int? Number { get {
        string id = DocumentId();
        if (id is null)
            return null;
        return int.Parse(Regex.Match(id, @"/(\d+)$").Groups[1].Value);
    } }

    virtual public string Cite { get {
        INeutralCitation cite = Util.Descendants<INeutralCitation>(judgment.Header).FirstOrDefault();
        if (cite is null)
            return null;
        return cite.Text.Trim();
    } }

    private string _id = null;
    
    virtual public string DocumentId() {
        if (_id is null)
            _id = MakeDocumentId();
        return _id;
    }

    private string MakeDocumentId() {
        string cite = this.Cite;
        if (cite is not null) {
            string trimmed = Regex.Replace(cite, @"\s+", " ").Trim();
            Match match1;
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (UKSC|UKPC) (\d+)$", RegexOptions.IgnoreCase);
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWCA) (Civ|Crim) (\d+)$", RegexOptions.IgnoreCase);
            if (match1.Success) {
                string num = match1.Groups[4].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[3].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWHC) +(\d+) \(([A-Z][a-z]+)\.?\)$", RegexOptions.IgnoreCase);
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[4].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWHC) (\d+) ([A-Z][a-z]+)$", RegexOptions.IgnoreCase); // EWHC/Admin/2003/301
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[4].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWCH) (\d+) \(([A-Z][a-z]+)\)$", RegexOptions.IgnoreCase); // EWHC/Admin/2006/2373
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return "EWHC".ToLower() + "/" + match1.Groups[4].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWHC) (\d+) \(([A-Z]+)\)$", RegexOptions.IgnoreCase);
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[4].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            // match1 = Regex.Match(cite.Text, @"^\[(\d{4})\] (EWHC) (\d+)$"); // is this valid? EWHC/Admin/2004/584
            // if (match1.Success) {
            //     return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + match1.Groups[3].Value;
            // }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWCOP) (\d+)$", RegexOptions.IgnoreCase);
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWFC) (\d+)$", RegexOptions.IgnoreCase);
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWCA) (\d+) \((Civ|Crim)\)$", RegexOptions.IgnoreCase);
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[4].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            match1 = Regex.Match(trimmed, @"^\[(\d{4})\] (EWCA) (\d+) (Civ|Crim)$", RegexOptions.IgnoreCase);
            if (match1.Success) {
                string num = match1.Groups[3].Value.TrimStart('0');
                if (string.IsNullOrEmpty(num))
                    throw new System.Exception(cite);
                return match1.Groups[2].Value.ToLower() + "/" + match1.Groups[4].Value.ToLower() + "/" + match1.Groups[1].Value + "/" + num;
            }
            throw new System.Exception();
        }
        // WCourtType courtType = judgment.Header.OfType<ILine>().SelectMany(line => line.Contents).OfType<WCourtType>().FirstOrDefault();
        // if (courtType is null)
        //     return null;
        // string caseNo = CaseNo();
        // if (caseNo is null)
        //     return null;
        // Match match = Regex.Match(caseNo, @"(\d{5,})[/\.](\d{4})");
        // if (match.Success)
        //     return match.Groups[2] + "/" + courtType.Code.ToLower() + "/" + match.Groups[1];
        // match = Regex.Match(caseNo, @"(\d{5,})[/\.](\d\d)");
        // if (match.Success) {
        //     int twoDigitYear = int.Parse(match.Groups[2].Value);
        //     int fourDigitYear = new CultureInfo("en-GB").Calendar.ToFourDigitYear(twoDigitYear);
        //     return fourDigitYear + "/" + courtType.Code.ToLower() + "/" + match.Groups[1];
        // }
        return null;
    }

    public IEnumerable<string> CaseNos() {
        IEnumerable<ICaseNo> caseNos = Util.Descendants<ICaseNo>(judgment.Header);
        if (judgment.CoverPage is not null)
            caseNos = judgment.CoverPage.OfType<ILine>().SelectMany(line => line.Contents).OfType<ICaseNo>().Concat(caseNos);
        return caseNos.Select(cn => cn.Text);
    }

    public string ComponentId() {
        return DocumentId();
    }

    virtual public string Date() {
        IDocDate date = Util.Descendants<IDocDate>(judgment.Header).FirstOrDefault();
        if (date is not null)
            return date.Date;
        date = judgment.Conclusions?.OfType<ILine>().SelectMany(line => line.Contents).OfType<IDocDate>().FirstOrDefault();
        if (date is not null)
            return date.Date;
        return null;
    }

    virtual public string CaseName { get {
        return UK.Gov.Legislation.Judgments.CaseName.Extract(judgment);
    } }

    public Dictionary<string, Dictionary<string, string>> CSSStyles() {
        return DOCX.CSS.Extract(main, "#judgment");
    }

    public IEnumerable<IExternalAttachment> ExternalAttachments { get; init; }

}

}
