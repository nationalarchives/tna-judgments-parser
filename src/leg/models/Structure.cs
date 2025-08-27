
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Models {


internal class Section : IBranch {

    public string Name => "section";

    public IFormattedText Number { get; internal init; }

    public ILine Heading { get; internal init; }

    public IEnumerable<IBlock> Intro => null;

    public IEnumerable<IDivision> Children { get; internal set; }

    public IEnumerable<IBlock> WrapUp => null;

}

internal class Subheading : IBranch {

    public string Name => null;

    public IFormattedText Number => null;

    public ILine Heading { get; internal init; }

    public IEnumerable<IBlock> Intro => null;

    public IEnumerable<IDivision> Children { get; internal set; }

    public IEnumerable<IBlock> WrapUp => null;

}

interface IParagraph : Judgments.IDivision { }

interface IBranchParagraph : IParagraph, Judgments.IBranch { }

interface ILeafParagraph : IParagraph, Judgments.ILeaf { }

interface ISubparagraph : Judgments.ILeaf { }

abstract class AbstractParagraph : IParagraph {

    public string Name => "paragraph";

    public IFormattedText Number { get; }

    public ILine Heading { get; }

}

class BranchParagraph : IBranchParagraph {

    public string Name => "paragraph";

    public IFormattedText Number { get; internal init; }

    public ILine Heading { get; internal init; }

    public IEnumerable<IBlock> Intro => null;

    public IEnumerable<Judgments.IDivision> Children { get; internal init; }

    public IEnumerable<IBlock> WrapUp => null;

}

class Subparagraph : ISubparagraph {

    public string Name => "subparagraph";

    public IFormattedText Number { get; internal init; }

    public ILine Heading { get; internal init; }

    public IEnumerable<Judgments.IBlock> Contents { get; internal init; }

}

}
