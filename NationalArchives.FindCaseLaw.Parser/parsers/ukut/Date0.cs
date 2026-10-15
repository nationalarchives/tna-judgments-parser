using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers;

internal class Date0
{
    private static readonly CultureInfo culture = new("en-GB");

    internal static WLine Enrich(WLine line, string name, int priority)
    {
        var enriched = EnrichLast1(line, name, priority);
        if (!ReferenceEquals(enriched, line))
        {
            return enriched;
        }

        enriched = EnrichLast2(line, name, priority);
        if (!ReferenceEquals(enriched, line))
        {
            return enriched;
        }

        enriched = EnrichLast3(line, name, priority);
        if (!ReferenceEquals(enriched, line))
        {
            return enriched;
        }

        enriched = EnrichWholeLine(line, name, priority);
        // if (!object.ReferenceEquals(enriched, line))
        return enriched;
        // return line;
    }

    internal static WLine EnrichLast1(WLine line, string name, int priority)
    {
        if (!line.Contents.Any())
        {
            return line;
        }

        if (line.Contents.Last() is not WText wText)
        {
            return line;
        }

        var pattern =
            @"(^| )(\d{1,2} (January|February|Feb|March|April|May|June|July|August|September|October|November|December) \d{4}) *$";
        var match = Regex.Match(wText.Text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return line;
        }

        var group = match.Groups[2];
        DateTime date;
        try
        {
            date = DateTime.Parse(group.Value, culture);
        }
        catch (FormatException)
        {
            // if day is 0
            return line;
        }

        Func<string, RunProperties, IInline> constructor = (text, props) =>
            new WDocDate(text, props, date) { Name = name, Priority = priority };
        var enriched = Helper.SplitOnGroup(wText, match.Groups[2], constructor);
        return WLine.Make(line, line.Contents.SkipLast(1).Concat(enriched));
    }

    internal static WLine EnrichLast2(WLine line, string name, int priority)
    {
        if (line.Contents.Count() < 2)
        {
            return line;
        }

        if (line.Contents.SkipLast(1).Last() is not WText wText1)
        {
            return line;
        }

        if (line.Contents.Last() is not WText wText2)
        {
            return line;
        }

        var normalized = Regex.Replace(wText1.Text + wText2.Text, @"\s+", " ").Trim();
        var pattern =
            @"^\d{1,2} (January|February|Feb|March|April|May|June|July|August|September|October|November|December) \d{4}$";
        var match = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return line;
        }

        var date = DateTime.Parse(match.Value, culture);
        var docDate = new WDocDate(line.Contents.TakeLast(2).Cast<WText>(), date) { Name = name, Priority = priority };
        return WLine.Make(line, line.Contents.SkipLast(2).Append(docDate));
    }

    internal static WLine EnrichLast3(WLine line, string name, int priority)
    {
        if (line.Contents.Count() < 3)
        {
            return line;
        }

        var last3 = line.Contents.TakeLast(3);
        if (last3.ElementAt(0) is not WText wText1)
        {
            return line;
        }

        if (last3.ElementAt(1) is not WText wText2)
        {
            return line;
        }

        if (last3.ElementAt(2) is not WText wText3)
        {
            return line;
        }

        var pattern1 = @"\d{1,2}$";
        var pattern2 = "^(st|nd|rd|th)$";
        var pattern3 =
            @"^ (January|February|Feb|March|April|May|June|July|August|September|October|November|December) \d{4}$";
        var match1 = Regex.Match(wText1.Text, pattern1);
        if (!match1.Success)
        {
            return line;
        }

        var match2 = Regex.Match(wText2.Text, pattern2);
        if (!match2.Success)
        {
            return line;
        }

        var match3 = Regex.Match(wText3.Text, pattern3, RegexOptions.IgnoreCase);
        if (!match3.Success)
        {
            return line;
        }

        var date = DateTime.Parse(match1.Value + match3.Value, culture);
        IInline[] replacement;
        if (match1.Index == 0)
        {
            WText[] contents =
            [
                wText1,
                wText2,
                wText3
            ];
            var docDate = new WDocDate(contents, date) { Name = name, Priority = priority };
            replacement = [docDate];
        }
        else
        {
            var split = wText1.Split(match1.Index);
            WText[] contents =
            [
                split.Item2,
                wText2,
                wText3
            ];
            var docDate = new WDocDate(contents, date) { Name = name, Priority = priority };
            replacement = [split.Item1, docDate];
        }

        return WLine.Make(line, line.Contents.SkipLast(3).Concat(replacement));
    }

    internal static WLine EnrichWholeLine(WLine line, string name, int priority)
    {
        if (!line.Contents.All(inline => inline is WText))
        {
            return line;
        }

        var normalized = line.NormalizedContent;
        var pattern =
            @"^\d{1,2} (January|February|Feb|March|April|May|June|July|August|September|October|November|December) \d{4}$";
        var match = Regex.Match(normalized, pattern);
        if (!match.Success)
        {
            return line;
        }

        var date = DateTime.Parse(normalized, culture);
        var docDate = new WDocDate(line.Contents.Cast<WText>(), date) { Name = name, Priority = priority };
        return WLine.Make(line, [docDate]);
    }
}
