
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Wordprocessing = DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

public class CSS {

    public static Dictionary<string, Dictionary<string, string>> Extract(MainDocumentPart main) {
        Wordprocessing.Styles styles = main.StyleDefinitionsPart.Styles;
        Dictionary<string, Dictionary<string, string>> selectors = new Dictionary<string, Dictionary<string, string>>();

        Dictionary<string, string> defaultProperties = new Dictionary<string, string>();

        var x = styles.DocDefaults.RunPropertiesDefault.RunPropertiesBaseStyle;
        AddFontFamily(x?.RunFonts, defaultProperties);
        AddFontSize(x?.FontSize?.Val, defaultProperties);

        Style defaultParagraphStyle = Styles.GetDefaultParagraphStyle(main);
        AddFontStyle(defaultParagraphStyle, defaultProperties);
        AddFontWeight(defaultParagraphStyle, defaultProperties);
        AddTextDecoration(defaultParagraphStyle, defaultProperties);
        // AddVerticalAlign(defaultParagraphStyle, defaultProperties);
        AddFontFamily(defaultParagraphStyle, defaultProperties);
        AddFontSize(defaultParagraphStyle, defaultProperties);
        AddColor(defaultParagraphStyle, defaultProperties);
        AddBackgroundColor(defaultParagraphStyle, defaultProperties);

        Style defaultCharacterStyle = Styles.GetDefaultCharacterStyle(main);
        AddFontStyle(defaultCharacterStyle, defaultProperties);
        AddFontWeight(defaultCharacterStyle, defaultProperties);
        AddTextDecoration(defaultCharacterStyle, defaultProperties);
        // AddVerticalAlign(defaultCharacterStyle, defaultProperties);
        AddFontFamily(defaultCharacterStyle, defaultProperties);
        AddFontSize(defaultCharacterStyle, defaultProperties);
        AddColor(defaultCharacterStyle, defaultProperties);
        AddBackgroundColor(defaultCharacterStyle, defaultProperties);
        selectors.Add("body", defaultProperties);

        // if (defaultFont is not null && (defaultFont.Name is not null || defaultFont.SizePt is not null)) {
        //     string selector = "body";   // this is unreliable
        //     Dictionary<string, string> properties = new Dictionary<string, string>();
        //     if (defaultFont.Name is not null) {
        //         string value = defaultFont.Name.Contains(" ") ? "'" + defaultFont.Name + "'" : defaultFont.Name;
        //         properties.Add("font-family", value);
        //     }
        //     if (defaultFont.SizePt is not null) {
        //         properties.Add("font-size", defaultFont.SizePt + "pt");
        //     }
        //     selectors.Add(selector, properties);
        // }
        IEnumerable<Style> paragraphStyles = styles.ChildElements
            .OfType<Style>()
            // .Where(e => e is Style).Cast<Style>()
            .Where(p => p.Type.Equals(StyleValues.Paragraph));
        foreach (Style style in paragraphStyles) {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            // if (style.StyleParagraphProperties?.Indentation?.Left is not null) {
            //     string key = "margin-left";
            //     float inches = float.Parse(style.StyleParagraphProperties.Indentation.Left.Value) / 1440f;
            //     string value = inches.ToString("F2") + "in";
            //     properties.Add(key, value);
            // } else {
            //     NumberingProperties nProps = style.StyleParagraphProperties?.NumberingProperties;
            //     if (nProps is not null) {
            //         Level level = Numbering.GetLevel(main, nProps);
            //         if (level?.PreviousParagraphProperties?.Indentation?.Left is not null) {
            //             string key = "margin-left";
            //             float inches = float.Parse(level.PreviousParagraphProperties.Indentation.Left.Value) / 1440f;
            //             string value = inches.ToString("F2") + "in";
            //             properties.Add(key, value);
            //         }
            //     }
            // }
            
            // if (style.StyleParagraphProperties?.Indentation?.Right is not null) {
            //     string key = "margin-right";
            //     float inches = float.Parse(style.StyleParagraphProperties.Indentation.Right.Value) / 1440f;
            //     string value = inches.ToString("F2") + "in";
            //     properties.Add(key, value);
            // }
            // if (style.StyleParagraphProperties?.Justification is not null) {
            //     string key = "text-align";
            //     string value = style.StyleParagraphProperties.Justification.Val.Equals(JustificationValues.Both)
            //         ? "justify"
            //         : style.StyleParagraphProperties.Justification.Val.Value.ToString().ToLower();
            //         properties.Add(key, value);
            // }
            AddAlignment(style, properties, main);
            AddMarginLeft(style, properties, main);
            AddMarginRight(style, properties);
            AddFontStyle(style, properties);
            AddFontWeight(style, properties);
            AddTextDecoration(style, properties);
            AddVerticalAlign(style, properties);
            AddFontFamily(style, properties);
            AddFontSize(style, properties);
            AddColor(style, properties);
            AddBackgroundColor(style, properties);
            // string fontName = Styles.GetFontName(main, style);
            // if (fontName is not null) {
            //     if (fontName.Contains(" "))
            //         fontName = "'" + fontName + "'";
            //     properties.Add("font-family", fontName);
            // }
            // float? fontSize = Styles.GetFontSizePt(main, style);
            // if (fontSize is not null) {
            //     properties.Add("font-size", fontSize.ToString() + "pt");
            // }
            if (properties.Count > 0)
                selectors.Add("." + style.StyleId.Value, properties);
        }
        IEnumerable<Style> characterStyles = styles.ChildElements
            .OfType<Style>()
            .Where(p => p.Type.Equals(StyleValues.Character));
        foreach (Style style in characterStyles) {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            AddFontStyle(style, properties);
            AddFontWeight(style, properties);
            AddTextDecoration(style, properties);
            AddVerticalAlign(style, properties);
            AddFontFamily(style, properties);
            AddFontSize(style, properties);
            AddColor(style, properties);
            AddBackgroundColor(style, properties);
            if (properties.Count > 0)
                selectors.Add("." + style.StyleId.Value, properties);
        }
        return selectors;
    }

