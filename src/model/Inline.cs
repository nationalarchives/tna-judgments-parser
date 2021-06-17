
using System;
using System.Collections.Generic;

namespace UK.Gov.Legislation.Judgments {

interface IInline { }

enum SuperSubValues { Baseline, Superscript, Subscript }

interface IFontInfo {

    string Name { get; }

    float? SizePt { get; }

}

interface IFormattedText : IInline {

    bool? Italic { get; }

    bool? Bold { get; }

    bool? Underline { get; }

    SuperSubValues? SuperSub { get; }

    string FontName { get; }

    float? FontSizePt { get; }

    string Text { get; }

    static bool HaveSameFormatting(IFormattedText fText1, IFormattedText fText2) {
        if (fText1.Italic != fText2.Italic)
            return false;
        if (fText1.Bold != fText2.Bold)
            return false;
        if (fText1.Underline != fText2.Underline)
            return false;
        if (fText1.SuperSub != fText2.SuperSub)
            return false;
        if (fText1.FontName != fText2.FontName)
            return false;
        if (fText1.FontSizePt != fText2.FontSizePt)
            return false;
        return true;
    }

    Dictionary<string, string> GetCSSStyles() {
        Dictionary<string, string> styles = new Dictionary<string, string>();
        if (this.Italic.HasValue)
            styles.Add("font-style", this.Italic.Value ? "italic" : "normal");
        if (this.Bold.HasValue)
            styles.Add("font-weight", this.Bold.Value ? "bold" : "normal");
        if (this.Underline.HasValue) {
            if (!this.Underline.Value)
                styles.Add("display", "inline-block");
            styles.Add("text-decoration", this.Underline.Value ? "underline" : "none");
        }
        if (this.SuperSub is not null) {
            string key = "vertical-align";
            string value = this.SuperSub switch {
                SuperSubValues.Superscript => "super",
                SuperSubValues.Subscript => "sub",
                SuperSubValues.Baseline => "baseline",
                _ => throw new Exception()
            };
            styles.Add(key, value);
            if (this.SuperSub == SuperSubValues.Superscript || this.SuperSub == SuperSubValues.Subscript) {
                styles.Add("font-size", "smaller");
            }
        }
        if (this.FontName is not null) {
            string value = this.FontName.Contains(" ") ? "'" + this.FontName + "'" : this.FontName;
            styles.Add("font-family", value);
        }
        if (this.FontSizePt is not null) {
            styles["font-size"] = this.FontSizePt + "pt"; // Add or replace, b/c of Super/SubScript
        }
        return styles;    
    }

}

interface IFootnote : IInline {

    string Marker { get; }

    IEnumerable<ILine> Content { get; }

}

interface IImageRef : IInline {

    string Src { get; }

}

interface INeutralCitation : IFormattedText { }

interface IDocDate : IInline {

    string Date { get; }

    IEnumerable<IFormattedText> Contents { get; }

}

interface ILineBreak : IInline { }

}
