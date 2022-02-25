using System;
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IJudgment {

    IMetadata Metadata { get; }

    IEnumerable<IBlock> CoverPage { get; }

    IEnumerable<IBlock> Header { get; }

    IEnumerable<IDecision> Body { get; }

    IEnumerable<IBlock> Conclusions { get; }

    IEnumerable<IAnnex> Annexes { get; }

    IEnumerable<IInternalAttachment> InternalAttachments { get; }

    IEnumerable<IImage> Images { get; }

}

interface IDecision {

    ILine Author { get; }

    IEnumerable<IDivision> Contents { get; }

}

interface IAnnex {

    ILine Number { get; }

    IEnumerable<IBlock> Contents { get; }

}

enum AttachmentType { Annex, Order }

interface IInternalAttachment {

    AttachmentType Type { get; }

    int Number { get; }

    IEnumerable<IBlock> Contents { get; }

    Dictionary<string, Dictionary<string, string>> CSSStyles();

    IEnumerable<IImage> Images { get; }

}

}
