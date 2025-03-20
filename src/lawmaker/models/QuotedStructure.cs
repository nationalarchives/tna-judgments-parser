
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class BlockQuotedStructure : HContainer, IQuotedStructure
    {

        public override string Name { get; internal init; } = "quotedStructure";

        public override string Class => null;

        public IList<IDivision> Contents { get; internal init; }

        public string StartQuote { get; internal set; }

        public string EndQuote { get; internal set; }

        public string Context { get; internal set; }

        public AppendText AppendText { get; internal set; }

    }

}
