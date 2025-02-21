
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class BlockQuotedStructure : IQuotedStructure
    {

        public IList<IDivision> Contents { get; internal init; }

        public string StartQuote { get; internal set; }

        public string EndQuote { get; internal set; }

        public AppendText AppendText { get; internal set; }

    }

}
