#nullable enable

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

using DocumentFormat.OpenXml.Vml;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker.Header;

record NIHeader(NICoverPage? CoverPage, NIPreface? Preface, NIPreamble? Preamble)
{
    internal static NIHeader? Parse(IParser<IBlock> parser)
    {
        NICoverPage? coverPage = parser.Match(NICoverPage.Parse);
        NIPreface? preface = parser.Match(NIPreface.Parse);
        NIPreamble? preamble = parser.Match(NIPreamble.Parse);
        return new NIHeader(coverPage, preface, preamble);
    }
}

record NICoverPage(IEnumerable<IBlock> Blocks)
{
    internal static NICoverPage? Parse(IParser<IBlock> parser)
    {
        List<IBlock> blocks = [];
        if (parser.Match(NIBillTitle.Parse) is WLine title)
        {
            blocks.Add(title);
        }
        if (parser.Match(NIStageVersion.Parse) is WLine stageVersion)
        {
            blocks.Add(stageVersion);
        }
        // A cover page *always* has a table of contents
        if (parser.Match(
            TableOfContents.Parse(
                block => parser.Peek(NIPreface.Parse) is not NIPreface preface))
            is TableOfContents toc)
        {
            // normally a front cover must have a ToC, but we want to be more permissive here
            blocks.AddRange(toc.Lines.Select(line => line.Line));
        }
        // At the moment we're ignoring table of contents as we rely on users generating it in Lawmaker instead.
        // Intentionally *not* adding it to the list of blocks
        return blocks switch
        {
            [] => null,
            _ => new NICoverPage(blocks),

        };


    }
}

record NIBillTitle
{
    internal static WLine? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        };

        // Could possibly check if the title ends with "Bill" as well
        if (line.IsCenterAligned() && line.IsAllBold())
        {
            return line;
        }

        return null;
    }
}

record NIStageVersion
{
    // We may want to parse metadata here
    internal static WLine? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is not WLine line)
        {
            return null;
        };
        if (line.IsCenterAligned()
            && line.NormalizedContent.StartsWith('[')
            && line.NormalizedContent.EndsWith(']'))
        {
            return line;
        }
        return null;



    }
}

record NIPreface(IEnumerable<IBlock> Blocks)
{
    static IParser<IBlock>.ParseStrategy<IBlock> TextContent(string content) => (IParser<IBlock> parser) =>
        parser.Match(p =>
            p.Advance() is WLine line
            && (line
                .TextContent?
                .Trim()?
                .Equals(content, System.StringComparison.InvariantCultureIgnoreCase) ?? false) ? line : null);


    static internal NIPreface? Parse(IParser<IBlock> parser)
    {
        List<IBlock> blocks = new List<IBlock?> {
            parser.Match(TextContent("A")),
            parser.Match(TextContent("Bill")),
            parser.Match(TextContent("To")),
        }.Where(it => it is not null)
            .Select(it => it!)
            .ToList();
        if (blocks.Count == 0)
        {
            return null;
        }
        if (parser.Peek(NIPreamble.Parse) is NIPreamble preamble)
        {
            return new NIPreface(blocks);
        }

        blocks.AddRange(parser.AdvanceWhile(b => b is WLine line
            && line.IsLeftAligned()
            && !NIPreamble.IsStartOfPreamble(b)));

        return new NIPreface(blocks);
    }
}

partial record NIPreamble(IEnumerable<IBlock> Blocks)
{
    internal static NIPreamble? Parse(IParser<IBlock> parser)
    {
        if (parser.Advance() is IBlock block && IsStartOfPreamble(block))
        {
            return new NIPreamble([block]);
        }
        return null;
    }

    internal static bool IsStartOfPreamble(IBlock? block) => block is WLine line
        && EnactingTextStart().IsMatch(line.TextContent.Trim());

    [GeneratedRegex(@"Be\s*it\s*enacted\s*by", RegexOptions.IgnoreCase)]
    private static partial Regex EnactingTextStart();
}