    private static void AddAlignment(Style style, Dictionary<string, string> css, MainDocumentPart main) {
        Justification just = style.GetInheritedProperty(style => style.StyleParagraphProperties?.Justification);
        if (just is null)
            return;
        string key = "text-align";
        string value = (just.Val == JustificationValues.Both) ? "justify" : just.Val.Value.ToString().ToLower();
        css[key] = value;
    }

    private static void AddMarginLeft(Style style, Dictionary<string, string> css, MainDocumentPart main) {
        Styles.StylePropertyGetter<StringValue> getter = s => {
            StringValue value = s.StyleParagraphProperties?.Indentation?.Left;
            if (value is not null)
                return value;
            NumberingProperties numProps = s.StyleParagraphProperties?.NumberingProperties;
            if (numProps is null)
                return null;
            Level level = Numbering.GetLevel(main, numProps);
            return level?.PreviousParagraphProperties?.Indentation?.Left;
        };
        StringValue left = style.GetInheritedProperty(getter);
        if (left is null)
            return;
        string key = "margin-left";
        float inches = float.Parse(left.Value) / 1440f;
        string value = inches.ToString("F2") + "in";
        css[key] = value;
    }

    private static void AddMarginRight(Style style, Dictionary<string, string> css) {
        StringValue right = style.GetInheritedProperty(s => s.StyleParagraphProperties?.Indentation?.Right);
        if (right is null)
            return;
        string key = "margin-right";
        float inches = float.Parse(right.Value) / 1440f;
        string value = inches.ToString("F2") + "in";
        css[key] = value;
    }

    private static void AddFontStyle(Style style, Dictionary<string, string> css) {
        Italic italic = style.GetInheritedProperty(s => s.StyleRunProperties?.Italic);
        if (italic is null)
            return;
        string key = "font-style";
        string value = (italic.Val is null ? true : italic.Val.Value) ? "italic" : "normal";
        css[key] = value;
    }

