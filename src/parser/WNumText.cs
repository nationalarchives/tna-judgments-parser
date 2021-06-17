
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

}
