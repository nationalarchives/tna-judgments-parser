
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IBlock { }

enum AlignmentValues { Left, Right, Center, Justify }

interface ILine : IBlock {

    string Style { get; }

    AlignmentValues? Alignment { get; }

    string LeftIndent { get; }
    string RightIndent { get; }
    string FirstLineIndent { get; }

    IEnumerable<IInline> Contents { get; }

    Dictionary<string, string> GetCSSStyles() {
        Dictionary<string, string> styles = new Dictionary<string, string>();
        if (this.Alignment is not null)
            styles.Add("text-align", this.Alignment.ToString().ToLower());
        if (this.LeftIndent is not null)
            styles.Add("margin-left", this.LeftIndent);
        if (this.RightIndent is not null)
            styles.Add("margin-right", this.RightIndent);
        if (this.FirstLineIndent is not null)
            styles.Add("text-indent", this.FirstLineIndent);
        return styles;
    }

}

interface IOldNumberedParagraph : ILine {

    string Number { get; }

}

}
