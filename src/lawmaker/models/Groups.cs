
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal interface GroupOfParts
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^the (\w+) group of parts";
            return Regex.IsMatch(num, pattern, RegexOptions.IgnoreCase);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is Part)
                return true;
            return false;
        }

    }


    internal class GroupOfPartsBranch : Branch, GroupOfParts
    {
        public override string Name { get; internal init; } = "groupOfParts";

        public override string Class => "group1";

    }

    internal class GroupOfPartsLeaf : Leaf, GroupOfParts
    {
        public override string Name { get; internal init; } = "groupOfParts";

        public override string Class => "group1";

    }

    internal interface Part
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^PART [A-Z]*\d+[A-Z]*$";
            return Regex.IsMatch(num, pattern, RegexOptions.IgnoreCase);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is Chapter)
                return true;
            if (child is CrossHeading)
                return true;
            if (child is Prov1)
                return true;
            return false;
        }

    }


    internal class PartBranch : Branch, Part
    {

        public override string Name { get; internal init; } = "part";

        public override string Class => "group2";

    }

    internal class PartLeaf : Leaf, Part
    {

        public override string Name { get; internal init; } = "part";

        public override string Class => "group2";

    }

    internal interface Chapter
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^CHAPTER [A-Z]*\d+[A-Z]*$";
            return Regex.IsMatch(num, pattern, RegexOptions.IgnoreCase);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is CrossHeading)
                return true;
            if (child is Prov1)
                return true;
            return false;
        }

    }

    internal class ChapterBranch : Branch, Chapter
    {
        public override string Name { get; internal init; } = "chapter";

        public override string Class => "group4";

    }

    internal class ChapterLeaf : Leaf, Chapter
    {
        public override string Name { get; internal init; } = "chapter";

        public override string Class => "group4";

    }

    internal interface CrossHeading
    {

        public static bool IsValidChild(IDivision child)
        {
            if (child is Prov1)
                return true;
            return false;
        }

    }

    internal class CrossHeadingBranch : Branch, CrossHeading
    {

        public override string Name { get; internal init; } = "crossheading";

        public override string Class => "group7";

        public override bool HeadingPrecedesNumber => true;

    }

    internal class CrossHeadingLeaf : Leaf, CrossHeading
    {

        public override string Name { get; internal init; } = "crossheading";

        public override string Class => "group7";

        public override bool HeadingPrecedesNumber => true;

    }

    internal class GroupingSectionBranch : Branch, CrossHeading
    {

        public override string Name { get; internal init; } = "section";

        public override string Class => "group5";

        public override bool HeadingPrecedesNumber => true;

    }

    internal class GroupingSectionLeaf : Leaf, CrossHeading
    {

        public override string Name { get; internal init; } = "section";

        public override string Class => "group5";

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

    internal interface SchedulePart
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^PART [A-Z]*\d+[A-Z]*$";
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

    internal class SchedulePartBranch : Branch, SchedulePart
    {

        public override string Name { get; internal init; } = "part";

        public override string Class => "schGroup2";

    }

    internal class SchedulePartLeaf : Leaf, SchedulePart
    {

        public override string Name { get; internal init; } = "part";

        public override string Class => "schGroup2";

    }

    internal interface ScheduleChapter
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^CHAPTER [A-Z]*\d+[A-Z]*$";
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

    internal class ScheduleChapterBranch : Branch, ScheduleChapter
    {

        public override string Name { get; internal init; } = "chapter";

        public override string Class => "schGroup4";

    }

    internal class ScheduleChapterLeaf : Leaf, ScheduleChapter
    {

        public override string Name { get; internal init; } = "chapter";

        public override string Class => "schGroup4";

    }

    internal interface ScheduleCrossHeading
    {

        public static bool IsValidChild(IDivision child)
        {
            if (child is SchProv1)
                return true;
            return false;
        }

    }

    internal class ScheduleCrossHeadingBranch : Branch, ScheduleCrossHeading
    {

        public override string Name { get; internal init; } = "crossheading";

        public override string Class => "schGroup7";

        public override bool HeadingPrecedesNumber => true;

    }

    internal class ScheduleCrossHeadingLeaf : Leaf, ScheduleCrossHeading
    {

        public override string Name { get; internal init; } = "crossheading";

        public override string Class => "schGroup7";

        public override bool HeadingPrecedesNumber => true;

    }

}
