
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace UK.Gov.Legislation.Judgments.Parse {

class DocDate : Enricher {

    private static readonly CultureInfo culture = new CultureInfo("en-GB");

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        if (!line.Any())
            return Enumerable.Empty<IInline>();
        if (line.Count() == 1)
            return Enrich1(line.First());
        if (line.Count() == 3) {
            IInline first = line.First();
            IInline second = line.ElementAt(1);
            IInline third = line.ElementAt(2);
            if (first is WText fText1) {
                if (second is WText fText2) {
                    if (third is WText fText3) {
                        string pattern1 = @"((Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday),? )?(\d{1,2})$";
                        string pattern2 = @"^(st|nd|rd|th)$";
                        string pattern3 = @"^ (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$";
                        Match match1 = Regex.Match(fText1.Text, pattern1);
                        Match match2 = Regex.Match(fText2.Text, pattern2);
                        Match match3 = Regex.Match(fText3.Text, pattern3);
                        if (match1.Success && match2.Success && match3.Success) {
                            string dayMonthYear = match1.Groups[3].Value + fText3.Text; // exclude day of the week, in case it doesn't match (EWHC/Admin/2018/1074)
                            DateTime dt = DateTime.Parse(dayMonthYear, culture);
                            if (match1.Index == 0) {
                                return new IInline[] { new WDocDate(line.Cast<WText>(), dt) };
                            } else {
                                string split1 = fText1.Text.Substring(0, match1.Index);
                                string split2 = fText1.Text.Substring(match1.Index);
                                WText before = new WText(split1, fText1.properties);
                                WText within1 = new WText(split2, fText1.properties);
                                IFormattedText[] within = { within1, fText2, fText3 };
                                return new IInline[] { before, new WDocDate(within, dt) };
                            }
                        }
                        /* difference here is only spacing */
                        pattern2 = @"^(st|nd|rd|th) +$";
                        pattern3 = @"^(January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$";
                        match2 = Regex.Match(fText2.Text, pattern2);
                        match3 = Regex.Match(fText3.Text, pattern3);
                        if (match1.Success && match2.Success && match3.Success) {
                            string dayMonthYear = match1.Groups[3].Value + fText3.Text; // exclude day of the week, in case it doesn't match (EWHC/Admin/2018/1074)
                            DateTime dt = DateTime.Parse(dayMonthYear, culture);
                            if (match1.Index == 0) {
                                return new IInline[] { new WDocDate(line.Cast<WText>(), dt) };
                            } else {
                                string split1 = fText1.Text.Substring(0, match1.Index);
                                string split2 = fText1.Text.Substring(match1.Index);
                                WText before = new WText(split1, fText1.properties);
                                WText within1 = new WText(split2, fText1.properties);
                                IFormattedText[] within = { within1, fText2, fText3 };
                                return new IInline[] { before, new WDocDate(within, dt) };
                            }
                        }
                    }
                }
            }
        }
        // if (line.Count() >= 2) {
            IEnumerable<IInline> leadUp = line.SkipLast(1);
            IInline last = line.Last();
            IEnumerable<IInline> rest = Enrich1(last);
            return leadUp.Concat(rest);
            // string pattern1 = @"^(On|Date of Judgment):$";
            // string pattern3 = @"^\d{1,2} (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$";
            // int i = line.Count() - 2;
            // IInline first = line.ElementAt(i);
            // while (i > 0 && first is WTab) {
            //     i -= 1;
            //     first = line.ElementAt(i);
            // }
            // if (first is WText fText1 && last is WText fText3) {
            //     Match match1 = Regex.Match(fText1.Text.Trim(), pattern1);
            //     Match match3 = Regex.Match(fText3.Text.Trim(), pattern3);
            //     if (match1.Success && match3.Success) {
            //         DateTime date = DateTime.Parse(fText3.Text, culture);
            //         WDocDate docDate =  new WDocDate(new List<WText>(1) { fText3 }, date);
            //         return line.Take(line.Count() - 1).Append(docDate);
            //     }
            // }
        // }
        // return line;
    }


    /* one */

    private static readonly string[] cardinalDatePatterns1 = {
        @"^(\s*Date: +)?\d{2}/\d{2}/\d{4}( *)$",
        @"^(\s*Date: +)?\d{1,2} (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}( *)$"
    };
    private static readonly string[] ordinalDatePatterns1 = {
        @"^(\s*Date: +)?(Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday),? (\d{1,2})(st|nd|rd|th)? (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}( *)$"
    };

    private List<IInline> EnrichText(WText fText) {
        foreach (string pattern in cardinalDatePatterns1) {
            Match match = Regex.Match(fText.Text, pattern);
            if (match.Success) {
                // int start = match.Groups[0].Index + match.Groups[1].Length;
                Group before = match.Groups[1];
                Group after = match.Groups[match.Groups.Count-1];
                // string main = fText.Text.Substring(start, after.Index - start);
                string main = fText.Text.Substring(before.Length, after.Index - before.Length);
                DateTime dt = DateTime.Parse(main, culture);
                WDocDate dd = new WDocDate(main, fText.properties, dt);
                List<IInline> contents = new List<IInline>(3);
                if (before.Length > 0)
                    contents.Add(new WText(before.Value, fText.properties));
                contents.Add(dd);
                if (after.Length > 0)
                    contents.Add(new WText(after.Value, fText.properties));
                return contents;
            }
        }
        foreach (string pattern in ordinalDatePatterns1) {
            Match match = Regex.Match(fText.Text, pattern);
            if (match.Success) {
                // int start = match.Groups[0].Index + match.Groups[1].Length;
                Group before = match.Groups[1];
                Group after = match.Groups[match.Groups.Count-1];
                string main = fText.Text.Substring(before.Length, after.Index - before.Length);
                string cardinal = match.Groups[3].Value + " " + fText.Text.Substring(match.Groups[5].Index);
                DateTime dt = DateTime.Parse(cardinal, culture);
                WDocDate dd = new WDocDate(main, fText.properties, dt);
                List<IInline> contents = new List<IInline>(3);
                if (before.Length > 0)
                    contents.Add(new WText(before.Value, fText.properties));
                contents.Add(dd);
                if (after.Length > 0)
                    contents.Add(new WText(after.Value, fText.properties));
                return contents;
            }
        }
        return new List<IInline>(1) { fText };
    }

    private IEnumerable<IInline> Enrich1(IInline inline) {
        if (inline is WDate wDate)
            return new IInline[] { new WDocDate(wDate) };
        if (inline is WText fText)
            return EnrichText(fText);
        return new IInline[] { inline };
    }

}

}
