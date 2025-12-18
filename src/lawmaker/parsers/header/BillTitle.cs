#nullable enable

using System;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record GenericBillTitle
{
    internal static WLine? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        };

        if (TitleRegex().IsMatch(line.NormalizedContent))
        {
            return line;
        }

        return null;
    }

    [GeneratedRegex(@"Bill$|Measure$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();
}
