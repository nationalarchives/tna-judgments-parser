
#nullable enable

using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Headers;

partial record UKHeader(UKPreface? Preface, Preamble? Preamble, WLine? Title, BracketedStageVersion? StageVersion) : IHeader
{

    internal static IParser<IBlock>.ParseStrategy<UKHeader> Parse(IParser<IBlock>.ParseStrategy<Preamble> preambleStrategy) =>
    (IParser<IBlock> parser) =>
    {
        WLine? title = null;
        BracketedStageVersion? stageVersion = null;
        UKPreface? preface = null;
        while (parser.Peek(preambleStrategy) is not Preamble _preamble
                && !parser.IsAtEnd())
        {
            preface = parser.Match(UKPreface.Parse);
            if (preface is not null)
            {
                break;
            }
            title = parser.Match(GenericBillTitle.Parse) ?? title;
            // TODO: handle title in running header
            stageVersion = parser.Match(BracketedStageVersion.Parse) ?? stageVersion;

            // skip the unknown element
            var _ = parser.Advance();
        }

        var preamble = parser.Match(preambleStrategy);

        if (preface is null && preamble is null && title is null)
        {
            return null;
        }
        if (preface is not null && preface.StageVersion is null)
        {
            preface = preface with { StageVersion = stageVersion };
        }
        return new UKHeader(preface, preamble, title, stageVersion);
    };

    internal static WLine? Note(IParser<IBlock> parser) =>
        Parsers.WLine(line => line.IsAllBold()
        && NoteRegex().IsMatch(line.NormalizedContent))(parser);

    [GeneratedRegex(@"\s*\[[\w\s]*\]\s*")]
    private static partial Regex NoteRegex();
}