
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

internal class WText : IFormattedText {

    public readonly RunProperties properties;
    private readonly string text;

    public WText(string text, RunProperties properties) {
        this.properties = properties;
        this.text = text;
    }
    public WText(Text text, RunProperties properties) {
        this.properties = properties;
        this.text = text.Text;
    }
    public WText(Run run) {
        this.properties = run.RunProperties;
        this.text = run.InnerText;
    }
    public WText(NoBreakHyphen hyphen, RunProperties properties) {
        this.properties = properties;
        this.text = "-";
    }
    public static WText MakeHyphen(RunProperties properties) {
        return new WText("-", properties);
    }

    #nullable enable
    public string? Style {
        get => properties?.RunStyle?.Val;
    }
    #nullable disable

    public bool? Italic {
        get {
            Italic italic = properties?.Italic;
            if (italic == null)
                return null;
            OnOffValue val = italic.Val;
            if (val == null)
                return true;
            return val.Value;
        }
    }

    public bool? Bold {
        get {
            Bold bold = properties?.Bold;
            if (bold == null)
                return null;
            OnOffValue val = bold.Val;
            if (val == null)
                return true;
            return val.Value;
        }
    }

    public UnderlineValues2? Underline {
        get {
            Underline underline = properties?.Underline;
            if (underline is null)
                return null;
            return DOCX.Underline2.Get(underline);
        }
    }

    public bool? Uppercase {
        get {
            Caps caps = properties?.Caps;
            if (caps is null)
                return null;
            OnOffValue val = caps.Val;
            if (val == null)
                return true;
            return val.Value;
        }
    }

    public static StrikethroughValue? GetStrikethrough(RunProperties props) {
        var single = DOCX.Util.OnOffToBool(props?.Strike);
        if (single.HasValue)
            return single.Value ? StrikethroughValue.Single : StrikethroughValue.None;
        var dbl = DOCX.Util.OnOffToBool(props?.DoubleStrike);
        if (dbl.HasValue)
            return dbl.Value ? StrikethroughValue.Double : StrikethroughValue.None;
        return null;
    }

    public StrikethroughValue? Strikethrough {
        get => GetStrikethrough(properties);
    }

    public bool? SmallCaps {
        get {
            SmallCaps caps = properties?.SmallCaps;
            if (caps is null)
                return null;
            OnOffValue val = caps.Val;
            if (val == null)
                return true;
            return val.Value;
        }
    }

    public SuperSubValues? SuperSub {
        get {
            VerticalTextAlignment valign = properties?.VerticalTextAlignment;
            if (valign is null)
                return null;
            EnumValue<VerticalPositionValues> val = valign.Val;
            if (val.Equals(VerticalPositionValues.Superscript))
                return SuperSubValues.Superscript;
            if (val.Equals(VerticalPositionValues.Subscript))
                return SuperSubValues.Subscript;
            return SuperSubValues.Baseline;
        }
    }

    virtual public string FontName {
        get {
            if (properties is null)
                return null;
            return DOCX.Fonts.GetFontName(properties);
        }
        // get {
        //     if (properties?.RunFonts?.AsciiTheme is not null) {
        //         MainDocumentPart main = properties.Ancestors<Document>().First().MainDocumentPart;
        //         return DOCX.Themes.GetFontName(main, properties.RunFonts.AsciiTheme);
        //     }
        //     return properties?.RunFonts?.Ascii?.Value;
        // }
    }

    virtual public float? FontSizePt { get {
        string fontSize = properties?.FontSize?.Val;
        if (fontSize is null)
            return null;
        return float.Parse(fontSize) / 2f;
    } }

    public string FontColor { get {
        string color = properties?.Color?.Val?.Value;
        if (color is not null)
            return color;
        return properties?.Shading?.Color?.Value;
    } }

    public string BackgroundColor { get {
        HighlightColorValues? highlight = properties?.Highlight?.Val?.Value;
        if (!highlight.HasValue)
            return properties?.Shading?.Fill?.Value;
        if (highlight == HighlightColorValues.None)
            return null;
        return highlight.ToString().ToLower();
    } }

    public bool IsHidden { get {
        Vanish vanish = properties?.Vanish;
        if (vanish is null)
            return false;
        OnOffValue val = vanish.Val;
        if (val is null)
            return true;
        return val.Value;
    } }

    public string Text {
        get {
            return text.Replace('\uF020', ' '); // .Replace('\u00A0', ' ')
        }
    }

