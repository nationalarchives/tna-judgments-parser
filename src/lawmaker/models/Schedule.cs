
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class Schedule : HContainer
    {

        public override string Name { get; internal init; } = "schedule";

        public override string Class => "schedule";

        internal IList<IDivision> Contents { get; init; }

    }

}
