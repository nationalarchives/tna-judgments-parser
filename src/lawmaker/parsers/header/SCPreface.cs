
#nullable enable

using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record SCPreface(
    SCLongTitle LongTitle
) : IPreface {

    internal static SCPreface? Parse(IParser<IBlock> parser)
    {
        // we always expect a long title since it indicates the end of a preface
        var longTitle = parser.Match(SCLongTitle.Parse);


        if (longTitle is null)
        {
            return null;
        }

        return new SCPreface(
            longTitle
        );
    }

    public XNode? Build(Document Document) =>
        new XElement(akn + "preface",
            LongTitle?.Build(Document));
}