
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using Judgments = UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Model {

interface IParagraph : Judgments.IDivision { }

interface IBranchParagraph : IParagraph, Judgments.IBranch { }

interface ILeafParagraph : IParagraph, Judgments.ILeaf { }

interface ISubparagraph : Judgments.ILeaf { }

abstract class AbstractParagraph : IParagraph {

    public IFormattedText Number { get; }

    public ILine Heading { get; }

}

class BranchParagraph : IBranchParagraph {

    public IFormattedText Number { get; internal init; }

    public ILine Heading { get; internal init; }

    public IEnumerable<Judgments.IDivision> Children { get; internal init; }

}

class Subparagraph : ISubparagraph {

    public IFormattedText Number { get; internal init; }

    public ILine Heading { get; internal init; }

    public IEnumerable<Judgments.IBlock> Contents { get; internal init; }

}

}
