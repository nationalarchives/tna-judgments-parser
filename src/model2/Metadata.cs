
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
        if (ShortUriComponent is null)
            return null;
        return Citations.YearFromUriComponent(ShortUriComponent);
    } }

    virtual public int? Number { get {
        if (ShortUriComponent is null)
            return null;
        return Citations.NumberFromUriComponent(ShortUriComponent);
    } }

    private string _cite = null;

    virtual public string Cite { get {
        if (_cite is not null)
            return _cite;
        INeutralCitation cite = Util.Descendants<INeutralCitation>(judgment.Header).FirstOrDefault();
        if (cite is null)
            return null;
        _cite = Citations.Normalize(cite.Text);
        return _cite;
    } }

    private string _id = null;
    
    virtual public string ShortUriComponent { get {
        if (_id is not null)
            return _id;
        if (Cite is not null)
            _id = Citations.MakeUriComponent(Cite);
        return _id;
    } }

    public string Domain = "https://caselaw.nationalarchives.gov.uk/";

    public string WorkThis { get => Domain + "id/" + ShortUriComponent; }
    public string WorkURI { get => WorkThis; }

    public string ExpressionThis { get => Domain + ShortUriComponent; }
    public string ExpressionUri { get => ExpressionThis; }

    public string ManifestationThis { get => ExpressionThis + "/data.xml"; }
    public string ManifestationUri { get => ManifestationThis; }

    public IEnumerable<string> CaseNos() {
        IEnumerable<ICaseNo> caseNos = Util.Descendants<ICaseNo>(judgment.Header);
        if (judgment.CoverPage is not null)
            caseNos = judgment.CoverPage.OfType<ILine>().SelectMany(line => line.Contents).OfType<ICaseNo>().Concat(caseNos);
        return caseNos.Select(cn => cn.Text);
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

    virtual public string Name { get {
        return UK.Gov.Legislation.Judgments.CaseName.Extract(judgment);
    } }

    public Dictionary<string, Dictionary<string, string>> CSSStyles() {
        return DOCX.CSS.Extract(main, "#judgment");
    }

    public IEnumerable<IExternalAttachment> ExternalAttachments { get; init; }

}

}
