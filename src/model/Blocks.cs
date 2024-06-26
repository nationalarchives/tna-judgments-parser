
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments {

interface IBlock {

    bool IsEmptyLine() {
        if (this is not ILine line)
            return false;
        return line.IsEmpty();
    }

}

enum AlignmentValues { Left, Right, Center, Justify }

interface ILine : IBlock, IBordered {

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
        CSS.AddBorderStyles(this, styles);
        return styles;
    }

    [System.Obsolete]
    static string TextContent(IEnumerable<IInline> contents) {
        IEnumerable<string> texts = contents
            .Select(i => {
                if (i is IFormattedText t)
                    return (t.Uppercase.HasValue && t.Uppercase.Value) ? t.Text.ToUpper() : t.Text;
                if (i is ITab)
                    return " ";
                return "";
            });
        return string.Join("", texts);
    }

    [System.Obsolete]
    string TextContent() {
        return TextContent(this.Contents);
    }

    [System.Obsolete]
    static string NormalizeContent(ILine line) {
        string text = line.TextContent();
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
    [System.Obsolete]
    string NormalizedContent() {
        return NormalizeContent(this);
    }

    bool IsEmpty() {
        if (!this.Contents.Any())
            return true;
        return this.Contents.All(IInline.IsEmpty);
    }

}

interface IRestriction : ILine { }

interface IOldNumberedParagraph : ILine {

    IFormattedText Number { get; }

}

interface ITableOfContents2 : IBlock {

    IEnumerable<ILine> Contents { get; }

}

interface IQuotedStructure : IBlock {

    IList<IDivision> Contents { get; }

}

}
