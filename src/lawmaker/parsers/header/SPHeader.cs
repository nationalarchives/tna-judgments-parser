#nullable enable

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker.Headers;

record SPHeader(SPCoverPage? CoverPage, SPPreface Preface) : IHeader
{
    internal static SPHeader? Parse(IParser<IBlock> parser)
    {

        SPCoverPage? coverPage = null;
        while (!parser.IsAtEnd())
        {
            // Checking for the preface first ensures we don't
            // greedily match a ToC
            if (parser.Match(SPPreface.Parse) is SPPreface preface)
            {
                return new SPHeader(coverPage, preface);
            }
            // We rely on the presence of the long title which begins with a particular form
            // to know we are done parsing the header
            coverPage = parser.Match(SPCoverPage.Parse);
            if (coverPage is not null)
            {
                break;
            }
            // skip the unknown
            var _ = parser.Advance();
        }
        if (parser.Match(SPPreface.Parse) is SPPreface preface1)
        {
            return new SPHeader(coverPage, preface1);
        }
        // we didn't find a preface (specifically a long title) so we assume no header.
        return null;
    }

    public IHeader? Visit(IHeaderVisitor visitor, HeaderVisitorContext _)
    {
        return visitor.VisitSP(this);
    }
}
