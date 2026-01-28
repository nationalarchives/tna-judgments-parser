
#nullable enable

using System.Text.RegularExpressions;
using System.Xml.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

interface IPreface : IBuildable<XNode> {}

partial record UKHeader(IPreface? Preface, Preamble? Preamble, WLine? Title, BracketedStageVersion? StageVersion) : IHeader
{

    internal static IParser<IBlock>.ParseStrategy<UKHeader> Parse(
        IParser<IBlock>.ParseStrategy<Preamble> preambleStrategy,
        IParser<IBlock>.ParseStrategy<IPreface> prefaceStrategy
        ) =>
    (IParser<IBlock> parser) =>
    {
        WLine? title = null;
        BracketedStageVersion? stageVersion = null;
        IPreface? preface = null;
        while (parser.Peek(preambleStrategy) is not Preamble _preamble
                && !parser.IsAtEnd())
        {
            preface = parser.Match(prefaceStrategy);
            if (preface is not null)
            {
                break;
            }
            if (parser.Match(GenericBillTitle.Parse) is WLine latestTitle)
            {
                title = latestTitle;
            }
            // TODO: handle title in running header
            else if (parser.Match(BracketedStageVersion.Parse) is BracketedStageVersion newStageVersion)
            {
                stageVersion = newStageVersion;
            } else
            {
                // skip the unknown element
                var _ = parser.Advance();
            }

        }

        var preamble = parser.Match(preambleStrategy);

        if (preface is null && preamble is null && title is null)
        {
            return null;
        }
        if (preface is UKPreface ukPreface && ukPreface.StageVersion is null)
        {
            preface = ukPreface with { StageVersion = stageVersion };
        }
        return new UKHeader(preface, preamble, title, stageVersion);
    };

    internal static WLine? Note(IParser<IBlock> parser) =>
        Parsers.WLine(line => line.IsAllBold()
        && NoteRegex().IsMatch(line.NormalizedContent))(parser);

    [GeneratedRegex(@"\s*\[[\w\s]*\]\s*")]
    private static partial Regex NoteRegex();

    public IHeader? Visit(IHeaderVisitor visitor, HeaderVisitorContext Context)
    {
        if (Context.DocName.IsWelshPrimary() || Context.DocName.IsWelshSecondary())
        {
            return visitor.VisitSC(this);
        }
        return visitor.VisitUK(this);
    }
}