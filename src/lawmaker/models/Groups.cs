
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal class GroupOfParts : Branch
    {
        public override string Name { get; internal init; } = "groupOfParts";
        public override string Class => "group1";
        public static bool IsGroupOfPartsHeading(string num)
        {
            string pattern = @"^the (\w+) group of parts";
            return Regex.IsMatch(num, pattern, RegexOptions.IgnoreCase);
        }
    }

    internal class Part : Branch
    {

        public static bool IsPartNumber(string num)
        {
            string pattern = @"^PART \d+$";
            return Regex.IsMatch(num, pattern, RegexOptions.IgnoreCase);
        }

        public override string Name { get; internal init; } = "part";

        public override string Class => "group2";

    }

    internal class Chapter : Branch
    {

        public static bool IsChapterNumber(string num)
        {
            string pattern = @"^CHAPTER \d+$";
            return Regex.IsMatch(num, pattern, RegexOptions.IgnoreCase);
        }

        public override string Name { get; internal init; } = "chapter";

        public override string Class => "group4";

    }

    internal class CrossHeading : Branch
    {

        public override string Name { get; internal init; } = "crossheading";

        public override string Class => "group7";

        public override bool HeadingPrecedesNumber => true;

    }

    internal class Schedules : Branch
    {

        public override string Name { get; internal init; } = "schedules";

        public override string Class => "schs";

        public override bool HeadingPrecedesNumber => true;

        public static bool IsValidHeading(string heading)
        {
            string pattern = @"^SCHEDULES$";
            return Regex.IsMatch(heading, pattern, RegexOptions.IgnoreCase);
        }

        public static bool IsValidChild(IDivision child)
        {
            // Upon entering the Schedules container, all following elements should be within
            return true;
        }

    }

    internal class SchedulePart : Branch
    {

        public override string Name { get; internal init; } = "part";

        public override string Class => "schGroup2";

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^PART \d+$";
            return Regex.IsMatch(num, pattern, RegexOptions.IgnoreCase);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is ScheduleChapter)
                return true;
            if (child is ScheduleCrossHeading)
                return true;
            if (child is SchProv1)
                return true;
            return false;
        }

    }

    internal class ScheduleChapter : Branch
    {

        public override string Name { get; internal init; } = "chapter";

        public override string Class => "schGroup4";

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^CHAPTER \d+$";
            return Regex.IsMatch(num, pattern, RegexOptions.IgnoreCase);
        }
        public static bool IsValidChild(IDivision child)
        {
            if (child is ScheduleCrossHeading)
                return true;
            if (child is SchProv1)
                return true;
            return false;
        }

    }

    internal class ScheduleCrossHeading : Branch
    {

        public override string Name { get; internal init; } = "crossheading";

        public override string Class => "schGroup7";

        public static bool IsValidChild(IDivision child)
        {
            if (child is SchProv1)
                return true;
            return false;
        }

    }

}
