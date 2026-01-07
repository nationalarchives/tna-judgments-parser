
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    /* para1 */

    internal partial interface Para1
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^\([a-z]+\)$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is Para2)
                return true;
            //if (child is Definition)
            //    return true;
            if (child is UnnumberedParagraph)
                return true;
            if (child is WDummyDivision)
                return true;
            return false;
        }

    }

    internal class Para1Branch : Branch, Para1
    {
        public override string Name { get; internal init; } = "level";

        public override string Class => "para1";

    }


    internal class Para1Leaf : Leaf, Para1
    {
        public override string Name { get; internal init; } = "level";

        public override string Class => "para1";

    }

    /* para2 */

    internal interface Para2
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^\(z*[ivxl]+[a-z]{0,3}\)$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            return child switch {
            Para3
            or WDummyDivision
            or UnnumberedParagraph
            //or Definition
                => true,
            _   => false,
            };
        }

    }

    internal class Para2Branch : Branch, Para2
    {

        public override string Name { get; internal init; } = "level";

        public override string Class => "para2";

    }


    internal class Para2Leaf : Leaf, Para2
    {

        public override string Name { get; internal init; } = "level";

        public override string Class => "para2";

    }

    /* para3 */

    internal interface Para3
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^\([A-Z]+\)$";
            return Regex.IsMatch(num, pattern);
        }

    }

    internal class Para3Branch : Branch, Para2
    {

        public override string Name { get; internal init; } = "level";

        public override string Class => "para3";

    }


    internal class Para3Leaf : Leaf, Para2
    {

        public override string Name { get; internal init; } = "level";

        public override string Class => "para3";

    }

    /* unnumbered paragraphs */

    internal interface UnnumberedParagraph : IDivision { }

    internal class UnnumberedBranch : Branch, UnnumberedParagraph
    {

        public override string Name { get; internal init; } = "level";

        public override string Class => "unnumberedParagraph";

    }

    internal class UnnumberedLeaf : Leaf, UnnumberedParagraph
    {

        public override string Name { get; internal init; } = "level";

        public override string Class => "unnumberedParagraph";

    }

    /* unknown */

    internal class UnknownLevel : Leaf
    {

        public override string Name { get; internal init; } = "level";

        public override string Class => null;

    }

}
