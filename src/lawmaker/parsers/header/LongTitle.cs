#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record BillLongTitle(
    WLine? A,
    WLine? Bill,
    WLine? To,
    IEnumerable<WLine>? Rest) : IBuildable<XNode>
{
    internal static BillLongTitle? BigABillTo(IParser<IBlock> parser)
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

        return new BillLongTitle(a, bill, to, rest);
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

partial record CMLongTitle(
    WLine Line
) : IBuildable<XNode> {
    internal static CMLongTitle? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        }
        if (Space().Replace(line.NormalizedContent, @" ").Trim().StartsWith("A measure of the General"))
        {
            return new CMLongTitle(line);
        }
        return null;

    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Space();

    public XNode? Build(Document Document) =>
        new XElement(akn + "longTitle",
            new XElement(akn + "p", Line.TextContent));
}

partial record SCLongTitle(
    WLine Line
) : IBuildable<XNode> {
    internal static SCLongTitle? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        }
        if (parser.LanguageService.IsMatch(
            Space().Replace(line.NormalizedContent, @"").Trim(),
            LongTitleStartPatterns
            )?.Count >= 1)
        {
            return new SCLongTitle(line);
        }
        return null;

    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Space();

    [GeneratedRegex(@"^AnActofSeneddCymru")]
    private static partial Regex LongTitleStartEnglish();

    [GeneratedRegex(@"^DeddfganSeneddCymru")]
    private static partial Regex LongTitleStartWelsh();

    private static readonly Dictionary<LanguageService.Lang, IEnumerable<Regex>> LongTitleStartPatterns = new()
    {
        [LanguageService.Lang.EN] = [ LongTitleStartEnglish() ],
        [LanguageService.Lang.CY] = [ LongTitleStartWelsh() ]
    };
    public XNode? Build(Document Document) =>
        new XElement(akn + "longTitle",
            new XElement(akn + "p", Line.TextContent));
}