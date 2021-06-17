
using System.Collections.Generic;
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
            string selector = "*";
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
                string value = style.StyleRunProperties.Underline.Val.Equals(UnderlineValues.None)
                    ? "none" : "underline";
                properties.Add(key, value);
            }
            if (properties.Count > 0)
                selectors.Add(style.StyleId.Value, properties);
        }
        return selectors;
    }

}

}
