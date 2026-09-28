#nullable enable

using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse;

internal class WNamedDate : INamedDate
{
    public required string Date { get; init; }

    public required string Name { get; init; }
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

    public virtual Court? Court
    {
        get
        {
            if (field is not null)
            {
                return field;
            }

            var courtType1 = Util.Descendants<WCourtType>(judgment.Header).FirstOrDefault();
            if (courtType1?.Code is not null)
            {
                field = Courts.GetByCode(courtType1.Code);
            }

            if (field is null)
            {
                var courtType2 = Util.Descendants<WCourtType2>(judgment.Header).FirstOrDefault();
                if (courtType2?.Code is not null)
                {
                    field = Courts.GetByCode(courtType2.Code);
                }
            }

            field = field?.Code switch
            {
                Courts.EwfcCourtCode when Cite is not null
                    && Courts.EWFC_B.CitationPattern!.IsMatch(Cite) => Courts.EWFC_B,

                Courts.EwcopCourtCode when Cite is not null
                    && Courts.EWCOP_T1.CitationPattern!.IsMatch(Cite) => Courts.EWCOP_T1,
                Courts.EwcopCourtCode when Cite is not null
                    && Courts.EWCOP_T2.CitationPattern!.IsMatch(Cite) => Courts.EWCOP_T2,
                Courts.EwcopCourtCode when Cite is not null
                    && Courts.EWCOP_T3.CitationPattern!.IsMatch(Cite) => Courts.EWCOP_T3,

                null when Cite is not null => Courts.ExtractFromCitation(Cite),
                _ => field
            };
            return field;
        }
    }

    public virtual IEnumerable<IDocJurisdiction> Jurisdictions => Util.Descendants<IDocJurisdiction>(judgment.Header);

    public virtual int? Year => ShortUriComponent is null ? null : Citations.YearFromUriComponent(ShortUriComponent);

    public virtual int? Number =>
        ShortUriComponent is null ? null : Citations.NumberFromUriComponent(ShortUriComponent);

    public virtual string? Cite
    {
        get
        {
            if (field is not null)
            {
                return field;
            }

            var cite = GetFirstOrDefaultFromHeaderOrCoverPage<INeutralCitation>();
            if (cite is not null)
            {
                field = Citations.Normalize(cite.Text);
                return field;
            }

            var cite2 = GetFirstOrDefaultFromHeaderOrCoverPage<INeutralCitation2>();
            if (cite2 is not null)
            {
                field = Citations.Normalize(cite2.Text);
            }

            return field;
        }
    }

    private T? GetFirstOrDefaultFromHeaderOrCoverPage<T>()
    {
        var item = Util.Descendants<T>(judgment.Header).FirstOrDefault();
        if (item is null && judgment.CoverPage is not null)
        {
            item = Util.Descendants<T>(judgment.CoverPage).FirstOrDefault();
        }

        return item;
    }

    public virtual string? ShortUriComponent
    {
        get
        {
            if (field is not null)
            {
                return field;
            }

            if (Cite is not null)
            {
                field = Citations.MakeUriComponent(Cite);
            }

            return field;
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

    public virtual INamedDate? Date => Util.Descendants<IDocDate>(judgment)
                                           .OrderByDescending(dd => (dd as IDate).Date)
                                           .FirstOrDefault();

    public virtual string Name => CaseName.Extract(judgment);

    public Dictionary<string, Dictionary<string, string>> CSSStyles()
    {
        return DOCX.CSS.Extract(main, "#judgment");
    }

    public IEnumerable<IExternalAttachment> ExternalAttachments { get; init; }
}
