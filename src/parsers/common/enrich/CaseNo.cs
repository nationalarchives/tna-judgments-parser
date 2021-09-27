
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class CaseNo : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return Enumerable.Empty<IBlock>();
        IBlock rawFirst = blocks.First();
        IBlock enrichedFirst = EnrichFirstBlock(rawFirst);
        IEnumerable<IBlock> rest = blocks.Skip(1);
        if (!rest.Any())
            return new List<IBlock>(1) { enrichedFirst };
        IBlock rawSecond = rest.First();
        IBlock enrichedSecond = EnrichFirstBlock(rawSecond);
        rest = rest.Skip(1);
        return base.Enrich(rest).Prepend(enrichedSecond).Prepend(enrichedFirst);
    }

    private IBlock EnrichFirstBlock(IBlock block) {
        if (block is WLine line) {
            if (line.Contents.Count() == 1) {
                IInline first = line.Contents.First();
                if (first is WText text) {
                    string pattern;
                    Match match;
                    pattern = @"^(No[:\.] )?([^ ]+) *$";
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        Group label1 = match.Groups[1];
                        Group caseNo1 = match.Groups[2];
                        WCaseNo caseNo = new WCaseNo(caseNo1.Value, text.properties);
                        List<IInline> contents;
                        if (label1.Length == 0) {
                            contents = new List<IInline>(1) { caseNo };
                        } else {
                            WText label = new WText(label1.Value, text.properties);
                            contents = new List<IInline>(2) { label, caseNo };
                        }
                        if (caseNo1.Index + caseNo1.Length < text.Text.Length) {
                            string after = text.Text.Substring(caseNo1.Index + caseNo1.Length);
                            WText label2 = new WText(after, text.properties);
                            contents.Add(label2);
                        }
                        return new WLine(line, contents);
                    }
                    pattern = @"^No: ([A-Z0-9/-]+ [A-Z]\d)$";
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        WText label = new WText(text.Text.Substring(0, match.Groups[1].Index), text.properties);
                        WCaseNo caseNo = new WCaseNo(text.Text.Substring(match.Groups[1].Index), text.properties);
                        IEnumerable<IInline> contents = new List<IInline>(2) { label, caseNo };
                        return new WLine(line, contents);
                    }
                    pattern = @"^ *([A-Z0-9/-]+) *$";
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        Group group = match.Groups[1];
                        int start = group.Index;
                        int length = group.Length;
                        List<IInline> contents = new List<IInline>(3);
                        if (start > 0) {
                            string s1 = text.Text.Substring(0, start);
                            WText label1 = new WText(s1, text.properties);
                            contents.Add(label1);
                        }
                        string s2 = group.Value;
                        WCaseNo caseNo = new WCaseNo(s2, text.properties);
                        contents.Add(caseNo);
                        string s3 = text.Text.Substring(start + length);
                        if (!string.IsNullOrEmpty(s3)) {
                            WText label2 = new WText(s3, text.properties);
                            contents.Add(label2);
                        }
                        return new WLine(line, contents);
                    }
                    pattern = @"^ *([A-Z0-9/]{10,}), +([A-Z0-9/]{10,}) *$";
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        List<IInline> contents = Split(text, match.Groups);
                        return new WLine(line, contents);
                    }
                    pattern = @"^([A-Z0-9][A-Z0-9/-]{7,}[A-Z0-9]) & ([A-Z0-9][A-Z0-9/-]{7,}[A-Z0-9])$"; // EWCA/Civ/2005/210
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        List<IInline> contents = Split(text, match.Groups);
                        return new WLine(line, contents);
                    }
                }
            }
            if (line.Contents.Count() == 3) {   // EWHC/Admin/2009/3016
                IInline first = line.Contents.First();
                IInline second = line.Contents.Skip(1).First();
                IInline third = line.Contents.Skip(2).First();
                if (first is WText wText1 && second is WText wText2 && third is WText wText3) {
                    if (wText1.Text == "Case No:" && wText2.Text == " " && Regex.IsMatch(wText3.Text, @"^[A-Z][A-Z0-9/-]{5,}[A-Z0-9]$")) {
                        List<IInline> contents = new List<IInline>(3) { first, second, new WCaseNo(wText3.Text, wText3.properties) };
                        return new WLine(line, contents);
                    }
                }
            }
        }
        return Enrich(block);
    }

    protected override IBlock Enrich(IBlock block) {
        if (block is not WLine line)
            return block;
        return EnrichLine(line);
    }

    Regex[] loneTextRegexesWithOneGroup = {
        new Regex(@"^\s*No:?\s*([A-Z0-9/]+)\s*$", RegexOptions.IgnoreCase),
        new Regex(@"^Case +No[:\.]? +([A-Z0-9][A-Z0-9/-]{6,}) *$", RegexOptions.IgnoreCase),
        new Regex(@"^Case +No[:\.]? +([0-9]{7,} [A-Z]\d) *$", RegexOptions.IgnoreCase),    // EWCA/Crim/2013/2398
        new Regex(@"^Case No[:\.] ([A-Z]+\d+ [A-Z]\d \d{4})$"),
        new Regex(@"^Case No[:\.] ([A-Z]{2} [0-9]{2} [A-Z] [0-9]+)$"),    // EWHC/Fam/2011/2376
        new Regex(@"^Case No[:\.] ([A-Z]{2} [0-9]{2} [A-Z][0-9]+)$"),    // EWHC/Ch/2004/1487
        new Regex(@"^Case No: ([A-Z]\d / \d{4} / \d+)$"),    // EWCA/Civ/2010/657
        new Regex(@"^Case No[:\.] ([A-Z][A-Z0-9/-]{7,}), "),
        new Regex(@"^Case No[:\.] (\d{4} \d{5,} [A-Z]\d)"),    // EWCA/Crim/2015/1612
        new Regex(@"^Case No[:\.] (\d+ of \d{4})$", RegexOptions.IgnoreCase),   // EWHC/Ch/2009/1961
        new Regex(@"^Claim No[:\.] (\d+ of \d{4})$"),
        new Regex(@"^Case No: (\d{4} Folio \d+) *$", RegexOptions.IgnoreCase),    // , EWHC/Comm/2012/571, EWHC/Comm/2013/3920
        new Regex(@"^Claim No: (\d{4} Folio \d+)$"),    // EWHC/Comm/2009/3386
        new Regex(@"^(\d{4} Folio No\. \d+)$"),    // EWHC/Comm/2004/2750
        new Regex(@"^Case No:  ([A-Z]{3} \d{3} OF \d{4})$"), // EWHC/Admin/2008/2214
        new Regex(@"^Case Nos[:\.] ([A-Z0-9][A-Z0-9/-]{7,})$", RegexOptions.IgnoreCase), // EWCA/Civ/2009/651
        new Regex(@"^Case Nos[:\.] ([A-Z0-9][A-Z0-9/-]{7,});", RegexOptions.IgnoreCase), // EWHC/Admin/2015/715
        new Regex(@"^Case No\. ([A-Z]\d{4} \d+, SCCO Ref: \d+/\d+) *$"), // EWHC/Costs/2010/90172
        new Regex(@"^Ref: ([A-Z0-9]{7,}) *$"), // EWHC/Ch/2011/3553
        new Regex(@"Claim No ([A-Z]{2} \d{4} \d+)$"),   // EWHC/Comm/2017/1198
        new Regex(@"Case No: ([A-Z]{2}-[0-9]{2}-[A-Z]{2} \d{4})$"),   // EWHC/Ch/2004/2316
        new Regex(@"^Claim No\. ([A-Z]{2} [0-9]{2} [A-Z] [0-9]{5})$"), // EWHC/Ch/2003/812
        new Regex(@"^Case No: ([A-Z]\d \d{4}/\d+)$") // EWCA/Civ/2006/1319
    };

    Regex[] loneTextRegexesWithTwoGroups = {
        new Regex(@"^Case No[:\.] ([A-Z0-9/-]{7,}) [&/] ([A-Z0-9/-]{7,})$", RegexOptions.IgnoreCase),  // EWHC/Ch/2014/4918
        new Regex(@"^Case Nos?[:\.] ([A-Z0-9/-]{7,}), ([A-Z0-9/-]{7,}),?$", RegexOptions.IgnoreCase),  // EWCA/Civ/2008/19
        new Regex(@"^Case Nos: ([A-Z0-9][A-Z0-9/-]{7,}[A-Z0-9]) and ([A-Z0-9][A-Z0-9/-]{7,}[A-Z0-9])$"), // EWHC/Admin/2013/19
        new Regex(@"Cases No: (\d{4} FOLIO \d+) and (\d{4} FOLIO \d+)") // EWHC/Comm/2013/2793
    };

    Regex[] loneTextRegexesWithThreeGroups = {
        new Regex(@"^([A-Z0-9][A-Z0-9/-]{5,}[A-Z0-9] \(A\)); ([A-Z0-9][A-Z0-9/-]{5,}[A-Z0-9]); ([A-Z0-9][A-Z0-9/-]{5,}[A-Z0-9])$"),  // EWCA/Civ/2004/122
        new Regex(@"^Case Nos[:\.] ([0-9]{7} [A-Z][0-9]), ([0-9]{7} [A-Z][0-9]), ([0-9]{7} [A-Z][0-9]) *$", RegexOptions.IgnoreCase),  // EWCA/Crim/2010/2638
    };


    private WLine EnrichLine(WLine line) {
        if (line.Contents.Count() == 1)
            return EnrichLineWithOneSpan(line);
        if (line.Contents.Count() == 2)
            return EnrichLineWithTwoSpans(line);
        return line;
    }

    private WLine EnrichLineWithOneSpan(WLine line) {
        IInline first = line.Contents.First();
        if (first is not WText text)
            return line;
        foreach (Regex re in loneTextRegexesWithOneGroup) {
            Match match = re.Match(text.Text);
            if (!match.Success)
                continue;
            List<IInline> contents = Split(text, match.Groups);
            return new WLine(line, contents);
        }
        foreach (Regex re in loneTextRegexesWithTwoGroups) {
            Match match = re.Match(text.Text);
            if (!match.Success)
                continue;
            List<IInline> contents = Split(text, match.Groups);
            return new WLine(line, contents);
        }
        foreach (Regex re in loneTextRegexesWithThreeGroups) {
            Match match = re.Match(text.Text);
            if (!match.Success)
                continue;
            List<IInline> contents = Split(text, match.Groups);
            return new WLine(line, contents);
        }
        return line;
    }

    private WLine EnrichLineWithTwoSpans(WLine line) {
        IInline first = line.Contents.First();
        IInline second = line.Contents.Skip(1).First();
        if (first is not WText text1)
            return line;
        if (second is not WText text2)
            return line;
        Regex re1 = new Regex(@"^\s*Case\s+(No|Number)s?[:\.]?\s*$", RegexOptions.IgnoreCase);
        Regex re2 = new Regex(@"^\s*([^ ]+) *$", RegexOptions.IgnoreCase);
        Match match1 = re1.Match(text1.Text);
        Match match2 = re2.Match(text2.Text);
        if (match1.Success && match2.Success) {
            IEnumerable<IInline> contents = Split(text2, match2.Groups).Prepend(text1);
            return new WLine(line, contents);
        }
        return line;
    }

    // internal delegate IFormattedText Wrapper(string text, RunProperties props);

    // internal static List<IInline> Split(WText text, Group group, Wrapper wrapper) {
    //     string before = text.Text.Substring(0, group.Index);
    //     string during = group.Value;
    //     string after = text.Text.Substring(group.Index + group.Length);
    //     List<IInline> replacement = new List<IInline>(3) {
    //         new WText(before, text.properties),
    //         wrapper(during, text.properties)
    //     };
    //     if (!string.IsNullOrEmpty(after)) {
    //         WText third = new WText(after, text.properties);
    //         replacement.Add(third);
    //     }
    //     return replacement;
    // }

    // internal static List<IInline> Split(WText text, Group group) {
    //     string before = text.Text.Substring(0, group.Index);
    //     string during = group.Value;
    //     string after = text.Text.Substring(group.Index + group.Length);
    //     List<IInline> replacement = new List<IInline>(3);
    //     if (!string.IsNullOrEmpty(before)) {
    //         WText first = new WText(before, text.properties);
    //         replacement.Add(first);
    //     }
    //     WCaseNo caseNo = new WCaseNo(during, text.properties);
    //     replacement.Add(caseNo);
    //     if (!string.IsNullOrEmpty(after)) {
    //         WText third = new WText(after, text.properties);
    //         replacement.Add(third);
    //     }
    //     return replacement;
    // }

    // internal static List<IInline> Split(WText wText, Group g1, Group g2) {
    //     string text = wText.Text;
    //     RunProperties props = wText.properties;
    //     string s1 = text.Substring(0, g1.Index);
    //     string s2 = g1.Value;
    //     int start3 = g1.Index + g1.Length;
    //     string s3 = text.Substring(start3, g2.Index - start3);
    //     string s4 = g2.Value;
    //     int start5 = g2.Index + g2.Length;
    //     string s5 = text.Substring(start5);
    //     List<IInline> contents = new List<IInline>(5);
    //     if (!string.IsNullOrEmpty(s1)) {
    //         WText label1 = new WText(s1, props);
    //         contents.Add(label1);
    //     }
    //     WCaseNo caseNo1 = new WCaseNo(s2, props);
    //     contents.Add(caseNo1);
    //     WText label2 = new WText(s3, props);
    //     contents.Add(label2);
    //     WCaseNo caseNo2 = new WCaseNo(s4, props);
    //     contents.Add(caseNo2);
    //     if (!string.IsNullOrEmpty(s5)) {
    //         WText label3 = new WText(s5, props);
    //         contents.Add(label3);
    //     }
    //     return contents;
    // }

    internal static List<IInline> Split(WText wText, GroupCollection groups) {
        List<IInline> contents = new List<IInline>();
        string text = wText.Text;
        int index = 0;
        foreach (Group group in groups.Values.Skip(1)) {
            int start = group.Index;
            if (start > index) {
                string s = text.Substring(index, start - index);
                WText label = new WText(s, wText.properties);
                contents.Add(label);
            }
            WCaseNo caseNo = new WCaseNo(group.Value, wText.properties);
            contents.Add(caseNo);
            index = start + group.Length;
        }
        if (text.Length > index) {
            string s = text.Substring(index, text.Length - index);
            WText label = new WText(s, wText.properties);
            contents.Add(label);
        }
        return contents;
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new System.NotImplementedException();
    }

}

}
