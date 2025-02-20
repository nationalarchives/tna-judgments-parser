
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class Schedule : HContainer
    {

        public static bool IsScheduleNumber(string heading)
        {
            string pattern = @"^SCHEDULE \d+$";
            return Regex.IsMatch(heading, pattern);
        }

        public override string Name { get; internal init; } = "schedule";

        public override string Class => "sch";

        public IFormattedText ReferenceNote { get; internal init; }

        internal IList<IDivision> Contents { get; init; }

    }

}
