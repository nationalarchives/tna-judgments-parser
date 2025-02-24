
namespace UK.Gov.Legislation.Lawmaker
{

    internal interface Definition { }

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