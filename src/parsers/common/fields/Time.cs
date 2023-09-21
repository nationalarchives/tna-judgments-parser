

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

internal class Time {

    internal static bool Is(string fieldCode) {
        return fieldCode.StartsWith(" TIME ");
    }

    private static string pattern = @"^ TIME \\@ ""([^""]+)"" $";

    [Obsolete]
    internal static IEnumerable<IInline> Parse(MainDocumentPart main, string fieldCode, IEnumerable<OpenXmlElement> rest) {
        Match match = Regex.Match(fieldCode, pattern);
        if (!match.Success)
            throw new Exception();
        string format = match.Groups[1].Value;
        switch (format) {
            case "dddd d MMMM yyyy":    // EWHC/Ch/2005/2793
            case "d MMMM yyyy":         // EWCA/Civ/2006/1103
                return DateOnly(main, rest);
            default:
                throw new Exception();
        }
    }

    [Obsolete]
    private static IEnumerable<IInline> DateOnly(MainDocumentPart main, IEnumerable<OpenXmlElement> rest) {
        IEnumerable<IInline> parsed = Fields.Rest(main, rest);
        if (parsed.All(inline => inline is IFormattedText)) {
            string content = IInline.ToString(parsed);
            try {
                CultureInfo culture = new CultureInfo("en-GB");
                DateTime date = DateTime.Parse(content, culture);
                WDate wDate = new WDate(parsed.Cast<IFormattedText>(), date);
                return new List<IInline>(1) { wDate };
            } catch (FormatException) {
            }
        }
        return parsed;
    }

    internal static List<IInline> Parse(string fieldCode, List<IInline> contents) {
        Match match = Regex.Match(fieldCode, pattern);
        if (!match.Success)
            throw new Exception();
        string format = match.Groups[1].Value;
        switch (format) {
            case "dddd d MMMM yyyy":    // EWHC/Ch/2005/2793
            case "d MMMM yyyy":         // EWCA/Civ/2006/1103
                return ConvertDate(contents);
            default:
                throw new Exception();
        }
    }

    internal static List<IInline> ConvertDate(List<IInline> contents) {
        if (!contents.All(inline => IFormattedText.IsFormattedTextAndNothingElse(inline)))
            return contents;
        DateTime date;
        try {
            string content = IInline.ToString(contents);
            CultureInfo culture = new CultureInfo("en-GB");
            date = DateTime.Parse(content, culture);
        } catch (FormatException) {
            return contents;
        }
        WDate wDate = new WDate(contents.Cast<IFormattedText>(), date);
        return new List<IInline>(1) { wDate };
    }

}

}
