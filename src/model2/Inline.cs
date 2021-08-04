
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
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

    public bool? Underline {
        get {
            Underline underline = properties?.Underline;
            if (underline is null)
                return null;
            return DOCX.Underline2.Is(underline);
            // EnumValue<UnderlineValues> val = underline.Val;
            // if (val == null)
            //     return false;
            // if (val.Equals(UnderlineValues.Single))
            //     return true;
            // if (val.Equals(UnderlineValues.Thick))
            //     return true;
            // if (val.Equals(UnderlineValues.None))
            //     return false;
            // throw new Exception();
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

    public float? FontSizePt { get {
        string fontSize = properties?.FontSize?.Val;
        if (fontSize is null)
            return null;
        return float.Parse(fontSize) / 2f;
    } }

    public string FontColor { get {
        // if (properties?.Color?.Val?.Value == "auto")
        //     return "black";
        return properties?.Color?.Val?.Value;
    } }

    public string BackgroundColor { get {
        return properties?.Shading?.Color?.Value;
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
            return text;
        }
    }

}

class WTab : ITab {

    private readonly TabChar tab;

    internal WTab(TabChar tab) {
        this.tab = tab;
    }

}


internal class WNeutralCitation : WText, INeutralCitation {

    public WNeutralCitation(string text, RunProperties properties) : base(text, properties) { }

}

internal class WCourtType : WText, ICourtType {

    public WCourtType(string text, RunProperties props) : base(text, props) { }

    public string Code { get; init; }

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

}

internal class WDateTime : IDateTime {

    public IEnumerable<IFormattedText> Contents { get; init; }

    public DateTime DateTime { get; init; }

}

internal class WParty : WText, IParty {

    public WParty(string text, RunProperties props) : base(text, props) { }

    public WParty(WText text) : base(text.Text, text.properties) { }

}

internal class WJudge : WText, IJudge {

    public WJudge(string text, RunProperties props) : base(text, props) { }

    public WJudge(WText text) : base(text.Text, text.properties) { }

}

internal class WLawyer : WText, ILawyer {

    public WLawyer(string text, RunProperties props) : base(text, props) { }

    public WLawyer(WText text) : base(text.Text, text.properties) { }

}

internal class WHyperlink1 : WText, IHyperlink1 {

    public WHyperlink1(WText text) : base(text.Text, text.properties) { }

    public string Href { get; init; }

    public string ScreenTip { get; init; }

}

internal class WHyperlink2 : IHyperlink2 {

    public IEnumerable<IInline> Contents { get; init; }

    public string Href { get; init; }

    public string ScreenTip { get; init; }

}

internal class WLineBreak : ILineBreak {

    internal WLineBreak(Break br) { }

}

}
