
#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System;
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

class BlockList : IBlock
{
    public WLine? Intro { get; internal init; }

    public IList<IBlock>? Children { get; internal init; }

    public static IEnumerable<IBlock> ParseBlocks(IEnumerable<IBlock> blocks)
    {
        IParser parser = new BlockParser(blocks);
        List<IBlock> enrichedBlocks = new List<IBlock>();

        while (!parser.IsAtEnd())
        {
            // Strip all empty lines from the cell, with a caveat:
            // Leave a single empty line if the cell consists entirely of empty lines.
            if (parser.Current().IsEmptyLine() && enrichedBlocks.Count() > 0)
            {
                parser.Advance();
                continue;
            }

            if (parser.Match(BlockList.Parse) is BlockList blockList)
                enrichedBlocks.Add(blockList);
            else
                enrichedBlocks.Add(parser.Advance());
        }
        return enrichedBlocks;
    }

    public static BlockList? Parse(IParser parser)
    {
        IBlock block = parser.Current();
        if (block.IsEmptyLine())
            return null;
        if (block is not WLine line)
            return null;
        // We expect that BlockLists are always left-aligned.
        if (line.GetEffectiveAlignment() == AlignmentValues.Center)
            return null;
        if (line.GetEffectiveAlignment() == AlignmentValues.Right)
            return null;

        // A BlockList can have a listIntroduction, which is a single unnumbered line.
        WLine? intro = null;
        if (!BlockListItem.HasNum(line))
        {
            intro = line;
            parser.Advance();
        }

        // TODO: Remove leader variable?
        WLine? leader = null;
        float leaderIndent = 0f;
        if (parser.Peek(-1) is WLine prevLine)
        {
            leader = prevLine;
            leaderIndent = OptimizedParser.GetEffectiveIndent(leader);
        }

        List<IBlock> children = [];
        while (!parser.IsAtEnd())
        {
            // Skip over empty lines to ensure they do not influence parsing. 
            parser.AdvanceWhile(block => block.IsEmptyLine());
            if (parser.IsAtEnd()) 
                break;

            IBlock currentBlock = parser.Current();
            if (currentBlock is not WLine currentLine) 
                break;

            // Child must have greater indent than leader.
            float childIndent = OptimizedParser.GetEffectiveIndent(currentLine);
            float threshold = 0.1f;
            if (!(childIndent - leaderIndent > threshold))
                break;

            // Attempt to parse BlockListItem
            if (BlockListItem.Parse(parser) is BlockListItem blockListItem)
            {
                children.Add(blockListItem);
                continue;
            }
            break;
        }

        if (children.Count == 0)
            return null;
        return new BlockList { Intro = intro, Children = children };
    }
}

internal class BlockListItem : IBlock
{
    public IFormattedText? Number { get; internal init; }

    public required IList<IBlock> Contents { get; internal init; }

    public float Indent { get; internal init; }

    internal static BlockListItem? Parse(IParser parser)
    {
        if (parser.Match(ParseBlockListItem) is BlockListItem blockListItem)
            return blockListItem;
        return null;
    }

    private static BlockListItem? ParseBlockListItem(IParser parser)
    {
        IFormattedText? number = null;
        IList<IBlock> contents = [];

        // Handle first line
        IBlock block = parser.Advance();
        if (block is not WLine line)
            return null;

        float indent = OptimizedParser.GetEffectiveIndent(line);

        if (line is WOldNumberedParagraph np)
        {
            number = np.Number;
            line = WLine.RemoveNumber(np);
        }
        else if (HasNum(line))
        {
            number = line.Contents.First() as IFormattedText;
            line = new WLine(line, line.Contents.Skip(2));
        }
        contents.Add(line);

        // Handle subsequent lines belonging to this BlockListItem
        // i.e. nested BlockListItems

        while (!parser.IsAtEnd())
        {
            int save = parser.Save();
            IBlock nextBlock = parser.Current();
            if (nextBlock is not WLine nextLine)
                break;

            float threshold = 0.1f;
            float currentIndent = OptimizedParser.GetEffectiveIndent(nextLine);
            if (!(currentIndent - indent > threshold)) // TODO: handle extra paragraphs
            {
                parser.Restore(save);
                break;
            }

            if (BlockListItem.Parse(parser) is BlockListItem blockListItem)
                contents.Add(blockListItem);
        }

        if (contents.Count == 0)
            return null;
        return new BlockListItem { Number = number, Contents = contents, Indent = indent };
    }

    internal static bool HasNum(WLine line)
    {
        if (line is WOldNumberedParagraph)
            return true;
        IEnumerable<IInline> contents = line.Contents;
        return contents.Count() >= 3 && contents.First() is IFormattedText && contents.Skip(1).First() is WTab;
    }

}