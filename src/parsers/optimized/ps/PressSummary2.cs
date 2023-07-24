
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives.CaseLaw.PressSummaries {

class PressSummary2 : IAknDocument2 {

    public DocType Type => DocType.PressSummary;

    public PSMetadata Metadata { get; internal init; }

    IAknMetadata IAknDocument2.Metadata { get => Metadata; }

    public IEnumerable<IBlock> Preface { get; internal init; }

    public IEnumerable<IDivision> Body { get; internal init; }

    public IEnumerable<IImage> Images { get; internal init; }

}

}
