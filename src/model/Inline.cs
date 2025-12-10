
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

using Imaging = UK.Gov.NationalArchives.Imaging;

namespace UK.Gov.Legislation.Judgments {

interface IInline {

    static bool IsEmpty(IInline i) {
        if (i is IFormattedText t)
            return string.IsNullOrWhiteSpace(t.Text);
        if (i is ITab)
            return true;
        if (i is ILineBreak)
            return true;
        if (i is IInlineContainer container)
            return container.Contents.All(IInline.IsEmpty);
        if (i is INeutralCitation2 ncn)
            return ncn.Contents.All(IInline.IsEmpty);
        if (i is ICourtType2 court)
            return court.Contents.All(IInline.IsEmpty);
        if (i is IDate date)
            return date.Contents.All(IInline.IsEmpty);
        if (i is IDateTime time)
            return time.Contents.All(IInline.IsEmpty);
        if (i is IParty2 party)
            return party.Contents.All(IInline.IsEmpty);
        return false;
    }

    static string GetText(IInline i) {
        if (i is IFormattedText t)
            return t.Text;
        if (i is ITab)
            return "\t";
        if (i is ILineBreak)
            return "";
        if (i is IInlineContainer container)
            return string.Join("", container.Contents.Select(GetText));
        if (i is INeutralCitation2 ncn)
            return string.Join("", ncn.Contents.Select(GetText));
        if (i is ICourtType2 court)
            return string.Join("", court.Contents.Select(GetText));
        if (i is IDate date)
            return string.Join("", date.Contents.Select(GetText));
        if (i is IDateTime time)
            return string.Join("", time.Contents.Select(GetText));
        if (i is IParty2 party)
            return string.Join("", party.Contents.Select(GetText));
        if (i is IInvalidRef) {
            return "";
        }
        return "";
    }

    static string ToString(IEnumerable<IInline> inlines, string separator = "") {
        return string.Join(separator, inlines.Select(GetText));
    }

}

interface IInlineContainer : IInline {

    IEnumerable<IInline> Contents { get; }

}

interface ITextOrWhitespace : IInline { }

enum SuperSubValues { Baseline, Superscript, Subscript }

enum UnderlineValues2 { None, Solid, Double, Dotted, Dashed, Wavy }

enum StrikethroughValue { None, Single, Double }

interface IFontInfo {

    string Name { get; }

    float? SizePt { get; }

}

interface IFormattedText : ITextOrWhitespace {

    string Style { get; }

    bool? Italic { get; }

    bool? Bold { get; }

    UnderlineValues2? Underline { get; }

    bool? Uppercase { get; }

    StrikethroughValue? Strikethrough { get; }

    bool? SmallCaps { get; }

    SuperSubValues? SuperSub { get; }

    string FontName { get; }

    float? FontSizePt { get; }

    string FontColor { get; }

    string BackgroundColor { get; }

    bool IsHidden { get; }

    string Text { get; }

    static bool HaveSameFormatting(IFormattedText fText1, IFormattedText fText2) {
        if (fText1.Style != fText2.Style)
            return false;
        if (fText1.Italic != fText2.Italic)
            return false;
        if (fText1.Bold != fText2.Bold)
            return false;
        if (fText1.Underline != fText2.Underline)
            return false;
        if (fText1.Uppercase != fText2.Uppercase)
            return false;
        if (fText1.Strikethrough != fText2.Strikethrough)
            return false;
        if (fText1.SmallCaps != fText2.SmallCaps)
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

    Dictionary<string, string> GetCSSStyles(string paragraphStyle);

    static bool IsFormattedTextAndNothingElse(IInline inline) {
        if (inline is not IFormattedText)
            return false;
        var type = inline.GetType();
        var allInterfaces = type.GetInterfaces();
        var ancestorInterfaces = type.GetInterface("IFormattedText").GetInterfaces();
        return allInterfaces.Length == ancestorInterfaces.Length + 1;
    }

}

interface IFootnote : IInline {

    string Marker { get; }

    IEnumerable<IBlock> Content { get; }

}

interface ISignatureBlock : IBlock
{

    public string Name { get; internal init; }

    IEnumerable<IInline> Content { get; }

}

interface IImageRef : IInline {

    string Src { get; set; }

    string Style { get; }

    Imaging.Inset? Crop { get; }

    int? Rotate { get; }

}

interface INeutralCitation : IFormattedText { }

interface INeutralCitation2 : IInline {

    IEnumerable<IFormattedText> Contents { get; init; }

    string Text { get; }

}


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

interface IDocDate : IDate, INamedDate {

    int Priority { get; }

}

public enum PartyRole { Appellant, Applicant, Claimant, Defendant, Petitioner, Respondent, InterestedParty, ThirdParty, Intervener, RequestedPerson, RequestingState, BeforeTheV, AfterTheV }

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

    /// <summary>
    /// Indicates whether a party should be ignored in the XML.
    /// </summary>
    bool Suppress { get; set; }

    // bool ROnTheApplicationOf { get; set; }

}

interface IParty1 : IFormattedText, IParty { }

interface IParty2 : IParty {

    IEnumerable<ITextOrWhitespace> Contents { get; }

}

interface IRole : IInlineContainer {

    PartyRole Role { get; }

}


interface IDocTitle : IFormattedText { }

interface IDocTitle2 : IInlineContainer { }


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

interface IDocJurisdiction : IInlineContainer {

    string Id { get; }

    string LongName { get; }

    string ShortName { get; }

    bool Overridden { get; }
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

interface IInternalLink : IInline {

    string Target { get; }

    IList<IInline> Contents { get; }

}

enum RefType { Case, Legislation }

/// a <ref> in LegalDocML is an inline element "containing a legal reference"
/// the properties below are part of the Find Case Law enrichment model
interface IRef : IHyperlink1 {

    string Canonical { get; }  // required

    bool? IsNeutral { get; }

    RefType? Type { get; }

    string Origin { get => "parser"; }

}

interface IInvalidRef : IInline {
    public void Add(XmlElement parent) {
        XmlElement invalidRef = parent.OwnerDocument.CreateElement("ref", parent.NamespaceURI);
        invalidRef.SetAttribute("class", parent.NamespaceURI, "invalid");
        invalidRef.SetAttribute("href", "#");
        parent.AppendChild(invalidRef);
    }

}

interface IPageReference : IInlineContainer { }

interface ILineBreak : ITextOrWhitespace { }

interface ITab : ITextOrWhitespace { }


/* markers */

interface IMarker : IInline { }

interface IBookmark : IMarker {

    string Name { get; }

}

}
