
using System;
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
        // CultureInfo culture = new CultureInfo("en-GB");
        CultureInfo culture = new CultureInfo("en-US");
        // NumberFormatInfo format = culture.NumberFormat;
        Func<string, double> parse = (num) => {
            num = Regex.Replace(num, @"\s+", " ").Trim();
            return double.Parse(num, NumberStyles.Currency, culture);
        };
        Func<double, string> format = num => num.ToString("c", culture);
        double sum = SumAbove(cell, parse);
        if (sum == 0d)
            throw new Exception();
        return format(sum);
    }

    internal static CellBorderStyle ExtractBorderStyle(BorderType border) {
        var x = border?.Val ?? BorderValues.None;
        if (x == BorderValues.None)
            return CellBorderStyle.None;
        if (x == BorderValues.Dotted)
            return CellBorderStyle.Dotted;
        if (x == BorderValues.Dashed)
            return CellBorderStyle.Dashed;
        if (x == BorderValues.Double)
            return CellBorderStyle.Double;
        return CellBorderStyle.Solid;
    }

    internal static float? ExtractBorderWidthPt(BorderType border) {
        var size = border?.Size;
        if (size is null)
            return null;
        return size / 8f;
    }

}

}
