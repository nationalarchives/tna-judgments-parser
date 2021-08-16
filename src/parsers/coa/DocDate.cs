
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace UK.Gov.Legislation.Judgments.Parse {

class DocDate : Enricher {

    private static readonly string[] datePatterns = {
        @"^Date: (\d{2}/\d{2}/\d{4})$",
        @"^Date: (\d{1,2} (January|February|March|April|May|June|July|August|September|October|November|December) \d{4})$"
    };
    private static Group MatchDate(string text) {
        foreach (string pattern in datePatterns) {
            Match match = Regex.Match(text, pattern);
            if (match.Success)
                return match.Groups[1];
        }
        return null;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        CultureInfo culture = new CultureInfo("en-GB");
        if (line.Count() == 1) {
            IInline inline = line.First();
            if (inline is WDate wDate) {
                WDocDate docDate = new WDocDate(wDate);
                return new IInline[] { docDate };
            }
            if (inline is WText fText) {
                Group group = MatchDate(fText.Text);
                if (group is not null) {
                    WText label = new WText(fText.Text.Substring(0, group.Index), fText.properties);
                    DateTime date = DateTime.Parse(group.Value, culture);
                    WDocDate docDate = new WDocDate(group.Value, fText.properties, date);
                    return new IInline[] { label, docDate };
                }
            }
        }
        if (line.Count() == 3) {
            IInline first = line.First();
            IInline second = line.ElementAt(1);
            IInline third = line.ElementAt(2);
            if (first is WText fText1) {
                if (second is WText fText2) {
                    if (third is WText fText3) {
                        string pattern1 = @"^((Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday),? )?(\d{1,2})$";
                        string pattern2 = @"^(st|nd|rd|th)$";
                        string pattern3 = @"^ (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$";
                        Match match1 = Regex.Match(fText1.Text, pattern1);
                        Match match2 = Regex.Match(fText2.Text, pattern2);
                        Match match3 = Regex.Match(fText3.Text, pattern3);
                        if (match1.Success && match2.Success && match3.Success) {
                            string combined = match1.Groups[3].Value + fText3.Text; // exclude day of the week, in case it doesn't match (EWHC/Admin/2018/1074)
                            DateTime date = DateTime.Parse(combined, culture);
                            return new IInline[] { new WDocDate(line.Cast<WText>(), date) };
                        }
                    }
                }
            }
        }
        if (line.Count() >= 2) {
            string pattern1 = @"^(On|Date of Judgment):$";
            string pattern3 = @"^\d{1,2} (January|February|March|April|May|June|July|August|September|October|November|December) \d{4}$";
            IInline last = line.ElementAt(line.Count() - 1);
            int i = line.Count() - 2;
            IInline first = line.ElementAt(i);
            while (i > 0 && first is WTab) {
                i -= 1;
                first = line.ElementAt(i);
            }
            if (first is WText fText1 && last is WText fText3) {
                Match match1 = Regex.Match(fText1.Text.Trim(), pattern1);
                Match match3 = Regex.Match(fText3.Text.Trim(), pattern3);
                if (match1.Success && match3.Success) {
                    DateTime date = DateTime.Parse(fText3.Text, culture);
                    WDocDate docDate =  new WDocDate(new List<WText>(1) { fText3 }, date);
                    return line.Take(line.Count() - 1).Append(docDate);
                }
            }
        }
        return line;
    }

}

}
