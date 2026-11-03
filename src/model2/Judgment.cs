using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse;

internal class Judgment : IJudgment
{
    private readonly WordprocessingDocument doc;

    public Judgment(WordprocessingDocument doc, IOutsideMetadata meta = null, bool metadataTrumps = true)
    {
        this.doc = doc;
        InternalMetadata = new WMetadata(doc.MainDocumentPart, this);

        if (meta is null)
        {
            Metadata = new WMetadata(doc.MainDocumentPart, this);
        }
        else if (metadataTrumps)
        {
            Metadata = new WMetadata3(doc.MainDocumentPart, this, meta);
        }
        else
        {
            Metadata = new WMetadata2(doc.MainDocumentPart, this, meta);
        }
    }

    public JudgmentType Type { get; init; } = JudgmentType.Judgment;

    public IMetadata Metadata { get; }

    internal IMetadata InternalMetadata { get; private init; }

    public IEnumerable<IBlock> CoverPage { get; internal set; }

    public IEnumerable<IBlock> Header { get; internal set; }

    public IList<IDecision> Body { get; internal set; }

    public IEnumerable<IBlock> Conclusions { get; internal set; }

    public IEnumerable<IAnnex> Annexes { get; internal set; }

    public IEnumerable<IInternalAttachment> InternalAttachments { get; internal init; }

    public IEnumerable<IImage> Images
    {
        get
        {
            field ??= WImage.Get(doc);

            return field;
        }
        set;
    }

    public void Dispose()
    {
        doc.Dispose();
    }
}

internal class Decision : IDecision
{
    public ILine Author { get; internal set; }

    public IEnumerable<IDivision> Contents { get; internal set; }
}

internal class Annex : IAnnex
{
    public ILine Number { get; internal set; }

    public IEnumerable<IBlock> Contents { get; internal set; }
}
