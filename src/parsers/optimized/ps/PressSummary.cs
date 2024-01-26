
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw.PressSummaries {

class PressSummary : IAknDocument {

    public DocType Type => DocType.PressSummary;

    public Metadata InternalMetadata { get; internal init; }

    public IOutsideMetadata ExternalMetadata { get; internal init; }

    IAknMetadata IAknDocument.Metadata => ExternalMetadata is null ? InternalMetadata : new CombinedMetadata(InternalMetadata, ExternalMetadata);

    public IEnumerable<IBlock> Preface { get; internal init; }

    public IEnumerable<IDivision> Body { get; internal init; }

    public IEnumerable<IImage> Images { get; internal set; }  // setter required by ImageConverter

}

}
