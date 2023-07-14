
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.NationalArchives {

internal class TableOfContents : IBlock, ITableOfContents2 {

    public IEnumerable<ILine> Contents { get; init; }

}

}
