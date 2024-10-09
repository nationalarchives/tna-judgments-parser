
using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using ParseNS = UK.Gov.Legislation.Judgments.Parse;
using JudgmentsNS = UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Judgments.DOCX {

abstract internal class WNumText : IFormattedText {

    private readonly NumberingSymbolRunProperties props;
    private readonly ParagraphMarkRunProperties props2;
    private readonly Style style;

    public WNumText(MainDocumentPart main, DOCX.NumberInfo info, Paragraph paragraph) {
        this.props = info.Props;
        this.props2 = paragraph.ParagraphProperties?.ParagraphMarkRunProperties;
        string styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        this.style = styleId is null ? null : DOCX.Styles.GetStyle(main, styleId);
        Text = info.Number;
    }

    public string Style => null;

    private T Get3<T>(Func<NumberingSymbolRunProperties,T> getter1, Func<StyleRunProperties,T> getter2) {
        T prop = default(T);
        if (props is not null)
            prop = getter1(props);
        if (prop is null && props2 is not null)
            prop = props2.ChildElements.OfType<T>().FirstOrDefault();
        if (prop is null && style is not null)
            prop = Styles.GetStyleProperty(this.style, s => s.StyleRunProperties is null ? default(T) : getter2(s.StyleRunProperties));
        return prop;
    }

    private string GetHps3<T>(Func<NumberingSymbolRunProperties,T> getter1, Func<StyleRunProperties,T> getter2) where T : HpsMeasureType {
        string prop = null;
        if (props is not null)
            prop = getter1(props)?.Val?.Value;
        if (prop is null && props2 is not null)
            prop = props2.ChildElements.OfType<T>().FirstOrDefault()?.Val?.Value;
        if (prop is null && style is not null)
            prop = Styles.GetStyleProperty(this.style, s => s.StyleRunProperties is null ? null : getter2(s.StyleRunProperties))?.Val?.Value;
        return prop;
    }

    /* some properties don't depend on the ParagraphMarkRunProperties */
    // private T Get2<T>(Func<NumberingSymbolRunProperties,T> getter1, Func<StyleRunProperties,T> getter2) {
    //     T prop = default(T);
    //     if (props is not null)
    //         prop = getter1(props);
    //     if (prop is null && style is not null)
    //         prop = Styles.GetStyleProperty(this.style, s => s.StyleRunProperties is null ? default(T) : getter2(s.StyleRunProperties));
    //     return prop;
    // }

    public bool? Italic {
        get {
            Italic italic = Get3<Italic>(x => x.Italic, x => x.Italic);
            return DOCX.Util.OnOffToBool(italic);
        }
    }

    public bool? Bold {
        get {
            Bold bold = Get3<Bold>(x => x.Bold, x => x.Bold);
            return DOCX.Util.OnOffToBool(bold);
        }
    }

    public UnderlineValues2? Underline {
        // underlining does not depend on style: [2023] EWCOP 3
        get {
            Underline underline = props?.Underline;
            return DOCX.Underline2.Get(underline);
        }
    }

    public bool? Uppercase {
        get {
            Caps caps = Get3<Caps>(x => x.Caps, x => x.Caps);
            return DOCX.Util.OnOffToBool(caps);
        }
    }

    public StrikethroughValue? Strikethrough {
        get {
            Strike single = Get3<Strike>(x => x.Strike, x => x.Strike);
            if (single is not null) {
                if (single.Val is null)
                    return  StrikethroughValue.Single;
                return single.Val.Value ? StrikethroughValue.Single : StrikethroughValue.None;
            }
            DoubleStrike dbl = Get3<DoubleStrike>(x => x.DoubleStrike, x => x.DoubleStrike);
            if (dbl is not null) {
                if (dbl.Val is null)
                    return  StrikethroughValue.Double;
                return dbl.Val.Value ? StrikethroughValue.Double : StrikethroughValue.None;
            }
            return null;
        }
    }

    public bool? SmallCaps {
        get {
            SmallCaps caps = Get3<SmallCaps>(x => x.SmallCaps, x => x.SmallCaps);
            return DOCX.Util.OnOffToBool(caps);
        }
    }

    public SuperSubValues? SuperSub {
        get {
            VerticalTextAlignment valign = Get3<VerticalTextAlignment>(x => x.VerticalTextAlignment, x => x.VerticalTextAlignment);
            if (valign is null)
                return null;
            EnumValue<VerticalPositionValues> val = valign.Val;
            if (val == VerticalPositionValues.Superscript)
                return SuperSubValues.Superscript;
            if (val == VerticalPositionValues.Subscript)
                return SuperSubValues.Subscript;
            return SuperSubValues.Baseline;
        }
    }

    public string FontName { get {
        string value = null;
        if (props is not null)
            value = DOCX.Fonts.GetFontName(props);
        RunFonts pMarkFont = props2?.ChildElements.OfType<RunFonts>().FirstOrDefault();
        if (value is null && pMarkFont?.AsciiTheme is not null)
            value = DOCX.Themes.GetFontName(DOCX.Main.Get(pMarkFont), pMarkFont.AsciiTheme);
        if (value is null && pMarkFont?.Ascii is not null)
            value = pMarkFont.Ascii.Value;
        if (value is null && style is not null)
            value = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.RunFonts?.Ascii?.Value);
        return value;
    } }

    public float? FontSizePt { get {
        string fontSize = GetHps3<FontSize>(x => x.FontSize, x => x.FontSize);
        if (fontSize is null)
            return null;
        return float.Parse(fontSize) / 2f;
    } }

    public string FontColor { get {
        Color color = Get3<Color>(x => x.Color, x => x.Color);
        return color?.Val?.Value;
    } }

    public string BackgroundColor { get {
        StringValue color = Get3<StringValue>(x => x.Shading?.Fill, x => x.Shading?.Fill);
        return color?.Value;
    } }

    public bool IsHidden { get {
        Vanish vanish = props?.Vanish;
        if (vanish is null)
            return false;
        OnOffValue val = vanish.Val;
        if (val is null)
            return true;
        return val.Value;
    } }

    public string Text { get; private init; }

    virtual public Dictionary<string, string> GetCSSStyles(string paragraphStyle)
    {
        return JudgmentsNS.CSS.GetCSSStyles(this);
    }

}

