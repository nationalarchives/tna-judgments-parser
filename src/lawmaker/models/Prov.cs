
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    /* prov1 */

    internal interface Prov1
    {

        public static bool IsSectionNumber(string num)
        {
            string pattern = @"^\d+[A-Z]*\.$";
            return Regex.IsMatch(num, pattern);
        }

    }

    internal class Prov1Branch : Branch, Prov1
    {

        public override string Name { get; internal init; } = "section";

        public override string Class => "prov1";

        public override bool HeadingPrecedesNumber => true;

    }

    internal class Prov1Leaf : Leaf, Prov1
    {

        public override string Name { get; internal init; } = "section";

        public override string Class => "prov1";

        public override bool HeadingPrecedesNumber => true;

    }


    /* schProv1 */

    internal interface SchProv1
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^\d+[A-Z]*\.$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is SchProv2)
                return true;
            if (child is Para1)
                return true;
            if (child is UnnumberedParagraph)
                return true;
            if (child is WDummyDivision)
                return true;
            return false;
        }

    }

    internal class SchProv1Branch : Branch, SchProv1
    {

        public override string Name { get; internal init; } = "paragraph";

        public override string Class => "schProv1";

    }

    internal class SchProv1Leaf : Leaf, SchProv1
    {

        public override string Name { get; internal init; } = "paragraph";

        public override string Class => "schProv1";

    }

    /* prov2 */

    internal interface Prov2 : IDivision
    {

        public static bool IsProv2Number(string num)
        {
            string pattern = @"^\(\d+\)$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsQuotedProv2Number(string num)
        {
            string pattern = @"^“?\(\d+[A-Z]*\)$";
            return Regex.IsMatch(num, pattern);
        }

    }

    internal class Prov2Branch : Branch, Prov2
    {

        public override string Name { get; internal init; } = "subsection";

        public override string Class => "prov2";

    }

    internal class Prov2Leaf : Leaf, Prov2
    {

        public override string Name { get; internal init; } = "subsection";

        public override string Class => "prov2";

    }


    /* schProv2 */

    internal interface SchProv2 : IDivision
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^\(\d+\)$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsQuotedSchProv2Number(string num)
        {
            string pattern = @"^\(\d+[A-Z]*\)$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is Para1)
                return true;
            if (child is UnnumberedParagraph)
                return true;
            if (child is WDummyDivision)
                return true;
            return false;
        }

    }

    internal class SchProv2Branch : Branch, SchProv2
    {

        public override string Name { get; internal init; } = "subparagraph";

        public override string Class => "schProv2";

    }

    internal class SchProv2Leaf : Leaf, SchProv2
    {

        public override string Name { get; internal init; } = "subparagraph";

        public override string Class => "schProv2";

    }

}
