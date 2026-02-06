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
        Preamble? preamble = parser.Match(Preamble.BeItEnacted);
        return new NIHeader(coverPage, preface, preamble);
    }

    public IHeader? Visit(IHeaderVisitor visitor, HeaderVisitorContext _)
    {
        return visitor.VisitNI(this);
    }
}
