
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    /* prov1 */

    internal enum Prov1Name { section, regulation, rule, article }

    internal interface Prov1
    {

        public static bool IsValidNumber(string num, DocName currentDocName)
        {
            // Found that Prov1 nums where the heading precedes the num must end with a "."
            string dot = currentDocName.RequireNumberedProv1Heading() ? ".?" : ".";
            string pattern = $@"^[A-Z]*\d+(?:[A-Z]+\d+)*[A-Z]*{dot}$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            return child switch {
            Prov2
            or Para1
            or Definition
            or UnnumberedParagraph
            or WDummyDivision
                => true,
            _   => false,
            }
            ;
        }

    }

    internal class Prov1Branch : Branch, Prov1
    {
        public required Prov1Name TagName;

        public override string Name 
        { 
            get => TagName.ToString();
            internal init => TagName.ToString();
        } 

        public override string Class => "prov1";

    }

    internal class Prov1Leaf : Leaf, Prov1
    {

        public required Prov1Name TagName;

        public override string Name
        {
            get => TagName.ToString();
            internal init => TagName.ToString();
        }

        public override string Class => "prov1";

    }


    /* schProv1 */

    internal interface SchProv1
    {

        public static bool IsValidNumber(string num, DocName currentDocName)
        {
            // For certain doc names, the num must end with a "."
            string dot = currentDocName.RequireNumberedProv1Heading() ? ".?" : ".";
            string pattern = $@"^[A-Z]*\d+(?:[A-Z]+\d+)*[A-Z]*{dot}$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            return child switch {

            SchProv2
            or Para1
            or Definition
            or UnnumberedParagraph
            or WDummyDivision
                => true,
            _   => false,
            };
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

    internal enum Prov2Name { subsection, paragraph }

    internal interface Prov2 : IDivision
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^\([A-Z]*\d+(?:[A-Z]+\d+)*[A-Z]*\)$";
            return Regex.IsMatch(num, pattern);
        }

        // Identifies when a Prov2 num starts with an em-dash and must therefore be the first child of a Prov1.
        // Note that the num of the first Prov2 is not necessarily (1). It could be (A1), for example.
        public static bool IsFirstProv2Start(string text)
        {
            string pattern = @"^\s*\u2014\([A-Z]*\d+(?:[A-Z]+\d+)*[A-Z]*\).*";
            return Regex.IsMatch(text, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            return child switch {
            Para1
            or Definition
            or UnnumberedParagraph
            or WDummyDivision
                => true,
            _   => false,
            };

        }

    }

    internal class Prov2Branch : Branch, Prov2
    {

        public required Prov2Name TagName;

        public override string Name
        {
            get => TagName.ToString();
            internal init => TagName.ToString();
        }

        public override string Class => "prov2";

    }

    internal class Prov2Leaf : Leaf, Prov2
    {

        public required Prov2Name TagName;

        public override string Name
        {
            get => TagName.ToString();
            internal init => TagName.ToString();
        }

        public override string Class => "prov2";

    }


    /* schProv2 */

    internal interface SchProv2 : IDivision
    {

        public static bool IsValidNumber(string num)
        {
            string pattern = @"^\([A-Z]*\d+(?:[A-Z]+\d+)*[A-Z]*\)$";
            return Regex.IsMatch(num, pattern);
        }

        public static bool IsValidChild(IDivision child)
        {
            if (child is Para1)
                return true;
            if (child is Definition)
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
