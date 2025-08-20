
#nullable enable
namespace UK.Gov.Legislation.Lawmaker;

using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

class BlockList : IBlock
{
    public IBlock Intro { get; internal init; }

    public IList<IBlock>? Children { get; internal init; }

    public string Name { get; internal init; } = "blockList";


    internal static BlockList? Parse(IParser parser)
    {
        if (parser.Match(ParseBlockList) is BlockList blockList)
        {
            return blockList;
        }
        return null;
    }

    private static BlockList? ParseBlockList(IParser parser)
    {
        IBlock block = parser.Advance();
        if (block is not WLine line)
            return null;

        // TODO: Handle listIntroduction
        WLine introLine = (line is WOldNumberedParagraph np) ? WLine.RemoveNumber(np) : line;
        return new BlockList { Intro = introLine };


        /*
        List<IBlock> children = [];
        while (i < Document.Body.Count)
        {
            if (BreakFromProv1())
                break;

            int save = i;
            BlockListItem next = ParseBlockListItem(Current());
            if (next == null)
            {
                i = save;
                break;
            }
            children.Add(next);

            if (IsEndOfQuotedStructure([next]))
                break;
        }*/


    }

}



/*

    if (children.Count == 0)
        return null;
    return new BlockList { Intro = introLine, Children = children };
}
private static BlockListItem? ParseBlockListItem(IEnumerable<IBlock> blocks, int j)
{
    if (!(block is WLine line))
        return null;

    int finalChildStart = i;
    while (i < Document.Body.Count)
    {
        if (BreakFromProv1())
            break;
    }
    return null;
}
*/