internal class WNumber : WNumText, INumber {

    private readonly MainDocumentPart main;
    private readonly ParagraphProperties pProps;

    public WNumber(MainDocumentPart main, DOCX.NumberInfo info, Paragraph paragraph) : base(main, info, paragraph) {
        this.main = main;
        this.pProps = paragraph.ParagraphProperties;
    }

    public float? LeftIndentInches {
        get => DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, pProps);
    }

    public string LeftIndent {
        get => JudgmentsNS.CSS.ConvertSize(LeftIndentInches, "in");
    }
    public string FirstLineIndent {
        get {
            float? inches = DOCX.Paragraphs.GetFirstLineIndentWithNumberingAndStyleInInches(main, pProps);
            return JudgmentsNS.CSS.ConvertSize(inches, "in");
        }
    }

    // public string ParagraphStyle => pProps?.ParagraphStyleId?.Val?.Value;

    internal static void AddIndentFormatting(INumber that, Dictionary<string, string> styles) {
        if (that.LeftIndent is not null && that.FirstLineIndent is not null && "-" + that.LeftIndent == that.FirstLineIndent)
            return;
        if (that.LeftIndent is not null && that.LeftIndent != "0in")
            styles.Add("margin-left", that.LeftIndent);
        if (that.FirstLineIndent is not null && that.FirstLineIndent != "0in")
            styles.Add("text-indent", that.FirstLineIndent);
    }

    internal static void RemoveDefaultCharacterFormatting(MainDocumentPart main, Dictionary<string, string> formatting) {
        if (formatting.Count == 0)
            return;
        Dictionary<string, string> defaultCharFormatting = DOCX.CSS.ExtractDefaultCharacterFormatting(main);
        foreach(KeyValuePair<string, string> entry in formatting) {
            if (defaultCharFormatting.TryGetValue(entry.Key, out string defaultValue) && defaultValue == entry.Value)
                formatting.Remove(entry.Key);
            if (defaultValue is null && entry.Key == "font-style" && entry.Value == "normal")
                formatting.Remove(entry.Key);
            if (defaultValue is null && entry.Key == "font-weight" && entry.Value == "normal")
                formatting.Remove(entry.Key);
            if (defaultValue is null && entry.Key == "font-variant" && entry.Value == "normal")
                formatting.Remove(entry.Key);
            if (defaultValue is null && entry.Key == "text-decoration-line" && entry.Value == "none") {
                formatting.Remove(entry.Key);
                formatting.Remove("text-decoration-style");
            }
            if (defaultValue is null && entry.Key == "text-transform" && entry.Value == "none")
                formatting.Remove(entry.Key);
            if (defaultValue is null && entry.Key == "color" && entry.Value == "initial")
                formatting.Remove(entry.Key);
            if (defaultValue is null && entry.Key == "color" && entry.Value == "#000000")
                formatting.Remove(entry.Key);
            if (defaultValue is null && entry.Key == "background-color" && entry.Value == "initial")
                formatting.Remove(entry.Key);
            if (defaultValue is null && entry.Key == "vertical-align" && entry.Value == "baseline")
                formatting.Remove(entry.Key);
        }
    }

    override public Dictionary<string, string> GetCSSStyles(string paragraphStyle)
    {
        paragraphStyle ??= pProps?.ParagraphStyleId?.Val?.Value;
        Dictionary<string, string> styles = base.GetCSSStyles(paragraphStyle);
        AddIndentFormatting(this, styles);
        RemoveDefaultCharacterFormatting(main, styles);
        return styles;
    }

}

