#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using static UK.Gov.Legislation.Lawmaker.LanguageService;
record TableOfContents(IEnumerable<TableOfContentsLine> Lines)
{

    private static readonly LanguagePatterns ContentsHeadingPatterns = new()
    {
        [Lang.ENG] = [@"^CONTENTS$"],
        [Lang.CYM] = [@"^CYNNWYS$"]
    };
    public static TableOfContents? Parse(IParser<IBlock> parser)
    {
        // Identify 'CONTENTS' heading
        IBlock? block = parser.Advance();
        if (block is not WLine line
            || !LegislationParser.IsCenterAligned(line)
            || !parser.LanguageService
                .IsMatch(line.NormalizedContent, ContentsHeadingPatterns))
        {
            return null;
        }
        if (parser.MatchWhile(TableOfContentsLine.Parse)
            is IEnumerable<TableOfContentsLine> lines
            && lines.Any())
        {
            return new(lines);
        } else
        {
            return null;
        }
    }
}

record TableOfContentsLine(WLine Line)
{

    public static TableOfContentsLine? Parse(IParser<IBlock> parser)
    {
        IBlock? block = parser.Advance();
        if (block is not WLine contentsLine)
            return null;
        // ToC Grouping provisions are center aligned
        if (LegislationParser.IsCenterAligned(contentsLine)
            // ToC Prov1 elements are numbered
            || contentsLine is WOldNumberedParagraph
            // ToC Schedules (and associated grouping provisions) have hanging indents
            || contentsLine.FirstLineIndentWithNumber < 0)
        {
            return new(contentsLine);
        }
        return null;
    }
}