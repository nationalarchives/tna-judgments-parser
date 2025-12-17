#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record LongTitle(
    WLine? A,
    WLine? Bill,
    WLine? To,
    IEnumerable<WLine>? Rest) : IBuildable<XNode>
{
    internal static LongTitle? BigABillTo(IParser<IBlock> parser)
    {
        var (a, bill, to) = (
            parser.Match(Parsers.TextContent("A")),
            parser.Match(Parsers.TextContent("Bill")),
            parser.Match(Parsers.TextContent("To"))
        );
        if (a is null && bill is null && to is null)
        {
            return null;
        }
        var rest = parser.MatchWhile(Parsers.WLine(line =>
            line.IsLeftAligned()
            && !Preamble.IsStartByText(line)));

        return new LongTitle(a, bill, to, rest);
    }

    public XNode? Build(Document Document) =>
        new XElement(akn + "longTitle",
            A is null
            ? null
            : new XElement(akn + "p",
                new XText("A")),
            Bill is null
            ? null
            : new XElement(akn + "p",
                new XText("bill")),
            To is null
            ? null
            : new XElement(akn + "p",
                new XText("to")),
            Rest?.Select(line => new XElement(akn + "p", line.TextContent))

        );
}