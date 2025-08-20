
#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System.Collections.Generic;
using System.Linq;
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

        WLine leader = intro;
        List<IBlock> children = [];

        while (!parser.IsAtEnd())
        {
            IBlock currentBlock = parser.Current();
            if (currentBlock is not WLine currentLine) break;

            if (leader != null)
            {
                float leaderIndent = OptimizedParser.GetEffectiveIndent(leader);
                float currentIndent = OptimizedParser.GetEffectiveIndent(currentLine);
                if (currentIndent <= leaderIndent - 0.2f) break;
            }

            if (BlockListItem.Parse(parser) is BlockListItem blockListItem)
            {
                if (children.Count == 0) leader = blockListItem.Contents.First() as WLine;
                children.Add(blockListItem);

            }
            else
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

    public IList<IBlock> Contents { get; internal init; }

    internal static BlockListItem? Parse(IParser parser)
    {
        if (parser.Match(ParseBlockListItem) is BlockListItem blockListItem)
            return blockListItem;
        return null;
    }

    private static BlockListItem? ParseBlockListItem(IParser parser)
    {
        IFormattedText? number = null;
        List<IBlock> contents = [];

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

        if (contents.Count == 0)
            return null;

        return new BlockListItem { Number = number, Contents = contents };
    }

    internal static bool IsNumberedWithTab(WLine line)
    {
        IEnumerable<IInline> contents = line.Contents;
        return contents.Count() >= 3 && contents.First() is IFormattedText && contents.Skip(1).First() is WTab;
    }

    internal float GetEffectiveIndent()
    {
        return OptimizedParser.GetEffectiveIndent(Contents.First() as WLine);
    }

}