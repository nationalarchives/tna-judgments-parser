#nullable enable

using DocumentFormat.OpenXml.Vml;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record NIHeader(NICoverPage? CoverPage, NIPreface? Preface, Preamble? Preamble) : IHeader
{
    internal static NIHeader? Parse(IParser<IBlock> parser)
    {
        NICoverPage? coverPage = parser.Match(NICoverPage.Parse);
        NIPreface? preface = parser.Match(NIPreface.Parse);
        Preamble? preamble = parser.Match(Preamble.Parse);
        return new NIHeader(coverPage, preface, preamble);
    }
}
