#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record CMPreface(
    CMLongTitle LongTitle
) : IPreface {

    internal static CMPreface? Parse(IParser<IBlock> parser)
    {
        // we always expect a long title since it indicates the end of a preface
        var longTitle = parser.Match(CMLongTitle.Parse);


        if (longTitle is null)
        {
            return null;
        }

        return new CMPreface(
            longTitle
        );
    }

    public XNode? Build(Document Document) =>
        new XElement(akn + "preface",
            new XAttribute("eId", "preface"),
            LongTitle?.Build(Document));
}