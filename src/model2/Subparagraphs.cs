
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments.Parse {

abstract class Branch : IBranch {

    public abstract string Name { get; }

    public IFormattedText Number { get; internal init; }

    virtual public ILine Heading{ get; internal init; }

    public IEnumerable<IBlock> Intro { get; internal init; }

    public IEnumerable<IDivision> Children { get; internal init; }

    public IEnumerable<IBlock> WrapUp { get; internal set; }

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

    internal static BranchSubparagraph Demote(BranchParagraph p) => new BranchSubparagraph {
        Number = p.Number,
        Intro = p.Intro,
        Children = p.Children
    };

}

class LeafSubparagraph : Leaf {

    override public string Name => "subparagraph";

    override public ILine Heading => null;

    // internal static LeafSubparagraph Demote(WNewNumberedParagraph p) => new LeafSubparagraph {
    //     Number = p.Number,
    //     Contents = p.Contents
    // };

}

}
