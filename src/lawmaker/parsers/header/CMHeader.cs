#nullable enable

using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record CMHeader(CMPreface? Preface, WLine? Title) : IHeader
{

    internal static CMHeader? Parse(IParser<IBlock> parser)
    {
        WLine? title = null;
        while (parser.Peek(CMPreface.Parse) is null
                && !parser.IsAtEnd())
        {
            title = parser.Match(GenericBillTitle.Parse) ?? title;

            // skip the unknown element
            var _ = parser.Advance();
        }

        if (parser.IsAtEnd())
        {
            return null;
        }
        CMPreface? preface = parser.Match(CMPreface.Parse);


        return new CMHeader(preface, title);
    }
}
