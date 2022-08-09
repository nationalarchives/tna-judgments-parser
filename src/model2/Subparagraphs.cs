
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class Branch : IBranch {

    public abstract string Name { get; }

    public IFormattedText Number { get; internal init; }

    virtual public ILine Heading{ get; internal init; }

    public IEnumerable<IBlock> Intro { get; internal init; }

    public IEnumerable<IDivision> Children { get; internal init; }

}

abstract class Leaf : ILeaf {

    public abstract string Name { get; }

    public IFormattedText Number { get; internal init; }

    virtual public ILine Heading { get; internal init; }

    public IEnumerable<IBlock> Contents { get; internal init; }

}

class BranchParagraph : Branch {

    override public string Name => "paragraph";

    override public ILine Heading => null;

}

class BranchSubparagraph : Branch {

    override public string Name => "subparagraph";

    override public ILine Heading => null;

}

class LeafSubparagraph : Leaf {

    override public string Name => "subparagraph";

    override public ILine Heading => null;

}

}
