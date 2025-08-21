
#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

class BlockList : IBlock
{
    public WLine? Intro { get; internal init; }

    public IList<IBlock>? Children { get; internal init; }

    internal static BlockList? Parse(IParser parser)
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

        WLine? intro = null;
        if (line is not WOldNumberedParagraph && !BlockListItem.IsNumberedWithTab(line))
            intro = parser.Advance() as WLine;

        WLine? leader = intro;
        List<IBlock> children = [];

        while (!parser.IsAtEnd())
        {
            // Skip over empty lines to ensure they do not influence parsing. 
            parser.AdvanceWhile(block => block.IsEmptyLine());

            IBlock currentBlock = parser.Current();
            if (currentBlock is not WLine currentLine) 
                break;

            // Stop parsing BlockListItem children upon reaching a line which is 
            // indented further to the left than the previous sibling.
            if (leader != null)
            {
                float leaderIndent = OptimizedParser.GetEffectiveIndent(leader);
                float currentIndent = OptimizedParser.GetEffectiveIndent(currentLine);
                if (currentIndent <= leaderIndent - 0.2f) 
                    break;
            }

            if (BlockListItem.Parse(parser) is BlockListItem blockListItem)
            {
                // Update leader to point to the start of the previous sibling
                leader = blockListItem.Contents.First() as WLine;
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
    public IFormattedText? Number { get; internal set; }

    public required IList<IBlock> Contents { get; internal init; }

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

        if (line is WOldNumberedParagraph np)
        {
            number = np.Number;
            line = WLine.RemoveNumber(np);
        }
        else if (IsNumberedWithTab(line))
        {
            number = line.Contents.First() as IFormattedText;
            line = new WLine(line, line.Contents.Skip(2));
        }
        contents.Add(line);

        // Handle subsequent lines
        float indent = OptimizedParser.GetEffectiveIndent(line);

        while (!parser.IsAtEnd())
        {
            int save = parser.Save();
            IBlock nextBlock = parser.Current();
            if (nextBlock is not WLine nextLine)
                break;

            float currentIndent = OptimizedParser.GetEffectiveIndent(nextLine);
            if (currentIndent <= indent + 0.2f) // TODO: handle extra paragraphs
            {
                parser.Restore(save);
                break;
            }

            if (BlockList.Parse(parser) is BlockList blockList)
                contents.Add(blockList);
        }
        contents = TidyBlockListContents(contents);

        if (contents.Count == 0)
            return null;
        return new BlockListItem { Number = number, Contents = contents };
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

    internal static bool IsNumberedWithTab(WLine line)
    {
        IEnumerable<IInline> contents = line.Contents;
        return contents.Count() >= 3 && contents.First() is IFormattedText && contents.Skip(1).First() is WTab;
    }

}