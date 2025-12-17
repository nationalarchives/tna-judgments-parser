#nullable enable

using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record NIPreface(IEnumerable<IBlock> Blocks)
{

    internal static NIPreface? Parse(IParser<IBlock> parser)
    {
        List<IBlock> blocks = new List<IBlock?> {
            parser.Match(Parsers.TextContent("A")),
            parser.Match(Parsers.TextContent("Bill")),
            parser.Match(Parsers.TextContent("To")),
        }.Where(it => it is not null)
            .Select(it => it!)
            .ToList();
        if (blocks.Count == 0)
        {
            return null;
        }
        if (parser.Peek(Preamble.Parse) is Preamble preamble)
        {
            return new NIPreface(blocks);
        }

        blocks.AddRange(parser.AdvanceWhile(b => b is WLine line
            && line.IsLeftAligned()
            && !Preamble.IsStartByText(b)));

        return new NIPreface(blocks);


    }
}