    internal Tuple<WText, WText> Split(int i) {
        var first = text.Substring(0, i);
        var second = text.Substring(i);
        return new Tuple<WText, WText>( new WText(first, properties), new WText(second, properties) );
    }

    /// <summary>
    /// This functions adds CSS formatting values derived from a paragraph style to override those from a run style
    /// </summary>
    /// <param name="formatting">a dictionary of CSS key-values pairs, including some ad hoc inline formatting from a run (which is not to be overridden)</param>
    /// <param name="paragraphStyle">the paragraph style</param>
    /// <param name="runStyle">the character style</param>
    /// <param name="main"></param>
    public static void OverrideRunStyleWithParagraphStyle(Dictionary<string, string> formatting, string paragraphStyle, string runStyle, MainDocumentPart main) {
        if (paragraphStyle is null)
            return;
        if (runStyle is null)
            return;
        Dictionary<string, string> formattingFromParagraphStyle = DOCX.CSS.ExtractCharacterFormatting(main, paragraphStyle);
        Dictionary<string, string> formattingFromCharacterStyle = DOCX.CSS.ExtractCharacterFormatting(main, runStyle);
        foreach(KeyValuePair<string, string> entry in formattingFromCharacterStyle) {
            if (formatting.ContainsKey(entry.Key))
                continue;
            bool existsInParagraphStyle = formattingFromParagraphStyle.TryGetValue(entry.Key, out string valueFromParagraphStyle);
            if (!existsInParagraphStyle)
                continue;
            if (entry.Value == valueFromParagraphStyle)
                continue;
            formatting.Add(entry.Key, valueFromParagraphStyle);
        }
    }

    virtual public Dictionary<string, string> GetCSSStyles(string paragraphStyle) {
        Dictionary<string, string> formatting = CSS.GetCSSStyles(this);
        if (properties is not null) {
            MainDocumentPart main = DOCX.Main.Get(properties);
            OverrideRunStyleWithParagraphStyle(formatting, paragraphStyle, Style, main);
        }
        return formatting;
    }

        public override string ToString()
        {
            return "WText: " + this.text;
        }
    }

class WTab : ITab {

    internal WTab(TabChar tab) { }
    internal WTab(PositionalTab pTab) { }
    internal WTab(OpenXmlElement e) { }

}


internal class WNeutralCitation : WText, INeutralCitation {

    public WNeutralCitation(string text, RunProperties properties) : base(text, properties) { }

}

internal class WNeutralCitation2 : INeutralCitation2 {

    public IEnumerable<IFormattedText> Contents { get; init; }

    public string Text => IInline.ToString(Contents);

}


internal class WCourtType : WText, ICourtType1 {

    public WCourtType(string text, RunProperties props) : base(text, props) { }

    public WCourtType(WText text, Court court) : base(text.Text, text.properties) {
        Code = court.Code;
    }

    public string Code { get; init; }

}

internal class WCourtType2 : ICourtType2 {

    public string Code { get; init; }

    public IEnumerable<IInline> Contents { get; init; }

}

internal class WCaseNo : WText, ICaseNo {

    public WCaseNo(string text, RunProperties props) : base(text, props) { }

}

internal class WDate : IDate {

    private readonly DateTime date;

    public WDate(IEnumerable<IFormattedText> contents, DateTime date) {
        Contents = contents;
        this.date = date;
    }

    public IEnumerable<IFormattedText> Contents { get; }

    public string Date {
        get {
            return date.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10);
        }
    }

}

internal class WDocDate : IDocDate {

    public WDocDate(IEnumerable<IFormattedText> contents, DateTime date) {
        Contents = contents;
        Date = date.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10);
    }
    public WDocDate(IFormattedText text, DateTime date) {
        Contents = new List<IFormattedText>(1) { text };
        Date = date.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10);
    }
    public WDocDate(string text, RunProperties properties, DateTime date) {
        WText wText = new WText(text, properties);
        Contents = new List<IFormattedText>(1) { wText };
        Date = date.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Substring(0, 10);
    }
    public WDocDate(WDate wDate) {
        Contents = wDate.Contents;
        Date = wDate.Date;
    }

    public IEnumerable<IFormattedText> Contents { get; }

    public string Date { get; }

    public string Name { get; init; } = "judgment";

    public int Priority { get; init; } = 0;

}

internal class WDateTime : IDateTime {

    public IEnumerable<IFormattedText> Contents { get; init; }

    public DateTime DateTime { get; init; }

}

internal class WParty : WText, IParty1 {

    public WParty(string text, RunProperties props) : base(text, props) { }

    public WParty(WText text) : base(text.Text, text.properties) { }

