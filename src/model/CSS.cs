
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
            styles["font-size"] = inline.FontSizePt + "pt"; // Add or replace, b/c of Super/SubScript
        }
        if (inline.FontColor is not null) {
            string value = inline.FontColor;
            if (value == "auto")
                value = "initial";
            else if (Regex.IsMatch(value, @"^[A-F0-9]{6}$"))
                value = "#" + value;
            styles.Add("color", value);
        }
        if (inline.BackgroundColor is not null) {
            string value = inline.BackgroundColor;
            if (value == "auto")
                value = "initial";
            else if (Regex.IsMatch(value, @"^[A-F0-9]{6}$"))
                value = "#" + value;
            styles.Add("background-color", value);
        }
        return styles;
    }

    internal static Dictionary<string, string> GetCSSStyles(ICell cell) {
        Dictionary<string, string> styles = new Dictionary<string, string>();

        List<CellBorderStyle> borderStyles;
        if (cell.BorderLeftStyle != cell.BorderRightStyle)
            borderStyles = new List<CellBorderStyle>(4) { cell.BorderTopStyle, cell.BorderRightStyle, cell.BorderBottomStyle, cell.BorderLeftStyle };
        else if (cell.BorderBottomStyle != cell.BorderTopStyle)
            borderStyles = new List<CellBorderStyle>(3) { cell.BorderTopStyle, cell.BorderRightStyle, cell.BorderBottomStyle };
        else if (cell.BorderRightStyle != cell.BorderTopStyle)
            borderStyles = new List<CellBorderStyle>(2) { cell.BorderTopStyle, cell.BorderRightStyle };
        else if (cell.BorderTopStyle != CellBorderStyle.None)
            borderStyles = new List<CellBorderStyle>(1) { cell.BorderTopStyle };
        else
            borderStyles = new List<CellBorderStyle>(0);
        IEnumerable<string> borderStyles2 = borderStyles.Select(s => Enum.GetName(typeof(CellBorderStyle), s).ToLower());
        if (borderStyles2.Any())
            styles.Add("border-style", string.Join(" ", borderStyles2));

        float top = cell.BorderTopWidthPt ?? 0;
        float right = cell.BorderRightWidthPt ?? 0;
        float bottom = cell.BorderBottomWidthPt ?? 0;
        float left = cell.BorderLeftWidthPt ?? 0;
        List<float> borderWidths;
        if (left != right)
            borderWidths = new List<float>(4) { top, right, bottom, left };
        else if (bottom != top)
            borderWidths = new List<float>(3) { top, right, bottom };
        else if (right != top)
            borderWidths = new List<float>(2) { top, right };
        else if (top != 0)
            borderWidths = new List<float>(1) { top };
        else
            borderWidths = new List<float>(0);
        IEnumerable<string> borderWidths2 = borderWidths.Select(w => w + "pt");
        if (borderWidths2.Any())
            styles.Add("border-width", string.Join(" ", borderWidths2));

        string topC = cell.BorderTopColor ?? "auto";
        string rightC = cell.BorderRightColor ?? "auto";
        string bottomC = cell.BorderBottomColor ?? "auto";
        string leftC = cell.BorderLeftColor ?? "auto";
        IEnumerable<string> borderColors;
        if (leftC != rightC)
            borderColors = new List<string>(4) { topC, rightC, bottomC, leftC };
        else if (bottomC != topC)
            borderColors = new List<string>(3) { topC, rightC, bottomC };
        else if (rightC != topC)
            borderColors = new List<string>(2) { topC, rightC };
        else if (topC != "auto")
            borderColors = new List<string>(1) { topC };
        else
            borderColors = new List<string>(0);
        borderColors = borderColors.Select(c => {
            if (c == "auto")
                return "initial";
            if (Regex.IsMatch(c, @"^[A-F0-9]{6}$"))
                return "#" + c;
            return c;
        });
        if (borderColors.Any())
            styles.Add("border-color", string.Join(" ", borderColors));

        return styles;
    }

}

}