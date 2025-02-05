
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;

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
            string pattern = @"^\(\d+[A-Z]*\)$";
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

}
