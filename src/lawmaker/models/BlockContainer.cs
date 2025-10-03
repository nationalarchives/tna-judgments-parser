#nullable enable

using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    abstract class BlockContainer : IBlock
    {

        public abstract string Name { get; internal init; }

        public abstract string Class { get; internal init; }

        public virtual required ILine Heading { get; internal set; }

        public virtual ILine? Subheading { get; internal set; }

        public virtual required IEnumerable<IBlock> Content { get; internal set; }

    }

    internal class HeadingTblock : BlockContainer
    {
        
        public override string Name { get; internal init; } = "tblock";

        public override string Class { get; internal init; } = "group1";

    }
}
