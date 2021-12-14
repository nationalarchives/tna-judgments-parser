
using System.Collections.Generic;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse {

class Judgment : UK.Gov.Legislation.Judgments.IJudgment {

    private readonly WordprocessingDocument doc;

    public Judgment(WordprocessingDocument doc, IOutsideMetadata meta = null) {
        this.doc = doc;
        // WordprocessingDocumentType type = doc.DocumentType;
        if (meta is null)
            Metadata = new WMetadata(doc.MainDocumentPart, this);
        else
            Metadata = new WMetadata2(doc.MainDocumentPart, this, meta);
    }

    public IMetadata Metadata { get; }

    public IEnumerable<IBlock> CoverPage { get; internal set; }

    public IEnumerable<IBlock> Header { get; internal set; }

    public IEnumerable<IDecision> Body { get; internal set; }

    public IEnumerable<IBlock> Conclusions { get; internal set; }

    public IEnumerable<IAnnex> Annexes { get; internal set; }

    public IEnumerable<IInternalAttachment> InternalAttachments { get; internal init; }

    public IEnumerable<IImage> Images { get => WImage.Get(doc); }

    public void Close() {
        doc.Close();
    }

}

class Decision : IDecision {

    public ILine Author { get; internal set; }

    public IEnumerable<IDivision> Contents { get; internal set; }

}

class Annex : IAnnex {

    public ILine Number { get; internal set; }

    public IEnumerable<IBlock> Contents { get; internal set; }

}

}
