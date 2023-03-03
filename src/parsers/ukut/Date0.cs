
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers {

class Date0 {

    static CultureInfo culture = new CultureInfo("en-GB");

    internal static WLine Enrich(WLine line, string name, int priority) {
        WLine enriched = EnrichLast1(line, name, priority);
        if (!object.ReferenceEquals(enriched, line))
            return enriched;
        enriched = EnrichLast2(line, name, priority);
        if (!object.ReferenceEquals(enriched, line))
            return enriched;
        enriched = EnrichLast3(line, name, priority);
        if (!object.ReferenceEquals(enriched, line))
            return enriched;
        enriched = EnrichWholeLine(line, name, priority);
        // if (!object.ReferenceEquals(enriched, line))
            return enriched;
        // return line;
    }

    internal static  WLine EnrichLast1(WLine line, string name, int priority) {
        if (!line.Contents.Any())
            return line;
        if (line.Contents.Last() is not WText wText)
            return line;
        string pattern = @"(^| )(\d{1,2} (January|February|Feb|March|April|May|June|July|August|September|October|November|December) \d{4}) *$";
        Match match = Regex.Match(wText.Text, pattern);
        if (!match.Success)
            return line;
        Group group = match.Groups[2];
        DateTime date;
        try {
            date = DateTime.Parse(group.Value, culture);
        } catch (FormatException) { // if day is 0
            return line;
        }
        Func<string, RunProperties, IInline> constructor = (text, props) => new WDocDate(text, props, date) { Name = name, Priority = priority };
        List<IInline> enriched = Helper.SplitOnGroup(wText, match.Groups[2], constructor);
        return WLine.Make(line, Enumerable.Concat(line.Contents.SkipLast(1), enriched));
    }

    internal static WLine EnrichLast2(WLine line, string name, int priority) {
        if (line.Contents.Count() < 2)
            return line;
        if (line.Contents.SkipLast(1).Last() is not WText wText1)
            return line;
        if (line.Contents.Last() is not WText wText2)
            return line;
        string normalized = Regex.Replace(wText1.Text + wText2.Text, @"\s+", " ").Trim();
        string pattern = @"^\d{1,2} (January|February|Feb|March|April|May|June|July|August|September|October|November|December) \d{4}$";
        Match match = Regex.Match(normalized, pattern);
        if (!match.Success)
            return line;
        DateTime date = DateTime.Parse(match.Value, culture);
        WDocDate docDate = new WDocDate(line.Contents.TakeLast(2).Cast<WText>(), date) { Name = name, Priority = priority };
        return WLine.Make(line, line.Contents.SkipLast(2).Append(docDate));
    }

    internal static WLine EnrichLast3(WLine line, string name, int priority) {
        if (line.Contents.Count() < 3)
            return line;
        IEnumerable<IInline> last3 = line.Contents.TakeLast(3);
        if (last3.ElementAt(0) is not WText wText1)
            return line;
        if (last3.ElementAt(1) is not WText wText2)
            return line;
        if (last3.ElementAt(2) is not WText wText3)
            return line;
        string pattern1 = @"\d{1,2}$";
        string pattern2 = @"^(st|nd|rd|th)$";
        string pattern3 = @"^ (January|February|Feb|March|April|May|June|July|August|September|October|November|December) \d{4}$";
        Match match1 = Regex.Match(wText1.Text, pattern1);
        if (!match1.Success)
            return line;
        Match match2 = Regex.Match(wText2.Text, pattern2);
        if (!match2.Success)
            return line;
        Match match3 = Regex.Match(wText3.Text, pattern3);
        if (!match3.Success)
            return line;
        DateTime date = DateTime.Parse(match1.Value + match3.Value, culture);
        List<IInline> replacement;
        if (match1.Index == 0) {
            List<WText> contents = new List<WText>(3) { wText1, wText2, wText3 };
            WDocDate docDate = new WDocDate(contents, date) { Name = name, Priority = priority };
            replacement = new List<IInline>(1) { docDate };
        } else {
            Tuple<WText, WText> split = wText1.Split(match1.Index);
            List<WText> contents = new List<WText>(3) { split.Item2, wText2, wText3 };
            WDocDate docDate = new WDocDate(contents, date) { Name = name, Priority = priority };
            replacement = new List<IInline>(2) { split.Item1, docDate };
        }
        return WLine.Make(line, Enumerable.Concat(line.Contents.SkipLast(3), replacement));
    }

    internal static WLine EnrichWholeLine(WLine line, string name, int priority) {
        if (!line.Contents.All(inline => inline is WText))
            return line;
        string normalized = line.NormalizedContent;
        string pattern = @"^\d{1,2} (January|February|Feb|March|April|May|June|July|August|September|October|November|December) \d{4}$";
        Match match = Regex.Match(normalized, pattern);
        if (!match.Success)
            return line;
        DateTime date = DateTime.Parse(normalized, culture);
        WDocDate docDate = new WDocDate(line.Contents.Cast<WText>(), date) { Name = name, Priority = priority };
        List<IInline> contents = new List<IInline>(1) { docDate };
        return WLine.Make(line, contents);
    }

}

}
