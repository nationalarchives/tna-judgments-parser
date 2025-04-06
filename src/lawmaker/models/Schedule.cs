
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    internal interface Schedule
    {

        IFormattedText ReferenceNote { get; }

        public static bool IsValidNumber(string number)
        {
            string pattern = @"^\s*SCHEDULE\s*([A-Z]*\d+[A-Z]*)?$";
            return Regex.IsMatch(number, pattern, RegexOptions.IgnoreCase);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is SchedulePart)
                return true;
            if (child is ScheduleCrossHeading)
                return true;
            if (child is SchProv1)
                return true;
            if (child is UnnumberedLeaf)
                return true;
            if (child is WDummyDivision)
                return true;
            return false;
        }

    }

    internal class ScheduleBranch : Branch, Schedule
    {

        public override string Name { get; internal init; } = "schedule";

        public override string Class => "sch";

        public IFormattedText ReferenceNote { get; internal init; }

        IFormattedText Schedule.ReferenceNote => ReferenceNote;

    }

    internal class ScheduleLeaf : Leaf, Schedule
    {

        public override string Name { get; internal init; } = "schedule";

        public override string Class => "sch";

        public IFormattedText ReferenceNote { get; internal init; }

        IFormattedText Schedule.ReferenceNote => ReferenceNote;
    }

}
