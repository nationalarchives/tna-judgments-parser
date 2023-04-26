
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX {

class Tables {

    private static int GetI(TableCell cell) {
        int i = 0;
        TableCell previous = cell.PreviousSibling<TableCell>();
        while (previous is not null) {
            i += 1;
            previous = previous.PreviousSibling<TableCell>();
        }
        return i;
    }

    internal static double SumAbove(TableCell anchor, Func<string, double> parse) {
        double value = 0;
        int i = GetI(anchor);
        TableRow row = anchor.Ancestors<TableRow>().First().PreviousSibling<TableRow>();
        while (row is not null) {
            TableCell above = row.ChildElements.OfType<TableCell>().Skip(i).First();
            try {
                double v = parse(above.InnerText);
                value += v;
            } catch (FormatException) {
                // System.Console.WriteLine(e);
            }
            row = row.PreviousSibling<TableRow>();
        }
        return value;
    }

    internal static string SumAbove(TableCell cell) {
        Func<string, double> parse = (num) => {
            num = Regex.Replace(num, @"\s+", " ").Trim();
            return double.Parse(num);  // TODO what if currency?
        };
        Func<double, string> format = num => num.ToString();  // TODO what if currency?
        double sum = SumAbove(cell, parse);
        if (sum == 0d)
            throw new Exception();
        return format(sum);
    }

    internal static float? ExtractBorderWidthPt(BorderType border) {
        var size = border?.Size;
        if (size is null)
            return null;
        return size / 8f;
    }

    internal static CellBorderStyle? ExtractBorderStyle(BorderType border) {
        var style = border?.Val;
        if (style is null)
            return null;
        if (style == BorderValues.Nil)
            return CellBorderStyle.None;
        if (style == BorderValues.None)
            return CellBorderStyle.None;
        if (style == BorderValues.Dotted)
            return CellBorderStyle.Dotted;
        if (style == BorderValues.Dashed)
            return CellBorderStyle.Dashed;
        if (style == BorderValues.Double)
            return CellBorderStyle.Double;
        return CellBorderStyle.Solid;
    }

    internal static string ExtractBorderColor(BorderType border) {
        return border?.Color?.Value;
    }

    internal static List<float> GetColumnWidthsIns(TableGrid grid) {
        return grid.ChildElements.OfType<GridColumn>()
            .Select(c => c.Width)
            .Where(w => w.HasValue)
            .Select(w => float.Parse(w) / 1440f)
            .ToList();
    }

}

}
