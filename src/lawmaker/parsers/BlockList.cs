
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

            if (BlockList.ParseOuter(parser) is BlockList blockList)
                enrichedBlocks.Add(blockList);
            else
                enrichedBlocks.Add(parser.Advance());
        }
        return enrichedBlocks;
    }

    internal static BlockList? ParseOuter(IParser parser)
    {
        int save = parser.Save();
        if (parser.Match(ParseBlockList) is BlockList blockList)
        {
            if (blockList.Intro is null && blockList.Children?.First() is BlockListItem bli && bli.Number is null)
            {
                parser.Restore(save);
                return null;
            }
            return blockList;
        }
        return null;
    }

    internal static BlockList? ParseInner(IParser parser)
    {
        if (parser.Match(ParseBlockList) is BlockList blockList)
            return blockList;
        return null;
    }

    private static BlockList? ParseBlockList(IParser parser)
    {
        IBlock block = parser.Current();
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
            IBlock? nextBlock = parser.Peek();
            if (nextBlock is not WLine nextLine)
                return null;

            float currentIndent = OptimizedParser.GetEffectiveIndent(line);
            float nextIndent = OptimizedParser.GetEffectiveIndent(nextLine);

            float threshold = 0.1f;
            if (nextIndent > currentIndent + threshold)
            {
                parser.Advance();
                if (!block.IsEmptyLine()) intro = line;
            }
        }

        IBlock? leader = intro;
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

            //if (children.Count == 0 && leader is null && !BlockListItem.HasNum(currentLine))
                //return null;

            // Stop parsing BlockListItem children upon reaching a line with insufficient indentation.
            if (leader != null)
            {
                float leaderIndent;
                if (leader is BlockListItem item)
                    leaderIndent = item.Indent;
                else
                    leaderIndent = OptimizedParser.GetEffectiveIndent(leader as WLine);

                float currentIndent = OptimizedParser.GetEffectiveIndent(currentLine);
                float threshold = 0.1f;
                // First BlockListItem child must have greater indent than previous line.
                if (children.Count == 0 && !(currentIndent > leaderIndent + threshold))
                    break;
                // Subsequent BlockListItem siblings must same or greater indent than previous siblings.
                if (children.Count > 0 && !(currentIndent >= leaderIndent - threshold))
                    break;
            }

            if (BlockListItem.Parse(parser) is BlockListItem blockListItem)
            {
                // Update leader to point to this BlockListItem child before advancing to the next.
                leader = blockListItem;
                children.Add(blockListItem);
                continue;
            }
            break;
        }

        if (children.Count == 0)
            return null;
        //BROKENif (intro is null && children.First() is BlockListItem bli && bli.Number is null)
            //return null;
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

        // Handle subsequent lines i.e. nested BlockLists

        while (!parser.IsAtEnd())
        {
            int save = parser.Save();
            IBlock nextBlock = parser.Current();
            if (nextBlock is not WLine nextLine)
                break;

            float threshold = 0.1f;
            float currentIndent = OptimizedParser.GetEffectiveIndent(nextLine);
            if (currentIndent <= indent + threshold) // TODO: handle extra paragraphs
            {
                parser.Restore(save);
                break;
            }

            if (BlockList.ParseInner(parser) is BlockList blockList)
                contents.Add(blockList);
        }
        contents = TidyBlockListContents(contents);

        if (contents.Count == 0)
            return null;
        return new BlockListItem { Number = number, Contents = contents, Indent = indent };
    }

    /* If this BlockListItem contains a nested BlockList as a child element, 
     * and this nested BlockList has no listIntroduction,
     * move the first WLine into the listIntroduction of the nested BlockList:
     * 
     *  <item>                  <item>
     *       <num/>                  <num/>
     *       *LINE*                  <blockList>     
     *       <blockList>                 <listIntroduction> 
     *           <item/>     --->            *LINE*
     *           <item/>                 </listIntroduction>
     *       </blockList>                <item/>
     *  </item>                          <item/>
     *                               </blockList>
     *                           </item>
     */
    internal static IList<IBlock> TidyBlockListContents(IList<IBlock> contents)
    {
        if (contents.Count() < 2)
            return contents;
        if (contents.First() is not WLine firstLine)
            return contents;
        if (contents.Skip(1).First() is not BlockList firstBlockList)
            return contents;
        if (firstBlockList.Intro is not null)
            return contents;

        BlockList tidiedBlockList = new BlockList { Intro = firstLine, Children = firstBlockList.Children };
        return contents.Skip(2).Prepend(tidiedBlockList).ToList();
    }

    internal static bool HasNum(WLine line)
    {
        if (line is WOldNumberedParagraph)
            return true;
        IEnumerable<IInline> contents = line.Contents;
        return contents.Count() >= 3 && contents.First() is IFormattedText && contents.Skip(1).First() is WTab;
    }

}