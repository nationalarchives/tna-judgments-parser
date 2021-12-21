
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments {

interface IInline { }

interface IInlineContainer : IInline {

    IEnumerable<IInline> Contents { get; init; }

}

enum SuperSubValues { Baseline, Superscript, Subscript }

enum UnderlineValues2 { None, Solid, Double, Dotted, Dashed, Wavy }

interface IFontInfo {

    string Name { get; }

    float? SizePt { get; }

}

interface IFormattedText : IInline {

    string Style { get; }
    
    bool? Italic { get; }

    bool? Bold { get; }

    UnderlineValues2? Underline { get; }

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

    Dictionary<string, string> GetCSSStyles() {
        return CSS.GetCSSStyles(this);
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

    IEnumerable<IInline> Contents { get; init; }

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

enum PartyRole { Appellant, Applicant, Claimant, Defendant, Petitioner, Respondent, InterestedParty, Intervener, BeforeTheV, AfterTheV }

interface IParty : IInline {

    static string MakeId(string name) {
        string id = Regex.Replace(name, @"\s", "-");
        id = Regex.Replace(id, @"[\.\(\)“”‘’,]+", "");
        return id.ToLower();
    }

    string Text { get; }

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

interface ILocation : IFormattedText {

    string Name { get {
        string text = Regex.Replace(this.Text, @"\s+", " ").Trim();
        return text;
    } }

    string Id { get {
        string id = Regex.Replace(this.Name, @"\s", "-");
        id = Regex.Replace(id, @"[\.\(\)“”‘’,]+", "");
        return id.ToLower();
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
