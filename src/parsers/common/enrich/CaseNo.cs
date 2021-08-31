
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
                        Group g1 = match.Groups[1];
                        Group g2 = match.Groups[2];
                        List<IInline> contents = Split(text, g1, g2);
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
        new Regex(@"^Case No[:\.] +([A-Z0-9][A-Z0-9/-]{7,}) *$", RegexOptions.IgnoreCase),
        new Regex(@"^Case No[:\.] ([A-Z]+\d+ [A-Z]\d \d{4})$"),
        new Regex(@"^Case No[:\.] [A-Z]{2} [0-9]{2} [A-Z] [0-9]+$"),    // EWHC/Fam/2011/2376
        new Regex(@"^Case No[:\.] ([A-Z][A-Z0-9/-]{7,}), "),
        new Regex(@"^Case No[:\.] (\d+ of \d{4})$"),
        new Regex(@"^Claim No[:\.] (\d+ of \d{4})$"),
        new Regex(@"^Case No: (\d{4} Folio \d+)$")
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
            List<IInline> contents = Split(text, match.Groups[1]);
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
            IEnumerable<IInline> contents = Split(text2, match2.Groups[1]).Prepend(text1);
            return new WLine(line, contents);
        }
        return line;
    }

    internal delegate IFormattedText Wrapper(string text, RunProperties props);

    internal static List<IInline> Split(WText text, Group group, Wrapper wrapper) {
        string before = text.Text.Substring(0, group.Index);
        string during = group.Value;
        string after = text.Text.Substring(group.Index + group.Length);
        List<IInline> replacement = new List<IInline>(3) {
            new WText(before, text.properties),
            wrapper(during, text.properties)
        };
        if (!string.IsNullOrEmpty(after)) {
            WText third = new WText(after, text.properties);
            replacement.Add(third);
        }
        return replacement;
    }

    internal static List<IInline> Split(WText text, Group group) {
        string before = text.Text.Substring(0, group.Index);
        string during = group.Value;
        string after = text.Text.Substring(group.Index + group.Length);
        List<IInline> replacement = new List<IInline>(3);
        if (!string.IsNullOrEmpty(before)) {
            WText first = new WText(before, text.properties);
            replacement.Add(first);
        }
        WCaseNo caseNo = new WCaseNo(during, text.properties);
        replacement.Add(caseNo);
        if (!string.IsNullOrEmpty(after)) {
            WText third = new WText(after, text.properties);
            replacement.Add(third);
        }
        return replacement;
    }

    internal static List<IInline> Split(WText wText, Group g1, Group g2) {
        string text = wText.Text;
        RunProperties props = wText.properties;
        string s1 = text.Substring(0, g1.Index);
        string s2 = g1.Value;
        int start3 = g1.Index + g1.Length;
        string s3 = text.Substring(start3, g2.Index - start3);
        string s4 = g2.Value;
        int start5 = g2.Index + g2.Length;
        string s5 = text.Substring(start5);
        List<IInline> contents = new List<IInline>(5);
        if (!string.IsNullOrEmpty(s1)) {
            WText label1 = new WText(s1, props);
            contents.Add(label1);
        }
        WCaseNo caseNo1 = new WCaseNo(s2, props);
        contents.Add(caseNo1);
        WText label2 = new WText(s3, props);
        contents.Add(label2);
        WCaseNo caseNo2 = new WCaseNo(s4, props);
        contents.Add(caseNo2);
        if (!string.IsNullOrEmpty(s5)) {
            WText label3 = new WText(s5, props);
            contents.Add(label3);
        }
        return contents;
    }

    // internal override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
    //     if (line.Count() > 0) {
    //         IInline first = line.First();
    //         if (first is WText text) {
    //             System.Console.WriteLine("checking " + text.Text);
    //             Regex re = new Regex(@"^\s*Case\s+(No|Number):\s*(\d+/\d{2}(\d{2})?)", RegexOptions.IgnoreCase);
    //             // Match match = Regex.Match(text.Text, @"^\s*(Case|CASE) (No|NO|Number|NUMBER):\s*(\d+/\d\d(\d\d)?)\s*/?(\(?V\)?)?\s*$"); // ^\s*(Case|CASE) (No|NO|Number|NUMBER):\s*(\d+/\d\d(\d\d)?)\s*/?(\(?V\)?)?\s*$
    //             Match match = re.Match(text.Text);
    //             System.Console.WriteLine(match.Success);
    //             if (match.Success) {
    //                 Group group = match.Groups[2];
    //                 List<IInline> replacement = Split(text, group, (t, props) => new WCaseNo(t, props));
    //                 IEnumerable<IInline> rest = line.Skip(1);
    //                 return Enumerable.Concat(replacement, rest);
    //             }
    //         }
    //     }
    //     return line;
    // }
    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new System.Exception();
    }
    // protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
    //     Regex re0 = new Regex(@"^\s*Case\s+(No|Number)s?[:\.]?\s*([^ ]+)", RegexOptions.IgnoreCase);
    //     // Regex re00 = new Regex(@"^\s*No:?\s*([A-Z0-9/]+)\s*$", RegexOptions.IgnoreCase);
    //     Regex re1 = new Regex(@"^\s*Case\s+(No|Number)s?[:\.]?\s*$", RegexOptions.IgnoreCase);
    //     Regex re2 = new Regex(@"^\s*([^ ]+)", RegexOptions.IgnoreCase);
    //     List<IInline> enriched = new List<IInline>();
    //     for (int i = 0; i < line.Count(); i++) {
    //         IInline inline = line.ElementAt(i);
    //         if (inline is WText text) {
    //             Match match = re0.Match(text.Text);
    //             if (match.Success) {
    //                 Group group = match.Groups[2];
    //                 List<IInline> replacement = Split(text, group, (t, props) => new WCaseNo(t, props));
    //                 enriched.AddRange(replacement);
    //                 continue;
    //             }
    //             match = re2.Match(text.Text);
    //             if (match.Success && i > 0) {
    //                 IInline last = line.ElementAt(i - 1);
    //                 if (last is WText text0 && re1.IsMatch(text0.Text)) {
    //                     Group group = match.Groups[1];
    //                     List<IInline> replacement = Split(text, group, (t, props) => new WCaseNo(t, props));
    //                     enriched.AddRange(replacement);
    //                     continue;
    //                 }
    //             }
    //         }
    //         enriched.Add(inline);
    //     }
    //     return enriched;
    // }

    // private IEnumerable<IInline> Enrich000(IEnumerable<IInline> line) {
    //     Regex re = new Regex(@"^\s*Case\s+(No|Number)s?[:\.]?\s*([A-Z\d/\.]+)", RegexOptions.IgnoreCase);
    //     return line.SelectMany(inline => {
    //         if (inline is WText text) {
    //             Match match = re.Match(text.Text);
    //             if (match.Success) {
    //                 Group group = match.Groups[2];
    //                 return Split(text, group, (t, props) => new WCaseNo(t, props));
    //             }
    //         }
    //         return new List<IInline>(1) { inline };
    //     });
    // }

}

}
