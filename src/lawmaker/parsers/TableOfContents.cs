#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using System.Text.RegularExpressions;
using static UK.Gov.Legislation.Lawmaker.LanguageService;
record TableOfContents(IEnumerable<ISingleLine> Lines)
{

    private static readonly LanguagePatterns ContentsHeadingPatterns = new()
    {
        [Lang.EN] = [@"^CONTENTS$"],
        [Lang.CY] = [@"^CYNNWYS$"]
    };

    public static bool IsTableOfContentsHeading(IBlock? block, LanguageService languageService) =>
        block is WLine line
            && line.IsCenterAligned()
            && languageService
                .IsMatch(line.NormalizedContent, ContentsHeadingPatterns);

    public static IParser<IBlock>.ParseStrategy<TableOfContents> Parse(System.Predicate<IBlock> takeWhile) => (IParser<IBlock> parser) =>
    {
        // Identify 'CONTENTS' heading
        if(parser.Advance() is not WLine heading)
        {
            return null;
        };
        if (!IsTableOfContentsHeading(heading, parser.LanguageService))
        {
            return null;
        }
        if (parser.MatchWhile(
            takeWhile,
            TableOfContentsLine.Parse,
            ToCType.Parse)
            is IEnumerable<ISingleLine> lines
            && lines.Any())
        {
            return new(lines.Prepend(new TableOfContentsLine(heading)));
        } else
        {
            return null;
        }
    };
}

interface ISingleLine{
    WLine Line { get; }
}

record TableOfContentsLine(WLine Line) : ISingleLine
{

    public static ISingleLine? Parse(IParser<IBlock> parser)
    {
        IBlock? block = parser.Advance();
        if (block is not WLine contentsLine)
            return null;
        // ToC Grouping provisions are center aligned
        if (contentsLine.IsCenterAligned()
            // ToC Prov1 elements are numbered
            || contentsLine is WOldNumberedParagraph
            // ToC Schedules (and associated grouping provisions) have hanging indents
            || contentsLine.FirstLineIndentWithNumber < 0)
        {
            return new TableOfContentsLine(contentsLine);
        }
        return null;
    }
}

partial record ToCType(WLine Line) : ISingleLine
{
    public static ISingleLine? Parse(IParser<IBlock> parser) =>
        parser.Advance() is WLine line
        && SectionRegex().IsMatch(line.TextContent)
        ? new ToCType(line)
        : null;

    [GeneratedRegex(@"Section")]
    private static partial Regex SectionRegex();

}