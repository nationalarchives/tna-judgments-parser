
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Wordprocessing = DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

public class CSS {

    public static Dictionary<string, Dictionary<string, string>> Extract(MainDocumentPart main, string rootSelector) {
        Wordprocessing.Styles styles = main.StyleDefinitionsPart.Styles;
        Dictionary<string, Dictionary<string, string>> selectors = new Dictionary<string, Dictionary<string, string>>();

        Dictionary<string, string> defaultProperties = new Dictionary<string, string>();

        var x = styles.DocDefaults.RunPropertiesDefault.RunPropertiesBaseStyle;
        AddFontFamily(x?.RunFonts, defaultProperties);
        AddFontSize(x?.FontSize?.Val, defaultProperties);

        RunFonts themeRunFonts = styles.DocDefaults.RunPropertiesDefault.RunPropertiesBaseStyle?.RunFonts;
        // ThemeFontValues? y = styles.DocDefaults.RunPropertiesDefault.RunPropertiesBaseStyle?.RunFonts?.AsciiTheme;
        if (themeRunFonts.AsciiTheme is not null) {
            ThemeFontValues themeFont = themeRunFonts.AsciiTheme;
            string fontName = Themes.GetFontName(main, themeFont);
            AddFontFamily(fontName, defaultProperties);
        }

        Style defaultParagraphStyle = Styles.GetDefaultParagraphStyle(main);
        AddFontStyle(defaultParagraphStyle, defaultProperties);
        AddFontWeight(defaultParagraphStyle, defaultProperties);
        AddTextDecoration(defaultParagraphStyle, defaultProperties);
        AddTextTransform(defaultParagraphStyle, defaultProperties);
        AddFontVariant(defaultParagraphStyle, defaultProperties);
        AddFontFamily(defaultParagraphStyle, defaultProperties);
        AddFontSize(defaultParagraphStyle, defaultProperties);
        AddColor(defaultParagraphStyle, defaultProperties);
        AddBackgroundColor(defaultParagraphStyle, defaultProperties);

        Style defaultCharacterStyle = Styles.GetDefaultCharacterStyle(main);
        AddFontStyle(defaultCharacterStyle, defaultProperties);
        AddFontWeight(defaultCharacterStyle, defaultProperties);
        AddTextDecoration(defaultCharacterStyle, defaultProperties);
        AddTextTransform(defaultCharacterStyle, defaultProperties);
        AddFontVariant(defaultCharacterStyle, defaultProperties);
        AddFontFamily(defaultCharacterStyle, defaultProperties);
        AddFontSize(defaultCharacterStyle, defaultProperties);
        AddColor(defaultCharacterStyle, defaultProperties);
        AddBackgroundColor(defaultCharacterStyle, defaultProperties);
        selectors.Add(rootSelector, defaultProperties);

        IEnumerable<Style> paragraphStyles = styles.ChildElements
            .OfType<Style>()
            // .Where(e => e is Style).Cast<Style>()
            .Where(p => p.Type.Equals(StyleValues.Paragraph));
        foreach (Style style in paragraphStyles) {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            AddAlignment(style, properties, main);
            AddMarginLeft(style, properties, main);
            AddMarginRight(style, properties);
            AddFontStyle(style, properties);
            AddFontWeight(style, properties);
            AddTextDecoration(style, properties);
            AddTextTransform(style, properties);
            AddFontVariant(style, properties);
            AddVerticalAlign(style, properties);
            AddFontFamily(style, properties);
            AddFontSize(style, properties);
            AddColor(style, properties);
            AddBackgroundColor(style, properties);
            if (properties.Count > 0)
                selectors.Add(rootSelector + " ." + style.StyleId.Value, properties);
        }
        IEnumerable<Style> characterStyles = styles.ChildElements
            .OfType<Style>()
            .Where(p => p.Type.Equals(StyleValues.Character));
        foreach (Style style in characterStyles) {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            AddFontStyle(style, properties);
            AddFontWeight(style, properties);
            AddTextDecoration(style, properties);
            AddTextTransform(style, properties);
            AddFontVariant(style, properties);
            AddVerticalAlign(style, properties);
            AddFontFamily(style, properties);
            AddFontSize(style, properties);
            AddColor(style, properties);
            AddBackgroundColor(style, properties);
            if (properties.Count > 0)
                selectors.Add(rootSelector + " ." + style.StyleId.Value, properties);
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

            /* sometimes a style specifies a numbering level but inherits its numbering id from its parent style */
            NumberingId numId = s.GetInheritedProperty(s2 => s2.StyleParagraphProperties?.NumberingProperties?.NumberingId);
            if (numId is null)
                return null;
            int ilvl = s.GetInheritedProperty(s2 => s2.StyleParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value) ?? 0;
            Level level = Numbering.GetLevel(main, numId, ilvl);
            // Level level = Numbering.GetLevel(main, numProps);
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
        if (underline.Val is null)
            return;
        string key = "text-decoration-line";
        string value = (underline.Val.Value == UnderlineValues.None) ? "none" : "underline";
        css[key] = value;
        if (underline.Val.Value == UnderlineValues.Single) {
            string key2 = "text-decoration-style";
            string value2 = "solid";
            css[key2] = value2;
        } else if (underline.Val.Value == UnderlineValues.Double) {
            string key2 = "text-decoration-style";
            string value2 = "double";
            css[key2] = value2;
        } else if (underline.Val.Value == UnderlineValues.Dotted) {
            string key2 = "text-decoration-style";
            string value2 = "dotted";
            css[key2] = value2;
        } else if (underline.Val.Value == UnderlineValues.Dash) {
            string key2 = "text-decoration-style";
            string value2 = "dashed";
            css[key2] = value2;
        } else if (underline.Val.Value == UnderlineValues.Wave) {
            string key2 = "text-decoration-style";
            string value2 = "wavy";
            css[key2] = value2;
        }
    }

    private static void AddTextTransform(Style style, Dictionary<string, string> css) {
        Caps caps = style.GetInheritedProperty(s => s.StyleRunProperties?.Caps);
        if (caps is null)
            return;
        string key = "text-transform";
        OnOffValue isUppercase = caps.Val ?? OnOffValue.FromBoolean(true);
        string value = isUppercase ? "uppercase" : "none";
        css[key] = value;
    }

    private static void AddFontVariant(Style style, Dictionary<string, string> css) {
        SmallCaps caps = style.GetInheritedProperty(s => s.StyleRunProperties?.SmallCaps);
        if (caps is null)
            return;
        string key = "font-variant";
        OnOffValue isSmallCaps = caps.Val ?? OnOffValue.FromBoolean(true);
        string value = isSmallCaps ? "small-caps" : "normal";
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
        else if (valign.Val.Equals(VerticalPositionValues.Baseline))
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
        if (fontName.EndsWith(" (W1)"))
            fontName = fontName.Substring(0, fontName.Length - 5);
        if (fontName == "Calibri")
            return fontName + ", sans-serif";
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
        if (string.IsNullOrEmpty(font.Value))
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
        else if (Regex.IsMatch(value, @"^[A-F0-9]{6}$"))
            value = "#" + value;
        css[key] = value;
    }

    private static void AddBackgroundColor(Style style, Dictionary<string, string> css) {
        StringValue background = style.GetInheritedProperty(s => s.StyleRunProperties?.Shading?.Color);
        if (background is null)
            return;
        string key = "background-color";
        string value = background.Value;
        if (value == "auto")
            value = "initial";
        else if (Regex.IsMatch(value, @"^[A-F0-9]{6}$"))
            value = "#" + value;
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
        return css.TrimEnd(';').Split(";")
            .Select(pair => pair.Split(":"))
            .Select(x => new KeyValuePair<string,string>(x[0], x[1]))
            .Distinct(new CSSKeyValueComparer())
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

}

}
