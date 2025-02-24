
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class QuotedText : IInlineContainer
    {

        public string StartQuote { get; internal set; }

        public string EndQuote { get; internal set; }

        internal IList<IInline> Contents { get; init; }

        IEnumerable<IInline> IInlineContainer.Contents => Contents;

    }

}
