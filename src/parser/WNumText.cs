
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

internal class WNumText : IFormattedText {

    private readonly NumberingSymbolRunProperties props;
    private readonly ParagraphMarkRunProperties props2;
    private readonly Style style;
    private readonly string text;

    public WNumText(string text, NumberingSymbolRunProperties props, ParagraphMarkRunProperties props2, Style style) {
        this.props = props;
        this.props2 = props2;
        this.style = style;
        this.text = text;
    }

    public string Style => null;

    public bool? Italic {
        get {
            Italic italic = props?.Italic;
            if (italic == null)
                italic = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.Italic);
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
            Bold bold = props?.Bold;
            if (bold == null)
                bold = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.Bold);
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
            Underline underline = props?.Underline;
            if (underline is null)
                underline = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.Underline);
            if (underline is null)
                return null;
            EnumValue<UnderlineValues> val = underline.Val;
            if (val is null)
                return null;
            if (val.Equals(UnderlineValues.None))
                return false;
            if (val.Equals(UnderlineValues.Single))
                return true;
            throw new System.Exception();
        }
    }

    public SuperSubValues? SuperSub {
        get {
            VerticalTextAlignment valign = props?.VerticalTextAlignment;
            if (valign is null)
                valign = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.VerticalTextAlignment);
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

    public string FontName { get {
        string value = null;
        if (props is not null)
            value = DOCX.Fonts.GetFontName(props);
        RunFonts pMarkFont = props2?.ChildElements.OfType<RunFonts>().FirstOrDefault();
        if (value is null && pMarkFont?.AsciiTheme is not null)
            value = DOCX.Themes.GetFontName(DOCX.Main.Get(pMarkFont), pMarkFont.AsciiTheme);
        if (value is null && pMarkFont?.Ascii is not null)
            value = pMarkFont.Ascii.Value;
        // string value = props?.RunFonts?.Ascii?.Value;
        if (value is null && style is not null)
            value = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.RunFonts?.Ascii?.Value);
        return value;
    } }

    public float? FontSizePt { get {
        string fontSize = props?.FontSize?.Val?.Value;
        if (fontSize is null)
            fontSize = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.FontSize?.Val?.Value);
        if (fontSize is null)
            return null;
        return float.Parse(fontSize) / 2f;
    } }

    public string FontColor { get {
        Color color = props?.Color;
        if (color is null)
            color = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.Color);
        // if (color?.Val?.Value == "auto")
        //     return "black";
        return color?.Val?.Value;
    } }

    public string BackgroundColor { get {
        StringValue color = props?.Shading?.Color;
        if (color is null)
            color = Styles.GetStyleProperty(style, s => s.StyleRunProperties?.Shading?.Color);
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

    public string Text {
        get {
            return text;
        }
    }

}

}
