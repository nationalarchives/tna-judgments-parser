
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives {

internal class TableOfContents : ITableOfContents2 {

    public IEnumerable<ILine> Contents { get; init; }

}

internal class QuotedStructure : IQuotedStructure {

    public List<IDivision> Contents { get; init; }

    IList<IDivision> IQuotedStructure.Contents { get => Contents; }

}

}
