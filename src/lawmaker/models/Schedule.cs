
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using static UK.Gov.Legislation.Lawmaker.LanguageService;

namespace UK.Gov.Legislation.Lawmaker
{

    internal interface Schedule
    {

        IFormattedText ReferenceNote { get; }

        public static readonly LanguagePatterns NumberPatterns = new()
        {
            [Lang.EN] = [@"^\s*SCHEDULE\s*([A-Z]*\d+[A-Z]*)?$"],
            [Lang.CY] = [@"^\s*(YR +)?ATODLEN\s*([A-Z]*\d+[A-Z]*)?$"]
        };

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
