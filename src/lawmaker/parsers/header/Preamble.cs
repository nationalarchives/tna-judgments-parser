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
    internal static Preamble? Parse(IParser<IBlock> parser)
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
                ..line2.Contents])]);

        }

        if (IsStartByText(line))
        {
            return new Preamble([line]);
        }
        return null;
    }

    internal static bool IsStart(WLine line) =>
        line.IsLeftAligned()
            && line.IsFlushLeft()
            && !line.IsAllItalicized();
    internal static bool IsStartByText(IBlock? block) => block is WLine line
        && (EnactingTextStart().IsMatch(line.TextContent.Trim())
            // A lone B is probably a drop-case B for the preamble
            || "B".Equals(line.NormalizedContent, StringComparison.CurrentCultureIgnoreCase));

    [GeneratedRegex(@"^B?e\s*it\s*enacted\s*by", RegexOptions.IgnoreCase)]
    private static partial Regex EnactingTextDropcapStart();

    [GeneratedRegex(@"^B?e\s*it\s*enacted\s*by", RegexOptions.IgnoreCase)]
    private static partial Regex EnactingTextStart();

    public XNode? Build(Document Document) =>
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
                    EnactingTextStart().Replace(string.Join(" ", Blocks.OfType<WLine>().Select(l => l.NormalizedContent)), ""))));
}
