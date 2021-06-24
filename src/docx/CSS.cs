
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
        IEnumerable<Style> paragraphStyles = styles.ChildElements
            .Where(e => e is Style).Cast<Style>()
            .Where(p => p.Type.Equals(StyleValues.Paragraph));
        Dictionary<string, Dictionary<string, string>> selectors = new Dictionary<string, Dictionary<string, string>>();
        IFontInfo defaultFont = DOCX.Styles.GetDefaultFont(main);
        if (defaultFont is not null && (defaultFont.Name is not null || defaultFont.SizePt is not null)) {
            string selector = "body";   // this is unreliable
            Dictionary<string, string> properties = new Dictionary<string, string>();
            if (defaultFont.Name is not null) {
                string value = defaultFont.Name.Contains(" ") ? "'" + defaultFont.Name + "'" : defaultFont.Name;
                properties.Add("font-family", value);
            }
            if (defaultFont.SizePt is not null) {
                properties.Add("font-size", defaultFont.SizePt + "pt");
            }
            selectors.Add(selector, properties);
        }
        foreach (Style style in paragraphStyles) {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            if (style.StyleParagraphProperties?.Indentation?.Left is not null) {
                string key = "margin-left";
                float inches = float.Parse(style.StyleParagraphProperties.Indentation.Left.Value) / 1440f;
                string value = inches.ToString("F2") + "in";
                properties.Add(key, value);
            } else {
                NumberingProperties nProps = style.StyleParagraphProperties?.NumberingProperties;
                if (nProps is not null) {
                    Level level = Numbering.GetLevel(main, nProps);
                    if (level?.PreviousParagraphProperties?.Indentation?.Left is not null) {
                        string key = "margin-left";
                        float inches = float.Parse(level.PreviousParagraphProperties.Indentation.Left.Value) / 1440f;
                        string value = inches.ToString("F2") + "in";
                        properties.Add(key, value);
                    }
                }
            }
            
            if (style.StyleParagraphProperties?.Indentation?.Right is not null) {
                string key = "margin-right";
                float inches = float.Parse(style.StyleParagraphProperties.Indentation.Right.Value) / 1440f;
                string value = inches.ToString("F2") + "in";
                properties.Add(key, value);
            }
            if (style.StyleParagraphProperties?.Justification is not null) {
                string key = "text-align";
                string value = style.StyleParagraphProperties.Justification.Val.Equals(JustificationValues.Both)
                    ? "justify"
                    : style.StyleParagraphProperties.Justification.Val.Value.ToString().ToLower();
                    properties.Add(key, value);
            }
            if (style.StyleRunProperties?.Bold is not null) {
                string key = "font-weight";
                OnOffValue isBold = style.StyleRunProperties.Bold.Val ?? OnOffValue.FromBoolean(true);
                string value = isBold ? "bold" : "normal";
                properties.Add(key, value);
            }
            if (style.StyleRunProperties?.Underline is not null) {
                string key = "text-decoration";
                string value;
                if (style.StyleRunProperties.Underline.Val is null) // not sure this is right
                    value = "none";
                else if (style.StyleRunProperties.Underline.Val.Equals(UnderlineValues.None))
                    value = "none";
                else
                    value = "underline";
                properties.Add(key, value);
            }
            string fontName = Styles.GetFontName(main, style);
            if (fontName is not null) {
                if (fontName.Contains(" "))
                    fontName = "'" + fontName + "'";
                properties.Add("font-family", fontName);
            }
            float? fontSize = Styles.GetFontSizePt(main, style);
            if (fontSize is not null) {
                properties.Add("font-size", fontSize.ToString() + "pt");
            }
            if (properties.Count > 0)
                selectors.Add("." + style.StyleId.Value, properties);
        }
        return selectors;
    }

    public static string SerializeInline(Dictionary<string, string> properties) {
        return string.Join(";", properties.Select(pair => pair.Key + ":" + pair.Value));
    }

        private class Cmp : IEqualityComparer<KeyValuePair<string, string>>
        {
            public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
            {
                return x.Key.Equals(y.Key, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode([DisallowNull] KeyValuePair<string, string> obj)
            {
                return obj.Key.GetHashCode();
            }
        }

        public static Dictionary<string, string> ParseInline(string css) {
        // IEnumerable<KeyValuePair<string,string>> pairs =
        return css.Split(";")
            .Select(pair => pair.Split(":"))
            .Select(x => new KeyValuePair<string,string>(x[0], x[1]))
            .Distinct(new Cmp())
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        // pairs = Enumerable.Distinct();
        // return css.Split(";")
        //     .Select(pair => pair.Split(":"))
        //     .ToDictionary(x => x[0], x => x[1]);
    }

}

}
