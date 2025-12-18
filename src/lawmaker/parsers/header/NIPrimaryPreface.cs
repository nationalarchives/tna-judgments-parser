#nullable enable

using System.Collections.Generic;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record NIPrimaryPreface(BillLongTitle LongTitle) : IBuildable<XNode>
{

    internal static NIPrimaryPreface? Parse(IParser<IBlock> parser)
    {
        var longTitle = parser.Match(BillLongTitle.BigABillTo);
        if (longTitle is null)
        {
            return null;
        }
        return new NIPrimaryPreface(longTitle);
    }

    public XNode? Build(Document Document) =>
        new XElement(akn + "preface",
            LongTitle.Build(Document));
}