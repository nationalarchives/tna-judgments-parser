#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.XmlNamespaces;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record Preamble(IEnumerable<IBlock> Blocks) : IBuildable<XNode>
{

    private enum PreambleType
    {
        BeItEnacted,
        MayItTherefore,
    }

    private static IEnumerable<XNode> BuildType(PreambleType? type) => type switch
    {
        PreambleType.BeItEnacted =>
            [],
        PreambleType.MayItTherefore =>
            [new XElement(akn + "inline",
                new XAttribute("name", "dropCap"),
                new XText("W")),
            new XElement(akn + "inline",
                new XAttribute("name", "smallCaps"),
                new XText("HERAS"))],
        _ => [],
    };
    private PreambleType? Type { get; init; }
    internal static Preamble? BeItEnacted(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        }
        // Sometimes, the drop cap B is treated as a separate block
        if (line.NormalizedContent.Equals("B", StringComparison.CurrentCultureIgnoreCase)
            && parser.Advance() is WLine line2
            && EnactingTextDropcapStart().IsMatch(line2.NormalizedContent))
        {
            return new Preamble([WLine.Make(line2, [
                new WText("B", line2.Contents.OfType<WText>().FirstOrDefault()?.properties),
                ..line2.Contents])])
                {
                    Type = PreambleType.BeItEnacted
                };

        }

        if (IsStartByText(line))
        {
            return new Preamble([line])
            {
                Type = PreambleType.BeItEnacted
            };
        }
        return null;
    }

    internal static Preamble? MayItTherefore(IParser<IBlock> parser)
    {
        if (parser.Advance() is WLine whereas && Whereas().IsMatch(whereas.NormalizedContent))
        {
            var _skip = parser.AdvanceWhile(block => block is not WLine line || !PrivateBillEnactingTextStart().IsMatch(line.NormalizedContent));
        }
        return Parsers.WLine(line => PrivateBillEnactingTextStart().IsMatch(line.NormalizedContent))(parser) is WLine line
        ? new Preamble([line]) { Type = PreambleType.MayItTherefore }
        : null;


    }
    internal static bool IsStart(WLine line) =>
        line.IsLeftAligned()
            && line.IsFlushLeft()
            && !line.IsAllItalicized();
    internal static bool IsStartByText(IBlock? block) => block is WLine line
        && (EnactingTextStart().IsMatch(line.TextContent.Trim())
            || Whereas().IsMatch(line.TextContent.Trim())
            // A lone B is probably a drop-case B for the preamble
            || "B".Equals(line.NormalizedContent, StringComparison.CurrentCultureIgnoreCase));

    [GeneratedRegex(@"^B?e\s*it\s*enacted\s*by", RegexOptions.IgnoreCase)]
    private static partial Regex EnactingTextDropcapStart();

    [GeneratedRegex(@"^B?e\s*it\s*enacted\s*by", RegexOptions.IgnoreCase)]
    private static partial Regex EnactingTextStart();

    [GeneratedRegex(@"^May\s*it\s*therefore\s*please", RegexOptions.IgnoreCase)]
    private static partial Regex PrivateBillEnactingTextStart();

    [GeneratedRegex(@"^W?hereas", RegexOptions.IgnoreCase)]
    private static partial Regex Whereas();

    public XNode? Build(Document Document) => this.Type switch
    {
        PreambleType.BeItEnacted =>
        new XElement(akn + "preamble",
            new XElement(akn + "formula",
                new XAttribute("name", "enactingText"),
                new XElement(akn + "p",
                    new XElement(akn + "inline",
                        new XAttribute("name", "dropCap"),
                        new XText("B")),
                    new XElement(akn + "inline",
                        new XAttribute("name", "smallCaps"),
                        new XText("e it enacted")),
                    EnactingTextStart().Replace(string.Join(" ", Blocks.OfType<WLine>().Select(l => l.NormalizedContent)), "")))),
        PreambleType.MayItTherefore =>
        new XElement(akn + "preamble",
            new XElement(akn + "formula",
                new XAttribute("name", "enactingText"),
                new XElement(akn + "p",
                    new XElement(akn + "inline",
                        new XAttribute("name", "dropCap"),
                        new XText("W")),
                    new XElement(akn + "inline",
                        new XAttribute("name", "smallCaps"),
                        new XText("HEREAS"))),
                new XElement(akn + "p",
                    EnactingTextStart().Replace(string.Join(" ", Blocks.OfType<WLine>().Select(l => l.NormalizedContent)), ""))
                )),
    };
}
