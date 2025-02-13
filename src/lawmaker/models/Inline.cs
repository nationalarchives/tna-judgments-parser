
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class ShortTitle : IInlineContainer
    {

        internal IList<IInline> Contents { get; init; }

        IEnumerable<IInline> IInlineContainer.Contents => Contents;

    }

}