internal class WNumber2 : ParseNS.WText, INumber {

    private readonly MainDocumentPart main;
    private readonly ParagraphProperties pProps;

    internal WNumber2(string text, RunProperties rProps, MainDocumentPart main, ParagraphProperties pProps) : base(text, rProps) {
        this.main = main;
        this.pProps = pProps;
    }

    // override public string Style {
    //     get {
    //         var x = base.Style;
    //         if (string.IsNullOrEmpty(x))
    //             return "inline";
    //         else
    //             return x + " inline";
    //     }
    // }

    public float? LeftIndentInches { get => DOCX.Paragraphs.GetLeftIndentWithStyleButNotNumberingInInches(main, pProps); }

    public string LeftIndent {
        // we don't want a value here for EWHC/Admin/2013/2744.rtf
        get {
            // float? inches = DOCX.Paragraphs.GetLeftIndentWithStyleButNotNumberingInInches(main, pProps);
            // if (inches is null)
                return null;
            // return inches.Value.ToString("F2") + "in";
        }
    }
    public string FirstLineIndent {
        get {
            // float? inches = DOCX.Paragraphs.GetFirstLineIndentWithStyleButNotNumberingInInches(main, pProps);
            // if (inches is null)
                return null;
            // if (inches == 0.0f)
            //     return null;
            // return inches.Value.ToString("F2") + "in";
        }
    }

    // public string ParagraphStyle => pProps?.ParagraphStyleId?.Val?.Value;

    internal static void AddCharacterFormattingFromParagraphStyle(MainDocumentPart main, string pStyle, Dictionary<string, string> formatting) {
        if (pStyle is null)
            return;
        Dictionary<string, string> charFormatting = CSS.ExtractCharacterFormatting(main, pStyle);
        if (charFormatting.Count == 0)
            return;
        foreach(KeyValuePair<string, string> entry in charFormatting) {
            if (formatting.ContainsKey(entry.Key))
                continue;
            if (entry.Key == "text-decoration-style" && formatting.ContainsKey("text-decoration-line") && formatting["text-decoration-line"] == "none")
                continue;
            formatting[entry.Key] = entry.Value;
        }
    }

    override public Dictionary<string, string> GetCSSStyles(string paragraphStyle)
    {
        paragraphStyle ??= pProps?.ParagraphStyleId?.Val?.Value;
        Dictionary<string, string> styles = base.GetCSSStyles(paragraphStyle);
        WNumber.AddIndentFormatting(this, styles);
        AddCharacterFormattingFromParagraphStyle(main, paragraphStyle, styles);
        WNumber.RemoveDefaultCharacterFormatting(main, styles);
        return styles;
    }

}


}
