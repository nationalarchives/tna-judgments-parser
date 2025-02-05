
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class QuotedStructure : IQuotedStructure
    {

        public IList<IDivision> Contents { get; internal init; }

        public string StartQuote { get; internal init; }

        public string EndQuote { get; internal init; }

    }

}
