#nullable enable

using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface INumber : IFormattedText {

    float? LeftIndentInches { get; }
    string LeftIndent { get; }
    string FirstLineIndent { get; }

    static void Alt(INumber that) {
        var x = 2f;
        var li = that.LeftIndentInches ?? 0f;
        float width = x + li - 0.125f;
        Dictionary<string, string> styles = CSS.GetCSSStyles(that);
        styles.Add("margin-left", (-x).ToString("F0") + "in");
        styles.Add("width", width.ToString("F3") + "in");
        styles.Add("text-align", "right");
    }

    static Dictionary<string, string> GetCSSStyles(INumber that) {
        Dictionary<string, string> styles = CSS.GetCSSStyles(that);
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

    string Name { get; }

    IFormattedText? Number { get; }

    ILine? Heading { get; }

}

interface IBranch : IDivision {

    IEnumerable<IBlock> Intro { get; }

    IEnumerable<IDivision> Children { get; }

}

interface ILeaf : IDivision {

    IEnumerable<IBlock> Contents { get; }

}

interface ITableOfContents : IDivision {

    IEnumerable<ILine> Contents { get; }

}

interface IBlockWrapper : IBlock {

    IDivision Division { get; }

}

}
