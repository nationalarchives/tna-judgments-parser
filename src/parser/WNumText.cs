
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

internal class WNumText : IFormattedText {

    public readonly NumberingSymbolRunProperties properties;
    private readonly string text;

    public WNumText(string text, NumberingSymbolRunProperties properties) {
        this.properties = properties;
        this.text = text;
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
            EnumValue<UnderlineValues> val = underline.Val;
            if (val == null)
                return true;
            if (val.Equals(UnderlineValues.None))
                return false;
            return true;
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

    public string FontName { get {
        return properties?.RunFonts?.Ascii?.Value;
    } }

    public float? FontSizePt { get {
        string fontSize = properties?.FontSize?.Val;
        if (fontSize is null)
            return null;
        return float.Parse(fontSize) / 2f;
    } }

    public string Text {
        get {
            return text;
        }
    }

}

internal class WNumText2 : IFormattedText {

    // private readonly ParagraphMarkRunProperties props1;
    private readonly NumberingSymbolRunProperties props2;
    private readonly Style style;
    private readonly string text;

    public WNumText2(string text, NumberingSymbolRunProperties props2, Style style) {
        // this.props1 = props1;
        this.props2 = props2;
        this.style = style;
        this.text = text;
    }

    public bool? Italic {
        get {
            // Italic italic = props1?.ChildElements.OfType<Italic>().FirstOrDefault();
            Italic italic = props2?.Italic;
            if (italic == null)
                italic = Styles.GetStyleThing(style, s => s.StyleRunProperties?.Italic);
                // italic = style?.StyleRunProperties?.Italic;
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
            // Bold bold = props1?.ChildElements.OfType<Bold>().FirstOrDefault();
            Bold bold = props2?.Bold;
            if (bold == null)
                bold = Styles.GetStyleThing(style, s => s.StyleRunProperties?.Bold);
                // bold = style?.StyleRunProperties?.Bold;
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
            // Underline underline = props1?.ChildElements.OfType<Underline>().FirstOrDefault();
            Underline underline = props2?.Underline;
            if (underline is null)
                underline = Styles.GetStyleThing(style, s => s.StyleRunProperties?.Underline);
                // underline = style?.StyleRunProperties?.Underline;
            if (underline is null)
                return null;
            EnumValue<UnderlineValues> val = underline.Val;
            if (val == null)
                return true;
            if (val.Equals(UnderlineValues.None))
                return false;
            return true;
        }
    }

    public SuperSubValues? SuperSub {
        get {
            VerticalTextAlignment valign = props2?.VerticalTextAlignment;
            if (valign is null)
                valign = Styles.GetStyleThing(style, s => s.StyleRunProperties?.VerticalTextAlignment);
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
        // string value = props1?.ChildElements.OfType<RunFonts>().FirstOrDefault()?.Ascii?.Value;
        string value = props2?.RunFonts?.Ascii?.Value;
        if (value is null && style is not null)
            value = Styles.GetStyleValue(style, s => s.StyleRunProperties?.RunFonts?.Ascii?.Value);
        return value;
    } }

    public float? FontSizePt { get {
        // string fontSize = props1?.ChildElements.OfType<FontSize>().FirstOrDefault()?.Val?.Value;
        string fontSize = props2?.FontSize?.Val?.Value;
        if (fontSize is null && style is not null)
            fontSize = Styles.GetStyleValue(style, s => s.StyleRunProperties?.FontSize?.Val?.Value);
        if (fontSize is null)
            return null;
        return float.Parse(fontSize) / 2f;
    } }

    public string Text {
        get {
            return text;
        }
    }

}

}
