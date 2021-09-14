
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments {

interface IInline { }

interface IInlineContainer : IInline {

    IEnumerable<IInline> Contents { get; init; }

}

enum SuperSubValues { Baseline, Superscript, Subscript }

interface IFontInfo {

    string Name { get; }

    float? SizePt { get; }

}

interface IFormattedText : IInline {

    string Style { get; }
    
    bool? Italic { get; }

    bool? Bold { get; }

    bool? Underline { get; }

    SuperSubValues? SuperSub { get; }

    string FontName { get; }

    float? FontSizePt { get; }

    string FontColor { get; }

    string BackgroundColor { get; }

    bool IsHidden { get; }

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
        if (fText1.FontColor != fText2.FontColor)
            return false;
        if (fText1.BackgroundColor != fText2.BackgroundColor)
            return false;
        if (fText1.IsHidden != fText2.IsHidden)
            return false;
        return true;
    }

    static Dictionary<string, string> GetCSSStyles(IFormattedText that) {
        Dictionary<string, string> styles = new Dictionary<string, string>();
        if (that.Italic.HasValue)
            styles.Add("font-style", that.Italic.Value ? "italic" : "normal");
        if (that.Bold.HasValue)
            styles.Add("font-weight", that.Bold.Value ? "bold" : "normal");
        if (that.Underline.HasValue) {
            if (!that.Underline.Value)
                styles.Add("display", "inline-block");
            styles.Add("text-decoration", that.Underline.Value ? "underline" : "none");
        }
        if (that.SuperSub is not null) {
            string key = "vertical-align";
            string value = that.SuperSub switch {
                SuperSubValues.Superscript => "super",
                SuperSubValues.Subscript => "sub",
                SuperSubValues.Baseline => "baseline",
                _ => throw new Exception()
            };
            styles.Add(key, value);
            if (that.SuperSub == SuperSubValues.Superscript || that.SuperSub == SuperSubValues.Subscript) {
                styles.Add("font-size", "smaller");
            }
        }
        if (that.FontName is not null) {
            string value = DOCX.CSS.ToFontFamily(that.FontName);
            styles.Add("font-family", value);
        }
        if (that.FontSizePt is not null) {
            styles["font-size"] = that.FontSizePt + "pt"; // Add or replace, b/c of Super/SubScript
        }
        if (that.FontColor is not null) {
            string value = that.FontColor;
            if (value == "auto")
                value = "initial";
            styles.Add("color", value);
        }
        if (that.BackgroundColor is not null)
            styles.Add("background-color", that.BackgroundColor);
        return styles;    
    }

    Dictionary<string, string> GetCSSStyles() {
        return GetCSSStyles(this);
    }

}

interface IFootnote : IInline {

    string Marker { get; }

    IEnumerable<IBlock> Content { get; }

}

interface IImageRef : IInline {

    string Src { get; }

    string Style { get; }

}

interface INeutralCitation : IFormattedText { }


interface ICourtType : IInline {

    string Code { get; init; }

}
interface ICourtType1 : IFormattedText, ICourtType { }

interface ICourtType2 : ICourtType {

    IEnumerable<IFormattedText> Contents { get; init; }

}


interface ICaseNo : IFormattedText { }

interface IDate : IInline {

    string Date { get; }

    IEnumerable<IFormattedText> Contents { get; }

}

interface IDateTime : IInline {

    DateTime DateTime { get; }

    IEnumerable<IFormattedText> Contents { get; }

}

interface IDocDate : IDate { }

enum PartyRole { Appellant, Applicant, Claimant, Defendant, Petitioner, Respondent, BeforeTheV, AfterTheV }

interface IParty : IInline {

    static string GetName(string text) {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.StartsWith("Mr "))
            text = text.Substring(3);
        if (text.StartsWith("Mrs "))
            text = text.Substring(4);
        if (text.StartsWith("Miss "))
            text = text.Substring(5);
        Match match = Regex.Match(text, @"^\(\d+\) ");
        if (match.Success)
            text = text.Substring(match.Length);
        match = Regex.Match(text, @"^\(\d+\)"); // EWCA/Civ/2005/450
        if (match.Success)
            text = text.Substring(match.Length);
        match = Regex.Match(text, @" \(\d+\)$");
        if (match.Success)
            text = text.Substring(0, text.Length - match.Length);
        match = Regex.Match(text, @" \(the “[^”]+”\)$");
        if (match.Success)
            text = text.Substring(0, match.Index);
        if (text.EndsWith(" (in administration)"))
            text = text.Substring(0, text.Length - 20);
        if (text.EndsWith(" in administration"))
            text = text.Substring(0, text.Length - 18);
        if (text == "R E G I N A")
            return "REGINA";
        if (text.EndsWith(')')) {
            int i = text.LastIndexOf('(');
            if (i > 0) {
                string text2 = text.Substring(0, i).TrimEnd();
                if (!string.IsNullOrWhiteSpace(text2))
                    return text2;
            }
        }
        return text;
    }

    static string MakeId(string name) {
        string id = Regex.Replace(name, @"\s", "-");
        id = Regex.Replace(id, @"[\.\(\)“”‘’,]+", "");
        return id.ToLower();
    }

    string Name { get; }

    string Id { get {
        return IParty.MakeId(this.Name);
    } }

    PartyRole? Role { get; }

}

interface IParty1 : IFormattedText, IParty { }

interface IParty2 : IParty {

    IEnumerable<IFormattedText> Contents { get; }

}

interface IRole : IInlineContainer {

    PartyRole Role { get; }

}


interface IDocTitle : IFormattedText { }

interface IJudge : IFormattedText {

    string Name { get {
        string text = Regex.Replace(this.Text, @"\s+", " ").Trim();
        if (text.StartsWith("Employment Judge "))
            text = text.Substring(17);
        if (text.StartsWith("Judge "))
            text = text.Substring(6);
        return text;
    } }

    string Id { get {
        string id = Regex.Replace(this.Name, @"\s", "-");
        id = Regex.Replace(id, @"[\.\(\)“”‘’,]+", "");
        return "judge-" + id.ToLower();
    } }

}

interface ILawyer : IFormattedText {

    string Name { get {
        string text = Regex.Replace(this.Text, @"\s+", " ").Trim();
        if (text.StartsWith("Mr "))
            text = text.Substring(3);
        if (text.StartsWith("Mrs "))
            text = text.Substring(4);
        if (text.StartsWith("Miss "))
            text = text.Substring(5);
        return text;
    } }

    string Id { get {
        string id = Regex.Replace(this.Name, @"\s", "-");
        id = Regex.Replace(id, @"[\.\(\)“”‘’,]+", "");
        return "lawyer-" + id.ToLower();
    } }

}

interface IHyperlink1 : IFormattedText {

    string Href { get; }

    string ScreenTip { get; }

}

interface IHyperlink2 : IInline {

    string Href { get; }

    IEnumerable<IInline> Contents { get; }

    string ScreenTip { get; }

}

interface ILineBreak : IInline { }

interface ITab : IInline { }

}
