
#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

class BlockList : IBlock
{
    public IBlock? Intro { get; internal init; }

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

        IBlock? intro = null;
        if (line is not WOldNumberedParagraph) // TODO: Handle tabbed nums here
            intro = parser.Advance();

        List<IBlock> children = [];

        while (!parser.IsAtEnd())
        {
            if (BlockListItem.Parse(parser) is BlockListItem blockListItem)
                children.Add(blockListItem);
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

    public IList<IBlock> Children { get; internal init; }

    internal static BlockListItem? Parse(IParser parser)
    {
        if (parser.Match(ParseBlockListItem) is BlockListItem blockListItem)
            return blockListItem;
        return null;
    }

    private static BlockListItem? ParseBlockListItem(IParser parser)
    {
        IBlock block = parser.Advance();
        if (block is not WLine line)
            return null;

        IFormattedText? number = null;
        IList<IBlock> children = [];
        if (line is WOldNumberedParagraph np)
        {
            number = np.Number;
            children = [WLine.RemoveNumber(np)];
        }
        else if (line.Contents.Count() >= 3 && line.Contents.First() is IFormattedText ft && line.Contents.Skip(1).First() is WTab)
        {
            number = ft;
            children = [new WLine(line, line.Contents.Skip(2))];
        }
        if (children.Count == 0)
            return null;

        return new BlockListItem { Number = number, Children = children };
    }

}