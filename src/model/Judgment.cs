using System;
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IJudgment {

    IMetadata Metadata { get; }

    IEnumerable<IBlock> Header { get; }

    IEnumerable<IDecision> Body { get; }

    IEnumerable<IAnnex> Annexes { get; }

}

interface IDecision {

    ILine Author { get; }

    IEnumerable<IDivision> Contents { get; }

}

interface IAnnex {

    ILine Number { get; }

    IEnumerable<IBlock> Contents { get; }

}

}
