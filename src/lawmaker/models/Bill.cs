
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    public abstract class Bill
    {

        public abstract string Type { get; protected init; }

        internal Dictionary<string, Dictionary<string, string>> Styles { get; init; }

        internal IList<IBlock> CoverPage { get; init; }

        internal IList<IBlock> Preface { get; init; }

        internal IList<IBlock> Preamble { get; init; }

        internal IList<IDivision> Body { get; init; }

        internal IList<Schedule> Schedules { get; init; }

    }

    public class NIPublicBill : Bill
    {

        public override string Type { get; protected init; } = "nipbb";

    }

}