    public string Name { get {
        bool uppercase = this.Uppercase ?? false;
        string text = uppercase ? this.Text.ToUpper() : this.Text;
        return Party.MakeName(text);
    } }

    public PartyRole? Role { get; set; }

    public bool Suppress { get; set; }

    // public bool RonTheApplicationOf { get; set; }

}

internal class WParty2 : IParty2 {

    public WParty2(IEnumerable<ITextOrWhitespace> contents) {
        this.Contents = contents;
    }

    public string Text { get {
        return string.Join("", this.Contents.Select(inline => inline is IFormattedText text ? text.Text : " "));
    } }

    public string Name { get => Party.MakeName(this.Text); }

    public PartyRole? Role { get; set; }

    public IEnumerable<ITextOrWhitespace> Contents { get; init; }

    public bool Suppress { get; set; }

    // public bool RonTheApplicationOf { get; set; }

}

internal class WRole : IRole {

    public IEnumerable<IInline> Contents { get; init; }

    public PartyRole Role { get; init; }

}

internal class WSignatureBlock : ISignatureBlock {

    public string Name { get; init; }

    public IEnumerable<IInline> Content { get; init; }

}

internal class WDocTitle : WText, IDocTitle {

    public WDocTitle(string text, RunProperties rProps) : base(text, rProps) { }

    public WDocTitle(WText text) : base(text.Text, text.properties) { }

}
internal class WDocTitle2 : IDocTitle2 {

    public IEnumerable<IInline> Contents { get; init; }

    public static WLine ConvertContents(WLine line) {
        if (line.Contents.Count() == 1 && line.Contents.First() is WText first) {
            WDocTitle title1 = new WDocTitle(first);
            return WLine.Make(line, new List<IInline>(1) { title1 });
        }
        WDocTitle2 title = new WDocTitle2 { Contents = line.Contents };
        return WLine.Make(line, new List<IInline>(1) { title });
    }

}

internal class WJudge : WText, IJudge {

    public WJudge(string text, RunProperties props) : base(text, props) { }

    public WJudge(WText text) : base(text.Text, text.properties) { }

}

internal class WLawyer : WText, ILawyer {

    public WLawyer(string text, RunProperties props) : base(text, props) { }

    public WLawyer(WText text) : base(text.Text, text.properties) { }

}

abstract class WInlineContainer : IInlineContainer {

    public IEnumerable<IInline> Contents { get; internal init; }

}

internal class WDocJurisdiction : WInlineContainer, IDocJurisdiction {

    public string Id { get => "jurisdiction-" + ShortName.ToLower(); }

    public string LongName { get; internal init; }

    public string ShortName { get; internal init; }

    public bool Overridden => false;
}


internal class WLocation : WText, ILocation {

    public WLocation(string text, RunProperties props) : base(text, props) { }

}

internal class WHyperlink1 : WText, IHyperlink1 {

    public WHyperlink1(string text, RunProperties props) : base(text, props) { }

    public WHyperlink1(WText text) : base(text.Text, text.properties) { }

    public string Href { get; init; }

    public string ScreenTip { get; init; }

}

internal class WHyperlink2 : IHyperlink2 {

    public IEnumerable<IInline> Contents { get; init; }

    public string Href { get; init; }

    public string ScreenTip { get; init; }

}

internal class InternalLink : IInternalLink {

    public string Target { get; internal init; }

    public List<IInline> Contents { get; internal init; }

    IList<IInline> IInternalLink.Contents { get => Contents; }

}

internal class WRef : WHyperlink1, IRef {

    public WRef(string text, RunProperties props) : base(text, props) { }

    public string Canonical { get; internal init; }  // required

    public bool? IsNeutral { get; internal init; }

    public RefType? Type { get; internal init; }

}

// This doesn't fit in with the rest of the design with Refs and is also
// Lawmaker-specific. There is some additional work to be done to
// make this less of a band-aid
public class WInvalidRef : IInvalidRef { }
internal abstract class InlineContainer : IInlineContainer {

    public IEnumerable<IInline> Contents { get; internal init; }

}

internal class WPageReference : InlineContainer, IPageReference { }


/* whitespace */

internal class WLineBreak : ILineBreak {

    internal WLineBreak() { }

    internal WLineBreak(Break br) { }

    internal WLineBreak(CarriageReturn cr) { }

    internal WLineBreak(OpenXmlElement e) { }   // should be an unknown br

}


/* markers */

internal class WBookmark : IBookmark {

    public string Name { get; internal init; }

}

}
