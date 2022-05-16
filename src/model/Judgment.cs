using System;
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

enum JudgmentType { Judgment, Decision }

interface IJudgment {

    JudgmentType Type { get; }

    IMetadata Metadata { get; }

    IEnumerable<IBlock> CoverPage { get; }

    IEnumerable<IBlock> Header { get; }

    IEnumerable<IDecision> Body { get; }

    IEnumerable<IBlock> Conclusions { get; }

    IEnumerable<IAnnex> Annexes { get; }

    IEnumerable<IInternalAttachment> InternalAttachments { get; }

    IEnumerable<IImage> Images { get; set; }

}

interface IDecision {

    ILine Author { get; }

    IEnumerable<IDivision> Contents { get; }

}

interface IAnnex {

    ILine Number { get; }

    IEnumerable<IBlock> Contents { get; }

}

enum AttachmentType { Annex, Order, Appendix }

interface IInternalAttachment {

    AttachmentType Type { get; }

    int Number { get; }

    IEnumerable<IBlock> Contents { get; }

    Dictionary<string, Dictionary<string, string>> CSSStyles();

    IEnumerable<IImage> Images { get; }

}

}
