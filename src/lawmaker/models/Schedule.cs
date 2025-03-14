
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class Schedule : Branch
    {

        public override string Name { get; internal init; } = "schedule";

        public override string Class => "sch";

        public IFormattedText ReferenceNote { get; internal init; }

        public static bool IsValidNumber(string number)
        {
            string pattern = @"^SCHEDULE \d+$";
            return Regex.IsMatch(number, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is SchedulePart)
                return true;
            if (child is ScheduleCrossHeading)
                return true;
            if (child is SchProv1)
                return true;
            if (child is WDummyDivision)
                return true;
            return false;
        }

    }

}
