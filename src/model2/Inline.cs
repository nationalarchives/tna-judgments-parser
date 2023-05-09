
using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

internal class WText : UK.Gov.Legislation.Judgments.IFormattedText {

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

    public string Style {
        get => properties?.RunStyle?.Val;
    }

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

    public String Text => IInline.ToString(Contents);

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

}

internal class WRole : IRole {

    public IEnumerable<IInline> Contents { get; init; }

    public PartyRole Role { get; init; }

}

internal class WDocTitle : WText, IDocTitle {

    public WDocTitle(WText text) : base(text.Text, text.properties) { }

}
internal class WDocTitle2 : IDocTitle2 {

    public IEnumerable<IInline> Contents { get; init; }

    public static WLine ConvertContents(WLine line) {
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

internal class WRef : WHyperlink1, IRef {

    public WRef(string text, RunProperties props) : base(text, props) { }

    public string Canonical { get; internal init; }  // required

    public bool? IsNeutral { get; internal init; }

    public RefType? Type { get; internal init; }

}

internal class WLineBreak : ILineBreak {

    internal WLineBreak() { }

    internal WLineBreak(Break br) { }

    internal WLineBreak(CarriageReturn cr) { }

    internal WLineBreak(OpenXmlElement e) { }   // should be an unknown br

}

}
