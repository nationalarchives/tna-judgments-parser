using System;
using System.Collections.Generic;

using NationalArchives.FindCaseLaw.Utils.Metadata;

namespace UK.Gov.Legislation.Judgments;

internal enum JudgmentType { Judgment, Decision }

internal interface IJudgment : IDisposable
{
    JudgmentType Type { get; }

    IMetadata Metadata { get; }

    IEnumerable<IBlock> CoverPage { get; }

    IEnumerable<IBlock> Header { get; }

    IList<IDecision> Body { get; }

    IEnumerable<IBlock> Conclusions { get; }

    IEnumerable<IAnnex> Annexes { get; }

    IEnumerable<IInternalAttachment> InternalAttachments { get; }

    IEnumerable<IImage> Images { get; set; }

    IMetadataField[] StructuredMetadataFields { get; }
}

internal interface IDecision
{
    ILine Author { get; }

    IEnumerable<IDivision> Contents { get; }
}

internal interface IAnnex
{
    ILine Number { get; }

    IEnumerable<IBlock> Contents { get; }
}

internal enum AttachmentType { Annex, Order, Appendix }

internal interface IInternalAttachment
{
    AttachmentType Type { get; }

    int Number { get; }

    IEnumerable<IBlock> Contents { get; }

    Dictionary<string, Dictionary<string, string>> CSSStyles();

    IEnumerable<IImage> Images { get; }
}
