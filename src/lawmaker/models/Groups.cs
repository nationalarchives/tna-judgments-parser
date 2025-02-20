
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class Part : Branch
    {

        public static bool IsPartNumber(string num)
        {
            string pattern = @"^PART \d+$";
            return Regex.IsMatch(num, pattern);
        }

        public override string Name { get; internal init; } = "part";

        public override string Class => "group2";

    }

    internal class CrossHeading : Branch
    {

        public override string Name { get; internal init; } = "crossheading";

        public override string Class => "group7";

    }

    internal class Schedules : Branch
    {

        public static bool IsSchedulesHeading(string heading)
        {
            string pattern = @"^SCHEDULES$";
            return Regex.IsMatch(heading, pattern, RegexOptions.IgnoreCase);
        }

        public override string Name { get; internal init; } = "schedules";

        public override string Class => "schs";

    }


}
