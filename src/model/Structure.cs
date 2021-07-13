#nullable enable

using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface INumber : IFormattedText {

    string LeftIndent { get; }
    string FirstLineIndent { get; }

    static Dictionary<string, string> GetCSSStyles(INumber that) {
        Dictionary<string, string> styles = IFormattedText.GetCSSStyles(that);
        if (that.LeftIndent is not null)
            styles.Add("margin-left", that.LeftIndent);
        if (that.FirstLineIndent is not null)
            styles.Add("text-indent", that.FirstLineIndent);
        return styles;
    }

    Dictionary<string, string> IFormattedText.GetCSSStyles() {
        return INumber.GetCSSStyles(this);
    }

}

interface IDivision {

    IFormattedText? Number { get; }

    ILine? Heading { get; }

}

interface IBranch : IDivision {

    IEnumerable<IDivision> Children { get; }

}

interface ILeaf : IDivision {

    IEnumerable<IBlock> Contents { get; }

}

}
