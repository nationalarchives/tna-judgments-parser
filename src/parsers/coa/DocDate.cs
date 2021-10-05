
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace UK.Gov.Legislation.Judgments.Parse {

class DocDate : Enricher {

    override internal IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>();
        bool found = false;
        foreach (IBlock block in blocks) {
            if (found) {
                enriched.Add(block);
            } else {
                IBlock e1 = EnrichOrDefault(block);
                if (e1 is null) {
                    enriched.Add(block);
                } else {
                    enriched.Add(e1);
                    found = true;
                }
            }
        }
        return enriched;
    }

    private IBlock EnrichOrDefault(IBlock block) {
        if (block is not WLine line)
            return block;
        return EnrichOrDefault(line);
    }

    private WLine EnrichOrDefault(WLine line) {
        if (!line.Contents.Any())
            return null;
        if (line.Contents.Count() == 1)
            return Enrich1OrDefault(line);
        if (line.Contents.Count() == 2)
            return Enrich2OrDefault(line);
        if (line.Contents.Count() == 3)
            return Enrich3OrDefault(line);
        return null;
    }

    private static readonly CultureInfo culture = new CultureInfo("en-GB");

    private WLine Enrich1OrDefault(WLine line) {
        IInline first = line.Contents.First();
        if (first is not WText text)
            return null;
        List<IInline> contents = EnrichText(text);
        if (contents is null)
            return null;
        return new WLine(line, contents);
    }

    private WLine Enrich2OrDefault(WLine line) {
        IInline first = line.Contents.First();
        IInline second = line.Contents.ElementAt(1);
        if (first is not WText fText1)
            return null;
        if (!Regex.IsMatch(fText1.Text, @"^Date: $"))
            return null;
        if (second is not WText fText2)
            return null;
        List<IInline> enriched = EnrichText(fText2);
        if (enriched is null)
            return null;
        return new WLine(line, enriched.Prepend(first));
    }

    private WLine Enrich3OrDefault(WLine line) {
        IInline first = line.Contents.First();
        IInline second = line.Contents.ElementAt(1);
        IInline third = line.Contents.ElementAt(2);
            if (first is WText fText1) {
                /* this needs to be improved, not everyting before the date should be allowed */
                /* EWCA/Civ/2011/1277 contains 'hearing' */
                bool isMain = !fText1.Text.Contains("hearing", StringComparison.InvariantCultureIgnoreCase);
                if (second is WText fText2) {
                    if (third is WText fText3) {
                        string pattern1 = @"((Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday),? +)?(\d{1,2})$";
                        string pattern2 = @"^(st|nd|rd|th)$";
                        string pattern3 = @"^ +(January|February|March|April|May|June|July|August|September|October|November|December),? +\d{4}$";    // comma after month in EWHC/Admin/2018/2410
                        Match match1 = Regex.Match(fText1.Text, pattern1);
                        Match match2 = Regex.Match(fText2.Text, pattern2, RegexOptions.IgnoreCase);
                        Match match3 = Regex.Match(fText3.Text, pattern3);
                        if (match1.Success && match2.Success && match3.Success) {
                            string dayMonthYear = match1.Groups[3].Value + fText3.Text; // exclude day of the week, in case it doesn't match (EWHC/Admin/2018/1074)
                            DateTime dt = DateTime.Parse(dayMonthYear, culture);
                            if (match1.Index == 0) {
                                IEnumerable<IInline> contents = new IInline[] { new WDocDate(line.Contents.Cast<WText>(), dt) };
                                return new WLine(line, contents);
                            } else {
                                string split1 = fText1.Text.Substring(0, match1.Index);
                                string split2 = fText1.Text.Substring(match1.Index);
                                WText before = new WText(split1, fText1.properties);
                                WText within1 = new WText(split2, fText1.properties);
                                IFormattedText[] within = { within1, fText2, fText3 };
                                IInline date = isMain ? new WDocDate(within, dt) : new WDate(within, dt);
                                IEnumerable<IInline> contents = new IInline[] { before, date };
                                return new WLine(line, contents);
                            }
                        }
                        /* difference here is only spacing */ // EWHC/Fam/2014/1768, EWCA/Civ/2003/1048
                        pattern3 = @"^ +(January|February|March|April|May|June|July|August|September|October|November|December) \d{4}( *)$";
                        match1 = Regex.Match(fText1.Text, pattern1);
                        match2 = Regex.Match(fText2.Text, pattern2, RegexOptions.IgnoreCase);
                        match3 = Regex.Match(fText3.Text, pattern3);
                        if (match1.Success && match2.Success && match3.Success) {
                            string dayMonthYear = match1.Groups[3].Value + fText3.Text;
                            DateTime dt = DateTime.Parse(dayMonthYear, culture);
                            List<IInline> everything = new List<IInline>();
                            if (match1.Index > 0) {
                                string before1 = fText1.Text.Substring(0, match1.Index);
                                WText before2 = new WText(before1, fText1.properties);
                                everything.Add(before2);
                            }
                            string within1 = fText1.Text.Substring(match1.Index);
                            WText within1bis = new WText(within1, fText1.properties);
                            if (match3.Groups[2].Length == 0) {
                                IFormattedText[] dateContents = { within1bis, fText2, fText3 };
                                IInline date = isMain ? new WDocDate(dateContents, dt) : new WDate(dateContents, dt);
                                everything.Add(date);
                            } else {
                                string within3 = fText3.Text.Substring(0, match3.Groups[2].Index);
                                string after1 = fText3.Text.Substring(match3.Groups[2].Index);
                                WText within3bis = new WText(within3, fText3.properties);
                                IFormattedText[] dateContents = { within1bis, fText2, within3bis };
                                IInline date = isMain ? new WDocDate(dateContents, dt) : new WDate(dateContents, dt);
                                everything.Add(date);
                                WText after1bis = new WText(after1, fText3.properties);
                                everything.Add(after1bis);
                            }
                            return new WLine(line, everything);
                        }
                        /* difference here is only spacing */
                        pattern2 = @"^(st|nd|rd|th)? +$";   // only space in EWHC/TCC/2015/412
                        pattern3 = @"^(January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$";
                        match2 = Regex.Match(fText2.Text, pattern2, RegexOptions.IgnoreCase);
                        match3 = Regex.Match(fText3.Text, pattern3);
                        if (match1.Success && match2.Success && match3.Success) {
                            string dayMonthYear = match1.Groups[3].Value + fText3.Text; // exclude day of the week, in case it doesn't match (EWHC/Admin/2018/1074)
                            DateTime dt = DateTime.Parse(dayMonthYear, culture);
                            if (match1.Index == 0) {
                                IEnumerable<IInline> contents = new IInline[] { new WDocDate(line.Contents.Cast<WText>(), dt) };
                                return new WLine(line, contents);
                            } else {
                                string split1 = fText1.Text.Substring(0, match1.Index);
                                string split2 = fText1.Text.Substring(match1.Index);
                                WText before = new WText(split1, fText1.properties);
                                WText within1 = new WText(split2, fText1.properties);
                                IFormattedText[] within = { within1, fText2, fText3 };
                                IInline date = isMain ? new WDocDate(within, dt) : new WDate(within, dt);
                                IEnumerable<IInline> contents = new IInline[] { before, date };
                                return new WLine(line, contents);
                            }
                        }
                    }
                }
            }
        return null;
        // }
        // /* this recursively tries the tail */ /* EWHC/Ch/2013/3866 */
        // /* problem is that the isMain variable will be unreliable: EWCA/Civ/2004/1067 */
        // IEnumerable<IInline> rest = line.Skip(1);
        // return Enrich(rest).Prepend(first);
    }


    /* one */

    private static readonly string[] cardinalDatePatterns1 = {
        @"^(\s*Date: *)?\d{1,2}/\d{1,2}/\d{4}( *)$",
        @"^(\s*Date:? *)?\d{1,2}\.\d{1,2}\.\d{4}( *)$",
        /* add other month abbreviations */
        @"^(\s*Date ?:? *)?\d{1,2} (January|February|Feb|March|April|May|June|July|August|September|October|November|December),? \d{4}( *)$"   // comma after month in EWHC/Ch/2003/812
    };

    private static readonly string[] cardinalDatePatterns2 = {
        @"^Date\|: ((Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday), (\d{1,2} +(January|February|March|April|May|June|July|August|September|October|November|December) \d{4}))$"
    };

    private static readonly string[] twoDigitYearCardinalDatePatterns = {
        @"^Date: (\d{1,2}/\d{1,2}/\d{2})$"
    };
    private static readonly string[] ordinalDatePatterns1 = {
        @"^(\s*Date: *)?(Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday),? +(\d{1,2})(st|nd|rd|th)? +(January|February|March|April|May|June|July|August|September|October|November|December) +\d{4}( *)$"
    };
    private static readonly string[] ordinalDatePatterns2 = {   // mistake in EWHC/Fam/2010/64
        @"^Date: (Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday), (Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday), (\d{1,2})(st|nd|rd|th)? (January|February|March|April|May|June|July|August|September|October|November|December) (\d{4})( *)$",
    };
    private static readonly string[] ordinalDatePatterns3 = {   // EWHC/Admin/2014/1564
        @"^(\s*Date: *)?(\d{1,2})(st|nd|rd|th) +(January|February|March|April|May|June|July|August|September|October|November|December) +\d{4}( *)$"
    };

    private static readonly string[] doubleCardinalDatePatterns = { // EWCA/Civ/2003/607
        @"^((Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday) \d{1,2} (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}) and ((Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday) \d{1,2} (January|February|March|April|May|June|July|August|September|October|November|December) \d{4})$"
    };

    private static List<IInline> Split(WText wText, Group display, Group parse) {
        List<IInline> contents = new List<IInline>();
        string text = wText.Text;
        if (display.Index > 0) {
            string s = text.Substring(0, display.Index);
            WText label = new WText(s, wText.properties);
            contents.Add(label);
        }
        DateTime dt = DateTime.Parse(parse.Value, culture);
        WDocDate docDate = new WDocDate(display.Value, wText.properties, dt);
        contents.Add(docDate);
        int end = display.Index + display.Length;
        if (end < text.Length) {
            string rest1 = text.Substring(end, text.Length - end);
            WText rest2 = new WText(rest1, wText.properties);
            contents.Add(rest2);
        }
        return contents;
    }

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
        foreach (string pattern in cardinalDatePatterns2) {
            Match match = Regex.Match(fText.Text, pattern);
            if (match.Success)
                return Split(fText, match.Groups[1], match.Groups[3]);
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
        foreach (string pattern in ordinalDatePatterns2) {  // error in EWHC/Fam/2010/64
            Match match = Regex.Match(fText.Text, pattern);
            if (match.Success) {
                int start = match.Groups[3].Index;
                int end = match.Groups[6].Index + match.Groups[6].Length;
                string before = fText.Text.Substring(0, start);
                string main = fText.Text.Substring(start, end - start);
                string after = fText.Text.Substring(end);
                string cardinal = match.Groups[3].Value + " " + match.Groups[5].Value + " " + match.Groups[6].Value;
                DateTime dt = DateTime.Parse(cardinal, culture);
                List<IInline> contents = new List<IInline>(3);
                contents.Add(new WText(before, fText.properties));
                contents.Add(new WDocDate(main, fText.properties, dt));
                if (after.Length > 0)
                    contents.Add(new WText(after, fText.properties));
                return contents;
            }
        }
        foreach (string pattern in ordinalDatePatterns3) {
            Match match = Regex.Match(fText.Text, pattern);
            if (match.Success) {
                Group before = match.Groups[1];
                Group after = match.Groups[match.Groups.Count-1];
                string main = fText.Text.Substring(before.Length, after.Index - before.Length);
                string cardinal = match.Groups[2].Value + " " + fText.Text.Substring(match.Groups[4].Index);
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
        foreach (string pattern in doubleCardinalDatePatterns) {
            Match match = Regex.Match(fText.Text, pattern);
            if (match.Success) {
                Group first = match.Groups[1];
                Group second = match.Groups[4];
                string betweenText = fText.Text.Substring(first.Length, second.Index - first.Length);
                DateTime firstDate = DateTime.Parse(first.Value, culture);
                DateTime secondDate = DateTime.Parse(second.Value, culture);
                return new List<IInline>(3) {
                    new WDocDate(first.Value, fText.properties, firstDate),
                    new WText(betweenText, fText.properties),
                    new WDocDate(second.Value, fText.properties, secondDate)
                };
            }
        }
        foreach (string pattern in twoDigitYearCardinalDatePatterns) {
            Match match = Regex.Match(fText.Text, pattern);
            if (match.Success) {
                Group group = match.Groups[1];
                string label = fText.Text.Substring(0, group.Index);
                string dateWithTwoDigitYear = group.Value;
                int i = dateWithTwoDigitYear.LastIndexOf('/');
                int twoDigitYear = int.Parse(dateWithTwoDigitYear.Substring(i + 1));
                int thisYear = DateTime.Now.Year;
                int lastTwoDigitsOfThisYear = thisYear % 100;
                int century;
                if (twoDigitYear <= lastTwoDigitsOfThisYear)
                    century = thisYear / 100;
                else
                    century = thisYear / 100 - 1;
                string dateWithFourDigitYear = dateWithTwoDigitYear.Substring(0, i + 1) + century.ToString() + dateWithTwoDigitYear.Substring(i + 1);
                DateTime dt = DateTime.Parse(dateWithFourDigitYear, culture);
                return new List<IInline>(2) {
                    new WText(label, fText.properties),
                    new WDocDate(dateWithTwoDigitYear, fText.properties, dt)
                };
            }
        }
        return null;
    }

    private IEnumerable<IInline> Enrich1(IInline inline) {
        if (inline is WDate wDate)
            return new IInline[] { new WDocDate(wDate) };
        if (inline is WText fText)
            return EnrichText(fText);
        return null;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new System.NotImplementedException();
    }

}

}
