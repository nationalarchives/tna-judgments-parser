using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    interface IBlockContainer
    {

        string Class { get; }

    }

    abstract class BlockContainer : IBlockContainer
    {

        public abstract string Name { get; internal init; }

        public abstract string Class { get; internal init;}

        public virtual ILine Heading { get; internal set; }
        
        public virtual ILine Subheading { get; internal set; }
        
        public virtual IEnumerable<IBlock> Blocks { get; internal set; }

    }

}
