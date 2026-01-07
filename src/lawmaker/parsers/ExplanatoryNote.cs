#nullable enable

using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.LanguageService;

namespace UK.Gov.Legislation.Lawmaker;
internal class ExplanatoryNote : BlockContainer
{

    public override string Name { get; internal init; } = "blockContainer";

    public override string Class { get; internal init; } = "explanatoryNote";

    private static readonly LanguagePatterns HeadingPatterns = new()
    {
        [Lang.EN] = [@"^EXPLANATORY +NOTE$"],
        [Lang.CY] = [@"^NODYN +ESBONIADOL"]
    };

    public static bool IsHeading(LanguageService langService, IBlock? block)
    {
        if (block is not WLine line)
            return false;
        return langService.IsMatch(line.NormalizedContent, HeadingPatterns);
    }

    public static bool IsSubheading(IBlock? block)
    {
        if (block is not WLine line)
            return false;
        string text = line.NormalizedContent;
        return text.StartsWith('(') && text.EndsWith(')');
    }

    public static bool IsTblockHeading(IBlock? block)
    {
        if (block is not WLine line)
            return false;
        return line.IsAllBold();
    }

    public static ExplanatoryNote? Parse(IParser<IBlock> parser)
    {
        // Handle heading and subheading
        WLine? heading = parser.Match(Parsers.WLine(line => IsHeading(parser.LanguageService, line)));
        if (heading is null)
        {
            return null;
        }

        WLine? subheading = parser.Match(Parsers.WLine(IsSubheading));

        List<IBlock>? content = parser.MatchWhile(
            // If we hit the the start of the Commencement History table,
            // then the Explanatory Note must have ended.
            block => !CommencementHistory.IsHeading(parser.LanguageService, block)
                && !(block is WLine line && line.IsCenterAligned()),
            p =>
            {
                if (p.Match(ParseHeadingTblock) is HeadingTblock tblock)
                {
                    return tblock;
                } else
                {
                    return p.Advance();
                }
            }
        );
        if (content is null || content.Count == 0)
        {
            return null;
        }
        BlockParser blockParser = new(content) { LanguageService = parser.LanguageService };
        IEnumerable<IBlock> structuredContent = BlockList.ParseFrom(blockParser);
        return new ExplanatoryNote { Heading = heading, Subheading = subheading, Content = structuredContent };

    }

    private static HeadingTblock? ParseHeadingTblock(IParser<IBlock> parser)
    {
        WLine? heading = parser.Match(Parsers.WLine(IsTblockHeading));
        if (heading is null)
        {
            return null;
        }

        List<IBlock>? content = parser.MatchWhile(
            // If we hit the the start of the Commencement History table,
            // then the Explanatory Note must have ended.
            block => !CommencementHistory.IsHeading(parser.LanguageService, block)
                // If we hit another Tblock heading, this Tblock must end.
                && !ExplanatoryNote.IsTblockHeading(block),
            p => p.Advance()
        );
        if (content is null || content.Count == 0)
        {
            return null;
        }
        BlockParser blockParser = new(content) { LanguageService = parser.LanguageService };
        IEnumerable<IBlock> structuredContent = BlockList.ParseFrom(blockParser);
        return new HeadingTblock { Heading = heading, Content = structuredContent };
    }
}
