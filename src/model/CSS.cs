
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments {

class CSS {

    internal static Dictionary<string, string> GetCSSStyles(IFormattedText inline) {
        Dictionary<string, string> styles = new Dictionary<string, string>();
        if (inline.Italic.HasValue)
            styles.Add("font-style", inline.Italic.Value ? "italic" : "normal");
        if (inline.Bold.HasValue)
            styles.Add("font-weight", inline.Bold.Value ? "bold" : "normal");
        List<string> textDecorationLineValues = new List<string>(2);
        string textDecorationStyleValue = null;
        if (inline.Underline.HasValue) {
            textDecorationLineValues.Add(inline.Underline.Value == UnderlineValues2.None ? "none" : "underline");
            if (inline.Underline.Value == UnderlineValues2.None)
                textDecorationStyleValue = null;
            else if (inline.Underline.Value == UnderlineValues2.Solid)
                textDecorationStyleValue = "solid";
            else if (inline.Underline.Value == UnderlineValues2.Double)
                textDecorationStyleValue = "double";
            else if (inline.Underline.Value == UnderlineValues2.Dotted)
                textDecorationStyleValue = "dotted";
            else if (inline.Underline.Value == UnderlineValues2.Dashed)
                textDecorationStyleValue = "dashed";
            else if (inline.Underline.Value == UnderlineValues2.Wavy)
                textDecorationStyleValue = "wavy";
            else
                throw new Exception();
        }
        if (inline.Strikethrough.HasValue) {
            if (inline.Strikethrough.Value == StrikethroughValue.None) {
            } else if (inline.Strikethrough.Value == StrikethroughValue.Single) {
                textDecorationLineValues.Add("line-through");
                // textDecorationStyleValue = textDecorationStyleValue ?? "solid";
            } else if (inline.Strikethrough.Value == StrikethroughValue.Double) {
                textDecorationLineValues.Add("line-through");
                textDecorationStyleValue = textDecorationStyleValue ?? "double";
            } else {
                throw new Exception();
            }
        }
        if (textDecorationLineValues.Any()) {
            styles.Add("text-decoration-line", string.Join(' ', textDecorationLineValues));
            if (textDecorationStyleValue is not null)
                styles.Add("text-decoration-style", textDecorationStyleValue);
        }
        if (inline.Uppercase.HasValue)
            styles.Add("text-transform", inline.Uppercase.Value ? "uppercase" : "none");
        if (inline.SmallCaps.HasValue)
            styles.Add("font-variant", inline.SmallCaps.Value ? "small-caps" : "normal");
        if (inline.SuperSub is not null) {
            string key = "vertical-align";
            string value = inline.SuperSub switch {
                SuperSubValues.Superscript => "super",
                SuperSubValues.Subscript => "sub",
                SuperSubValues.Baseline => "baseline",
                _ => throw new Exception()
            };
            styles.Add(key, value);
            if (inline.SuperSub == SuperSubValues.Superscript || inline.SuperSub == SuperSubValues.Subscript) {
                styles.Add("font-size", "smaller");
            }
        }
        if (inline.FontName is not null && !string.IsNullOrEmpty(inline.FontName)) {
            string value = DOCX.CSS.ToFontFamily(inline.FontName);
            styles.Add("font-family", value);
        }
        if (inline.FontSizePt is not null) {
            styles["font-size"] = ConvertSize(inline.FontSizePt, "F1", "pt"); // Add or replace, b/c of Super/SubScript
        }
        if (inline.FontColor is not null) {
            string value = ConvertColor(inline.FontColor);
            styles.Add("color", value);
        }
        if (inline.BackgroundColor is not null) {
            string value = ConvertColor(inline.BackgroundColor);
            styles.Add("background-color", value);
        }
        return styles;
    }

    internal static Dictionary<string, string> GetCSSStyles(ICell cell) {
        Dictionary<string, string> styles = new Dictionary<string, string>();
        List<float?> borderWidths = new List<float?>(4) {
            cell.BorderTopWidthPt, cell.BorderRightWidthPt, cell.BorderBottomWidthPt, cell.BorderLeftWidthPt
        };
        List<CellBorderStyle?> borderStyles = new List<CellBorderStyle?>(4) {
            cell.BorderTopStyle, cell.BorderRightStyle, cell.BorderBottomStyle, cell.BorderLeftStyle
        };
        List<string> borderColors = new List<string>(4) {
            cell.BorderTopColor, cell.BorderRightColor, cell.BorderBottomColor, cell.BorderLeftColor
        };
        AddBorders(borderWidths, borderStyles, borderColors, styles);
        if (cell.BackgroundColor is not null)
            AddColor("background-color", cell.BackgroundColor, styles);
        if (cell.VAlignment is not null)
            AddEnum<VerticalAlignment>("vertical-align", cell.VAlignment, styles);
        return styles;
    }

    private static void Reduce4<T>(List<T> four) {
        if (EqualityComparer<T>.Default.Equals(four[3], four[1])) {
            four.RemoveAt(3);
            if (EqualityComparer<T>.Default.Equals(four[2], four[0])) {
                four.RemoveAt(2);
                if (EqualityComparer<T>.Default.Equals(four[1], four[0])) {
                    four.RemoveAt(1);
                    if (four[0] is null)
                        four.RemoveAt(0);
                }
            }
        }
    }

    internal static void AddBorders(List<float?> widths, List<CellBorderStyle?> styles, List<string> colors, Dictionary<string, string> css) {
        Reduce4(widths);
        Reduce4(styles);
        Reduce4(colors);
        if (widths.Count == 1 && styles.Count == 1 && colors.Count == 1) {
            string width = ConvertPoints(widths[0]);
            string style = ConvertEnum<CellBorderStyle>(styles[0]);
            string color = ConvertColor(colors[0]);
            string value;
            if (styles[0] == CellBorderStyle.None) {
                value = style;
            } else {
                value = width + " " + style + " " + color;
            }
            css.Add("border", value);
        } else if (widths.Count == 1 && styles.Count == 1 && colors.Count == 0) {
            string width = ConvertPoints(widths[0]);
            string style = ConvertEnum<CellBorderStyle>(styles[0]);
            string value;
            if (styles[0] == CellBorderStyle.None) {
                value = style;
            } else {
                value = width + " " + style;
            }
            css.Add("border", value);
        } else {
            AddBorderWidths(widths, css);
            AddBorderStyles(styles, css);
            AddBorderColors(colors, css);
        }
    }

    private static void AddBorderWidths(List<float?> borderWidths, Dictionary<string, string> styles) {
        if (!borderWidths.Any())
            return;
        if (borderWidths.Any(w => w is null)) {
            AddPoints("border-top-width", borderWidths[0], styles);
            if (borderWidths.Count > 1) // should be unnecessary
                AddPoints("border-right-width", borderWidths[1], styles);
            if (borderWidths.Count > 2)
                AddPoints("border-bottom-width", borderWidths[2], styles);
            if (borderWidths.Count > 3)
                AddPoints("border-left-width", borderWidths[3], styles);
        } else {
            styles.Add("border-width", string.Join(" ", borderWidths.Select(ConvertPoints)));
        }
    }

    private static void AddBorderStyles(List<CellBorderStyle?> borderStyles, Dictionary<string, string> styles) {
        if (!borderStyles.Any())
            return;
        if (borderStyles.Any(s => s is null)) {
            AddEnum<CellBorderStyle>("border-top-style", borderStyles[0], styles);
            if (borderStyles.Count > 1) // should be unnecessary
                AddEnum<CellBorderStyle>("border-right-style", borderStyles[1], styles);
            if (borderStyles.Count > 2)
                AddEnum<CellBorderStyle>("border-bottom-style", borderStyles[2], styles);
            if (borderStyles.Count > 3)
                AddEnum<CellBorderStyle>("border-left-style", borderStyles[3], styles);
        } else {
            styles.Add("border-style", string.Join(" ", borderStyles.Select(e => ConvertEnum<CellBorderStyle>(e))));
        }
    }

    private static void AddBorderColors(List<string> borderColors, Dictionary<string, string> styles) {
        if (!borderColors.Any())
            return;
        if (borderColors.Any(c => c is null)) {
            AddColor("border-top-color", borderColors[0], styles);
            if (borderColors.Count > 1) // should be unnecessary
                AddColor("border-right-color", borderColors[1], styles);
            if (borderColors.Count > 2)
                AddColor("border-bottom-color", borderColors[2], styles);
            if (borderColors.Count > 3)
                AddColor("border-left-color", borderColors[3], styles);
        } else {
            styles.Add("border-color", string.Join(" ", borderColors.Select(ConvertColor)));
        }
    }

    public static string ConvertSize(double? size, string format, string unit) {
        if (size is null)
            return null;
        return size.Value.ToString(format).TrimEnd('0').TrimEnd('.') + unit;
    }
    public static string ConvertSize(double? size, string unit) {
        return ConvertSize(size, "F2", unit);
    }

    private static string ConvertPoints(float? size) {
        return ConvertSize(size, "pt");
    }
    private static void AddPoints(string key, float? size, Dictionary<string, string> css) {
        if (size is null)
            return;
        string value = ConvertPoints(size);
        css.Add(key, value);
    }
    private static string ConvertEnum<T>(Enum e) where T : Enum {
        if (e is null)
            return null;
        return Enum.GetName(typeof(T), e).ToLower();
    }
    private static void AddEnum<T>(string key, Enum e, Dictionary<string, string> css) where T : Enum {
        if (e is null)
            return;
        var value = ConvertEnum<T>(e);
        css.Add(key, value);
    }
    internal static string ConvertColor(string color) {
        if (color is null)
            return null;
        if (color == "auto")
            return "initial";
        if (Regex.IsMatch(color, @"^[A-F0-9]{6}$"))
            return "#" + color;
        return color;
    }
    private static void AddColor(string key, string color, Dictionary<string, string> css) {
        if (color is null)
            return;
        string value = ConvertColor(color);
        // if (value is null)
        //     return;
        css.Add(key, value);
    }

    private static void AddTopBorderWidth(IBordered thing, Dictionary<string, string> css) {
        AddPoints("border-top-width", thing.BorderTopWidthPt, css);
    }
    private static void AddTopBorderStyle(IBordered thing, Dictionary<string, string> css) {
        AddEnum<CellBorderStyle>("border-top-style", thing.BorderTopStyle, css);
    }
    private static void AddTopBorderColor(IBordered thing, Dictionary<string, string> css) {
        AddColor("border-top-color", thing.BorderTopColor, css);
    }
    private static void AddRightBorderWidth(IBordered thing, Dictionary<string, string> css) {
        AddPoints("border-right-width", thing.BorderRightWidthPt, css);
    }
    private static void AddRightBorderStyle(IBordered thing, Dictionary<string, string> css) {
        AddEnum<CellBorderStyle>("border-right-style", thing.BorderRightStyle, css);
    }
    private static void AddRightBorderColor(IBordered thing, Dictionary<string, string> css) {
        AddColor("border-right-color", thing.BorderRightColor, css);
    }
    private static void AddBottomBorderWidth(IBordered thing, Dictionary<string, string> css) {
        AddPoints("border-bottom-width", thing.BorderBottomWidthPt, css);
    }
    private static void AddBottomBorderStyle(IBordered thing, Dictionary<string, string> css) {
        AddEnum<CellBorderStyle>("border-bottom-style", thing.BorderBottomStyle, css);
    }
    private static void AddBottomBorderColor(IBordered thing, Dictionary<string, string> css) {
        AddColor("border-bottom-color", thing.BorderBottomColor, css);
    }
    private static void AddLeftBorderWidth(IBordered thing, Dictionary<string, string> css) {
        AddPoints("border-left-width", thing.BorderLeftWidthPt, css);
    }
    private static void AddLeftBorderStyle(IBordered thing, Dictionary<string, string> css) {
        AddEnum<CellBorderStyle>("border-left-style", thing.BorderLeftStyle, css);
    }
    private static void AddLeftBorderColor(IBordered thing, Dictionary<string, string> css) {
        AddColor("border-left-color", thing.BorderLeftColor, css);
    }

}

}
