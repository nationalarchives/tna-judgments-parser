
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class CaseNo : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        bool found = false;
        bool unfoundAfterFound = false;
        IEnumerator<IBlock> enumerator = blocks.GetEnumerator();
        if (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            IBlock rich = EnrichFirstBlock(block);
            enriched.Add(rich);
            found = !Object.ReferenceEquals(rich, block);
        }
        if (!found && enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            IBlock rich = EnrichFirstBlock(block);
            enriched.Add(rich);
            found = !Object.ReferenceEquals(rich, block);
        }
        while (enumerator.MoveNext()) {
            IBlock block = enumerator.Current;
            if (unfoundAfterFound) {
                enriched.Add(block);
            } else if (found) {
                IBlock rich = EnrichFirstBlock(block);
                enriched.Add(rich);
                unfoundAfterFound = Object.ReferenceEquals(rich, block);
            } else {
                IBlock rich = Enrich(block);
                enriched.Add(rich);
                found = !Object.ReferenceEquals(rich, block);
            }
        }
        return enriched;
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
                    pattern = @"^([A-Z0-9][A-Z0-9/\-]{7,}[A-Z0-9]) & ([A-Z0-9][A-Z0-9/\-]{7,}[A-Z0-9])$"; // EWCA/Civ/2005/210
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        List<IInline> contents = Split(text, match.Groups);
                        return new WLine(line, contents);
                    }
                    pattern = @"^([A-Z0-9][A-Z0-9/\-]{7,}[A-Z0-9]) & ([A-Z0-9][A-Z0-9/\-]{7,}[A-Z0-9]\(A\))$"; // EWCA/Civ/2006/829
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        List<IInline> contents = Split(text, match.Groups);
                        return new WLine(line, contents);
                    }
                    pattern = @"^([A-Z0-9][A-Z0-9/]{7,}[A-Z0-9]), ([A-Z0-9][A-Z0-9/]{7,}[A-Z0-9]) & ([A-Z0-9][A-Z0-9/]{7,}[A-Z0-9])$"; // EWCA/Civ/2008/1082
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        List<IInline> contents = Split(text, match.Groups);
                        return new WLine(line, contents);
                    }
                    pattern = @"^([A-Z]{2} \d{4} \d{5,})$";
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        List<IInline> contents = Split(text, match.Groups);
                        return new WLine(line, contents);
                    }
                    pattern = @"^No\. (\d+ of \d{4})$";  // EWHC/Ch/2014/1100
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
        new Regex(@"^Case +No[:\.]? +([A-Z0-9][A-Z0-9/\-]{6,}) *$", RegexOptions.IgnoreCase),
        new Regex(@"^Case No: ([A-Z0-9][A-Z0-9/\-]{6,} CHANF)$"),   // EWCA/Civ/2004/245
        new Regex(@"^Case +No[:\.]? +([0-9]{7,} [A-Z]\d) *$", RegexOptions.IgnoreCase),    // EWCA/Crim/2013/2398
        new Regex(@"^Case No[:\.] ([A-Z]+\d+ [A-Z]\d \d{4})$"),
        new Regex(@"^Case No[:\.] ([A-Z]{2} [0-9]{2} [A-Z] [0-9]+)$"),    // EWHC/Fam/2011/2376
        new Regex(@"^Case No[:\.] ([A-Z]{2} [0-9]{2} [A-Z][0-9]+)$"),    // EWHC/Ch/2004/1487
        new Regex(@"^Case No: ([A-Z]\d / \d{4} / \d+)$"),    // EWCA/Civ/2010/657
        new Regex(@"^Case No: ([A-Z]\d/ \d{4} / \d+) *$"), // EWCA/Civ/2010/709
        new Regex(@"^Case No[:\.] ([A-Z][A-Z0-9/\-]{7,}), "),
        new Regex(@"^Case No[:\.] (\d{4} - \d+)$"),    // EWHC/Comm/2007/197
        new Regex(@"^Case No[:\.] (\d{4} \d{5,} [A-Z]\d)"),    // EWCA/Crim/2015/1612
        new Regex(@"^Case No: (\d{4}/\d{5,} [A-Z]\d)$"),  // EWCA/Crim/2005/1983
        new Regex(@"^Case No[:\.] (\d+ of \d{4})$", RegexOptions.IgnoreCase),   // EWHC/Ch/2009/1961
        new Regex(@"^Claim No[:\.] (\d+ of \d{4})$"),
        new Regex(@"^Claim Nos: ([A-Z][A-Z0-9]{8,})$"), //   EWHC/QB/2013/417
        new Regex(@"^Case No: (\d{4} Folio \d+) *$", RegexOptions.IgnoreCase),    // , EWHC/Comm/2012/571, EWHC/Comm/2013/3920
        new Regex(@"^Claim No: (\d{4} Folio \d+)$"),    // EWHC/Comm/2009/3386
        new Regex(@"^Claim No:(\d{4} Folio \d+)$"),    // EWHC/Comm/2010/3113
        new Regex(@"^Claim No. (\d{4} Folio \d+)$"),    // EWHC/Comm/2011/894
        new Regex(@"^(\d{4} Folio No\. \d+)$"),    // EWHC/Comm/2004/2750
        new Regex(@"^(Folio No\. \d+ of \d{4})$"),    // EWHC/Ch/2013/2818
        new Regex(@"^Case No: (\d{4} FOLIO NO\. \d+)$"),    // EWHC/Admlty/2004/1506
        new Regex(@"^Case No:  ([A-Z]{3} \d{3} OF \d{4})$"), // EWHC/Admin/2008/2214
        new Regex(@"^Case Nos[:\.] ([A-Z0-9][A-Z0-9/\-]{7,})$", RegexOptions.IgnoreCase), // EWCA/Civ/2009/651
        new Regex(@"^Case Nos[:\.] ([A-Z0-9][A-Z0-9/\-]{7,});", RegexOptions.IgnoreCase), // EWHC/Admin/2015/715
        new Regex(@"^Case Nos: ([A-Z]{2} \d{4} \d{5,}) *$", RegexOptions.IgnoreCase), // EWHC/Ch/2017/541
        new Regex(@"^Case No\. ([A-Z]\d{4} \d+, SCCO Ref: \d+/\d+) *$"), // EWHC/Costs/2010/90172
        new Regex(@"^Ref: ([A-Z0-9]{7,}) *$"), // EWHC/Ch/2011/3553
        new Regex(@"Claim No ([A-Z]{2} \d{4} \d+)$"),   // EWHC/Comm/2017/1198
        new Regex(@"^Claim No ([A-Z]{2} [A-Z0-9]{7,})$"),    // EWHC/Ch/2012/1569
        new Regex(@"^Claim No: ([A-Z]{2} \d{4}-\d+) $"),    // EWHC/QB/2016/1174
        new Regex(@"^Claim No: ([A-Z]{2}-\d{4}-\d+) $"),    // EWHC/QB/2017/1748
        new Regex(@"^Case No: ([A-Z]{2}-\d{4} - \d+)$"),  // EWHC/Ch/2017/758
        new Regex(@"Case No: ([A-Z]{2}-[0-9]{2}-[A-Z]{2} \d{4})$"),   // EWHC/Ch/2004/2316
        new Regex(@"^Claim No\. ([A-Z]{2} [0-9]{2} [A-Z] [0-9]{5})$"), // EWHC/Ch/2003/812
        new Regex(@"^Claim No: ([A-Z]{2}-\d{2}-\d+)$"), // EWHC/TCC/2018/751
        new Regex(@"^Claim No: ([A-Z]{2}-\d{4}-\d+)$"), // EWHC/Ch/2015/411
        new Regex(@"^Case No: ([A-Z]\d \d{4}/\d+)$"), // EWCA/Civ/2006/1319
        new Regex(@"^Case No: ([A-Z][A-Z] \d{4} \d+)$"), // EWHC/Ch/2003/2497
        new Regex(@"^Case No: ([A-Z]\d \d{4} \d+)$"), // EWCA/Civ/2015/57
        new Regex(@"^Case No: ([A-Z]{3} \d+/\d{4})$"), // EWHC/Admin/2007/233
        new Regex(@"^Claim No\. ([A-Z0-9]{7,})$"), // EWHC/QB/2017/1550
        new Regex(@"^Case No: (\d{6,} \(Costs\))$"), // EWHC/QB/2007/1406   ???
        new Regex(@"^CaseNo\. ([A-Z0-9]{7,})$"),    // EWHC/Fam/2018/2433
        new Regex(@"^Case number:([A-Z0-9][A-Z0-9\/\-]+[A-Z0-9])$"),    // EWHC/TCC/2004/8

        new Regex(@"^Case Nos: ([A-Z][0-9] \d{4}/\d+), \d+ and \d+$"), // EWCA/Civ/2008/1303
        new Regex(@"^Case No: ([A-Z]{2}-\d{2}-\d+),\d+,\d+$") // EWHC/TCC/2009/3212
    };

    Regex[] loneTextRegexesWithTwoGroups = {
        new Regex(@"^Case No[:\.] ([A-Z0-9/\-\(\)]{7,}) [&/] ([A-Z0-9/\-\(\)]{7,})$", RegexOptions.IgnoreCase),  // EWHC/Ch/2014/4918, EWCA/Civ/2015/483
        new Regex(@"^Case No: ([A-Z0-9][A-Z0-9/\-]{7,}[A-Z0-9]); ([A-Z0-9][A-Z0-9/\-]{7,}[A-Z0-9])$"),    // EWCA/Civ/2011/1059
        new Regex(@"^Case Nos?[:\.] ([A-Z0-9/\-]{7,}), ([A-Z0-9/\-]{7,}),? *$", RegexOptions.IgnoreCase),  // EWCA/Civ/2008/19, EWCA/Civ/2008/1082
        new Regex(@"^Case Nos: ([A-Z0-9][A-Z0-9/\-]{7,}[A-Z0-9]) and ([A-Z0-9][A-Z0-9/\-]{7,}[A-Z0-9])$"), // EWHC/Admin/2013/19
        new Regex(@"Cases No: (\d{4} FOLIO \d+) and (\d{4} FOLIO \d+)"), // EWHC/Comm/2013/2793
        new Regex(@"Case Numbers: (\d{4} Folio \d+) & (\d{4} Folio \d+)$"), // EWHC/Comm/2010/784
        new Regex(@"^Case No: (\d+) and (\d+)$"),    // EWCOP/2016/30
        new Regex(@"^Case No: ([A-Z0-9][A-Z0-9\/\-]+[A-Z0-9]) - ([A-Z0-9][A-Z0-9\/\-]+[A-Z0-9])$") // EWHC/Ch/2016/2683
        // new Regex(@"Case Nos: ([A-Z][A-Z0-9/]{7,}[A-Z0-9]), ([A-Z][A-Z0-9/]{7,}[A-Z0-9]), ") //
    };

    Regex[] loneTextRegexesWithMultipleGroups = {
        new Regex(@"^([A-Z0-9][A-Z0-9/\-]{5,}[A-Z0-9] \(A\)); ([A-Z0-9][A-Z0-9/\-]{5,}[A-Z0-9]); ([A-Z0-9][A-Z0-9/\-]{5,}[A-Z0-9])$"),  // EWCA/Civ/2004/122
        new Regex(@"^Case Nos[:\.] ([0-9]{7} [A-Z][0-9]), ([0-9]{7} [A-Z][0-9]), ([0-9]{7} [A-Z][0-9]) *$", RegexOptions.IgnoreCase),  // EWCA/Crim/2010/2638
        new Regex(@"^Case No: ([A-Z0-9]{10,}); ([A-Z0-9]{10,}); ([A-Z0-9]{10,}); ([A-Z0-9]{10,}) *$"),   // EWCA/Crim/2006/1741
        new Regex(@"^Case No: (\d{7,}), (\d{7,}), (\d{7,})$"),   // EWCA/Crim/2013/1305
        new Regex(@"^Case Nos: ([A-Z0-9][A-Z0-9\/\-]+[A-Z0-9]), ([A-Z0-9][A-Z0-9\/\-]+[A-Z0-9]), ([A-Z0-9][A-Z0-9\/\-]+[A-Z0-9]), ([A-Z0-9][A-Z0-9\/\-]+[A-Z0-9]), ([A-Z0-9][A-Z0-9\/\-]+[A-Z0-9])$")   // EWHC/Admin/2012/2736
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
        List<IInline> contents = EnrichOneSpan(text);
        return new WLine(line, contents);
        // foreach (Regex re in loneTextRegexesWithOneGroup) {
        //     Match match = re.Match(text.Text);
        //     if (!match.Success)
        //         continue;
        //     List<IInline> contents = Split(text, match.Groups);
        //     return new WLine(line, contents);
        // }
        // foreach (Regex re in loneTextRegexesWithTwoGroups) {
        //     Match match = re.Match(text.Text);
        //     if (!match.Success)
        //         continue;
        //     List<IInline> contents = Split(text, match.Groups);
        //     return new WLine(line, contents);
        // }
        // foreach (Regex re in loneTextRegexesWithMultipleGroups) {
        //     Match match = re.Match(text.Text);
        //     if (!match.Success)
        //         continue;
        //     List<IInline> contents = Split(text, match.Groups);
        //     return new WLine(line, contents);
        // }
        // return line;
    }

    private List<IInline> EnrichOneSpan(WText text) {
        foreach (Regex re in loneTextRegexesWithOneGroup) {
            Match match = re.Match(text.Text);
            if (!match.Success)
                continue;
            return Split(text, match.Groups);
        }
        foreach (Regex re in loneTextRegexesWithTwoGroups) {
            Match match = re.Match(text.Text);
            if (!match.Success)
                continue;
            return Split(text, match.Groups);
        }
        foreach (Regex re in loneTextRegexesWithMultipleGroups) {
            Match match = re.Match(text.Text);
            if (!match.Success)
                continue;
            return Split(text, match.Groups);
        }
        return new List<IInline>(1) { text };
    }

    private WLine EnrichLineWithTwoSpans(WLine line) {
        IInline first = line.Contents.First();
        IInline second = line.Contents.Skip(1).First();
        if (first is not WText text1)
            return line;
        if (second is not WText text2)
            return line;
        if (string.IsNullOrWhiteSpace(text1.Text)) {
            IEnumerable<IInline> contents = EnrichOneSpan(text2).Prepend(text1);
            return new WLine(line, contents);
        }
        Regex re1 = new Regex(@"^\s*Case\s+(No|Number)s?[:\.]?\s*$", RegexOptions.IgnoreCase);
        Regex re2 = new Regex(@"^\s*([^ ]+) *$", RegexOptions.IgnoreCase);
        Match match1 = re1.Match(text1.Text);
        Match match2 = re2.Match(text2.Text);
        if (match1.Success && match2.Success) {
            IEnumerable<IInline> contents = Split(text2, match2.Groups).Prepend(text1);
            return new WLine(line, contents);
        }
        re2 = new Regex(@"^\s*([A-Z0-9]{9,} [A-Z][0-9]) *$");   // EWCA/Crim/2012/2893
        match2 = re2.Match(text2.Text);
        if (match1.Success && match2.Success) {
            IEnumerable<IInline> contents = Split(text2, match2.Groups).Prepend(text1);
            return new WLine(line, contents);
        }
        return line;
    }

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