    private static void AddFontWeight(Style style, Dictionary<string, string> css) {
        Bold bold = style.GetInheritedProperty(s => s.StyleRunProperties?.Bold);
        if (bold is null)
            return;
        string key = "font-weight";
        OnOffValue isBold = bold.Val ?? OnOffValue.FromBoolean(true);
        string value = isBold ? "bold" : "normal";
        css[key] = value;
    }

    private static void AddTextDecoration(Style style, Dictionary<string, string> css) {
        Underline underline = style.GetInheritedProperty(s => s.StyleRunProperties?.Underline);
        if (underline is null)
            return;
        string key = "text-decoration";
        string value;
        if (underline.Val is null)
            return;
        else if (underline.Val.Equals(UnderlineValues.None))
            value = "none";
        else if (underline.Val.Equals(UnderlineValues.Single))
            value = "underline";
        else
            throw new Exception();
        css[key] = value;
    }

    private static void AddVerticalAlign(Style style, Dictionary<string, string> css) {
        VerticalTextAlignment valign = style.GetInheritedProperty(s => s.StyleRunProperties?.VerticalTextAlignment);
        if (valign is null)
            return;
        string key = "vertical-align";
        string value;
        if (valign.Val is null)
            value = "baseline";
        else if (valign.Val.Equals(VerticalPositionValues.Superscript))
            value = "super";
        else if (valign.Val.Equals(VerticalPositionValues.Subscript))
            value = "sub";
        else
            throw new Exception();
        css[key] = value;
    }

    internal static string ToFontFamily(string fontName) {
        // if (fontName == "Arial (W1)")
        //     return "Arial";
        if (fontName.EndsWith(" (W1)"))
            return fontName.Substring(0, fontName.Length - 5);
        if (fontName.Contains(" "))
            return "'" + fontName + "'";
        return fontName;
    }

    private static void AddFontFamily(Style style, Dictionary<string, string> css) {
        RunFonts font = style.GetInheritedProperty(s => s.StyleRunProperties?.RunFonts);
        AddFontFamily(font, css);
    }
    private static void AddFontFamily(RunFonts font, Dictionary<string, string> css) {
        AddFontFamily(font?.Ascii, css);
    }
    private static void AddFontFamily(StringValue font, Dictionary<string, string> css) {
        if (font is null)
            return;
        string key = "font-family";
        string value = ToFontFamily(font.Value);
        css[key] = value;
    }

    private static void AddFontSize(Style style, Dictionary<string, string> css) {
        StringValue fontSize = style.GetInheritedProperty(s => s.StyleRunProperties?.FontSize?.Val);
        AddFontSize(fontSize, css);
    }
    private static void AddFontSize(StringValue fontSize, Dictionary<string, string> css) {
        if (fontSize is null)
            return;
        string key = "font-size";
        string value = (int.Parse(fontSize.Value) / 2) + "pt";
        css[key] = value;
    }

    private static void AddColor(Style style, Dictionary<string, string> css) {
        StringValue color = style.GetInheritedProperty(s => s.StyleRunProperties?.Color?.Val);
        if (color is null)
            return;
        string key = "color";
        string value = color.Value;
        if (value == "auto")
            value = "initial";
        css[key] = value;
    }

    private static void AddBackgroundColor(Style style, Dictionary<string, string> css) {
        StringValue background = style.GetInheritedProperty(s => s.StyleRunProperties?.Shading?.Color);
        if (background is null)
            return;
        string key = "background-color";
        string value = background.Value;
        css[key] = value;
    }

    public static string SerializeInline(Dictionary<string, string> properties) {
        return string.Join(";", properties.Select(pair => pair.Key + ":" + pair.Value));
    }

    private class CSSKeyValueComparer : IEqualityComparer<KeyValuePair<string, string>> {
        public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y) {
            return x.Key.Equals(y.Key, StringComparison.OrdinalIgnoreCase);
        }
        public int GetHashCode([DisallowNull] KeyValuePair<string, string> obj) {
            return obj.Key.GetHashCode();
        }
    }

    public static Dictionary<string, string> ParseInline(string css) {
        return css.Split(";")
            .Select(pair => pair.Split(":"))
            .Select(x => new KeyValuePair<string,string>(x[0], x[1]))
            .Distinct(new CSSKeyValueComparer())
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

}

}
