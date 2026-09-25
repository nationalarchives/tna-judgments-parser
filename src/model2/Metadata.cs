using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse;

internal class WNamedDate : INamedDate
{
    public string Date { get; internal init; }

    public string Name { get; internal init; }
}

internal class WMetadata : IMetadata
{
    private readonly MainDocumentPart main;
    private readonly IJudgment judgment;

    internal WMetadata(MainDocumentPart main, IJudgment judgment)
    {
        this.main = main;
        this.judgment = judgment;
        ExternalAttachments = [];
    }

    protected WMetadata(MainDocumentPart main, IJudgment judgment, IEnumerable<IExternalAttachment> attachments)
    {
        this.main = main;
        this.judgment = judgment;
        ExternalAttachments = attachments;
    }

    private Court? _court;

    public virtual Court? Court
    {
        get
        {
            if (_court is not null)
            {
                return _court;
            }

            var courtType1 = Util.Descendants<WCourtType>(judgment.Header).FirstOrDefault();
            if (courtType1?.Code is not null)
            {
                _court = Courts.GetByCode(courtType1.Code);
            }

            if (_court is null)
            {
                var courtType2 = Util.Descendants<WCourtType2>(judgment.Header).FirstOrDefault();
                if (courtType2?.Code is not null)
                {
                    _court = Courts.GetByCode(courtType2.Code);
                }
            }

            if (_court?.Code == Courts.EWFC.Code && Cite is not null && Courts.EWFC_B.CitationPattern.IsMatch(Cite))
            {
                _court = Courts.EWFC_B;
            }

            if (_court?.Code == Courts.EWCOP.Code && Cite is not null && Courts.EWCOP_T1.CitationPattern.IsMatch(Cite))
            {
                _court = Courts.EWCOP_T1;
            }

            if (_court?.Code == Courts.EWCOP.Code && Cite is not null && Courts.EWCOP_T2.CitationPattern.IsMatch(Cite))
            {
                _court = Courts.EWCOP_T2;
            }

            if (_court?.Code == Courts.EWCOP.Code && Cite is not null && Courts.EWCOP_T3.CitationPattern.IsMatch(Cite))
            {
                _court = Courts.EWCOP_T3;
            }

            if (_court is null && Cite is not null)
            {
                _court = Courts.ExtractFromCitation(Cite);
            }

            return _court;
        }
    }

    public virtual IEnumerable<IDocJurisdiction> Jurisdictions => Util.Descendants<IDocJurisdiction>(judgment.Header);

    public virtual int? Year
    {
        get
        {
            if (ShortUriComponent is null)
            {
                return null;
            }

            return Citations.YearFromUriComponent(ShortUriComponent);
        }
    }

    public virtual int? Number
    {
        get
        {
            if (ShortUriComponent is null)
            {
                return null;
            }

            return Citations.NumberFromUriComponent(ShortUriComponent);
        }
    }

    private string _cite;

    public virtual string Cite
    {
        get
        {
            if (_cite is not null)
            {
                return _cite;
            }

            var cite = Util.Descendants<INeutralCitation>(judgment.Header).FirstOrDefault();
            if (cite is null && judgment.CoverPage is not null)
            {
                cite = Util.Descendants<INeutralCitation>(judgment.CoverPage).FirstOrDefault();
            }

            if (cite is not null)
            {
                _cite = Citations.Normalize(cite.Text);
                return _cite;
            }

            var cite2 = Util.Descendants<INeutralCitation2>(judgment.Header).FirstOrDefault();
            if (cite2 is null && judgment.CoverPage is not null)
            {
                cite2 = Util.Descendants<INeutralCitation2>(judgment.CoverPage).FirstOrDefault();
            }

            if (cite2 is not null)
            {
                _cite = Citations.Normalize(cite2.Text);
            }

            return _cite;
        }
    }

    private string _id;

    public virtual string ShortUriComponent
    {
        get
        {
            if (_id is not null)
            {
                return _id;
            }

            if (Cite is not null)
            {
                _id = Citations.MakeUriComponent(Cite);
            }

            return _id;
        }
    }

    public string Domain = "https://caselaw.nationalarchives.gov.uk/";

    public string WorkThis => Domain + "id/" + ShortUriComponent;
    public string WorkURI => WorkThis;

    public string ExpressionThis => Domain + ShortUriComponent;
    public string ExpressionUri => ExpressionThis;

    public string ManifestationThis => ExpressionThis + "/data.xml";
    public string ManifestationUri => ManifestationThis;

    public virtual IEnumerable<string> CaseNos()
    {
        var caseNos = Util.Descendants<ICaseNo>(judgment.Header);
        if (judgment.CoverPage is not null)
        {
            caseNos = judgment.CoverPage.OfType<ILine>().SelectMany(line => line.Contents).OfType<ICaseNo>()
                              .Concat(caseNos);
        }

        return caseNos.Select(cn => cn.Text);
    }

    public virtual INamedDate Date
    {
        get
        {
            var dates = Util.Descendants<IDocDate>(judgment);
            return dates.OrderByDescending(dd => (dd as IDate).Date).FirstOrDefault();
        }
    }

    public virtual string Name => CaseName.Extract(judgment);

    public Dictionary<string, Dictionary<string, string>> CSSStyles()
    {
        return DOCX.CSS.Extract(main, "#judgment");
    }

    public IEnumerable<IExternalAttachment> ExternalAttachments { get; init; }
}
