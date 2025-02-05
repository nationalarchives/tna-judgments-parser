
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    /* para1 */

    internal interface Para1
    {

        public static bool IsPara1Number(string num)
        {
            string pattern = @"^\([a-z]+\)$";
            return Regex.IsMatch(num, pattern);
        }

    }

    internal class Para1Branch : Branch, Para1
    {
        public override string Name { get; internal init; } = "paragraph";
        public override string Class => "para1";

    }


    internal class Para1Leaf : Leaf, Para1
    {
        public override string Name { get; internal init; } = "paragraph";
        public override string Class => "para1";

    }

    /* para2 */

    internal interface Para2
    {

        public static bool IsPara2Number(string num)
        {
            string pattern = @"^\([ivxl]+\)$";
            return Regex.IsMatch(num, pattern);
        }

    }

    internal class Para2Branch : Branch, Para2
    {

        public override string Name { get; internal init; } = "subparagraph";

        public override string Class => "para2";

    }


    internal class Para2Leaf : Leaf, Para2
    {

        public override string Name { get; internal init; } = "subparagraph";

        public override string Class => "para2";

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
