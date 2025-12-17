#nullable enable

using System;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record GenericBillTitle
{
    internal static WLine? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        };

        if (line.NormalizedContent.EndsWith("Bill", StringComparison.CurrentCultureIgnoreCase))
        {
            return line;
        }

        return null;
    }
}
