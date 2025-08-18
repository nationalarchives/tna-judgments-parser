
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class BlockList : IBlock
    {
        public IBlock Intro { get; internal init; }

        public IList<IBlock> Children { get; internal init; }

        public string Name { get; internal init; } = "blockList";

    }

    internal interface IBlockListItem : IBlock
    {

    }

    internal class BlockListItem : IBlockListItem
    {
        public virtual IFormattedText Number { get; internal set; }

        public IList<IBlock> Children { get; internal init; }

        public string Name { get; internal init; } = "item";


    }

}
