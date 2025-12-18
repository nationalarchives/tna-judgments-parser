
#nullable enable

using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record SCPreface(
    Reference? BillTitle,
    BracketedStageVersion StageVersion,
    SCLongTitle LongTitle
) : IPreface {

    internal static SCPreface? Parse(IParser<IBlock> parser)
    {
        WLine? billTitle = parser.Match(GenericBillTitle.Parse);
        BracketedStageVersion? stageVersion = parser.Match(BracketedStageVersion.Parse) ?? BracketedStageVersion.Default();
        // we always expect a long title since it indicates the end of a preface
        var longTitle = parser.Match(SCLongTitle.Parse);


        if (longTitle is null)
        {
            return null;
        }

        return new SCPreface(
            billTitle is not null
                ? new Reference(ReferenceKey.varBillTitle, billTitle.NormalizedContent)
                : null,
            stageVersion,
            longTitle
        );
    }

    public XNode? Build(Document Document) =>
        new XElement(akn + "preface",
            new XAttribute("eId", "preface"),
            new XElement(akn + "block",
                new XAttribute("name", "title"),
                new XElement(akn + "docTitle",
                    new XElement(akn + "ref",
                        new XAttribute(akn + "class", "placeholder"),
                        new XAttribute("href", $"#{Document.Metadata.Register(BillTitle)?.EId ?? "varBillTitle"}")))),
            (StageVersion ?? BracketedStageVersion.Default()).Build(Document),
            LongTitle?.Build(Document));
}