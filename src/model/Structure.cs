#nullable enable

using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IDivision {

    IFormattedText? Number { get; }

    ILine? Heading { get; }

}

interface IBranch : IDivision {

    IEnumerable<IDivision> Children { get; }

}

interface ILeaf : IDivision {

    IEnumerable<IBlock> Contents { get; }

}

}
