
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    internal interface Definition 
    {
        public static bool IsValidChild(IDivision child)
        {
            return child switch
            {
                Para1 or UnnumberedParagraph or WDummyDivision => true,
                _ => false,
            };
        }
    }

    internal class DefinitionBranch : Branch, Definition
    {

        public override string Name { get; internal init; } = "definition";

        public override string Class => "definition";

    }

    internal class DefinitionLeaf : Leaf, Definition
    {

        public override string Name { get; internal init; } = "definition";

        public override string Class => "definition";

    }

}