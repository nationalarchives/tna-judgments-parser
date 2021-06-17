
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

namespace UK.Gov.Legislation.Judgments.Parse {

class Judgment : UK.Gov.Legislation.Judgments.IJudgment {

    private readonly WordprocessingDocument doc;

    public Judgment(WordprocessingDocument doc) {
        this.doc = doc;
        Metadata = new WMetadata(doc.MainDocumentPart, this);
    }

    public IMetadata Metadata { get; }

    public IEnumerable<IBlock> Header { get; internal set; }

    public IEnumerable<IDecision> Body { get; internal set; }

    public IEnumerable<IAnnex> Annexes { get; internal set; }

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
