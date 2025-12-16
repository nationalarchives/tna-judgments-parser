
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    public class Document
    {

        public DocName Type { get; init; }

        internal Dictionary<string, Dictionary<string, string>> Styles { get; init; }

        public required Metadata Metadata { get; init; }

        internal IHeader Header { get; init; }

        internal IList<IDivision> Body { get; init; }

        internal IList<Schedule> Schedules { get; init; }
        internal IList<BlockContainer> Conclusions { get; init; }

    }
}