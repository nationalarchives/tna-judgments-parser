#nullable enable

using System.Collections.Generic;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record NIPreamble(IEnumerable<IBlock> Blocks)
{
    internal static NIPreamble? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is IBlock block && IsStartOfPreamble(block))
        {
            return new NIPreamble([block]);
        }
        return null;
    }

    internal static bool IsStartOfPreamble(IBlock? block) => block is WLine line
        && EnactingTextStart().IsMatch(line.TextContent.Trim());

    [GeneratedRegex(@"Be\s*it\s*enacted\s*by", RegexOptions.IgnoreCase)]
    private static partial Regex EnactingTextStart();
}
