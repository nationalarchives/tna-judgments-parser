
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class CaseNo : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return Enumerable.Empty<IBlock>();
        IBlock first = blocks.First();
        IEnumerable<IBlock> rest = blocks.Skip(1);
        return base.Enrich(rest).Prepend(EnrichFirstBlock(first));
    }

    private IBlock EnrichFirstBlock(IBlock block) {
        if (block is WLine line) {
            if (line.Contents.Count() == 1) {
                IInline first = line.Contents.First();
                if (first is WText text) {
                    string pattern;
                    Match match;
                    pattern = @"^(No: )?([^ ]+)$";
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
                        return new WLine(line, contents);
                    }
                    pattern = @"^No: ([^ ]+ C\d)$";
                    match = Regex.Match(text.Text, pattern);
                    if (match.Success) {
                        WText label = new WText(text.Text.Substring(0, match.Groups[1].Index), text.properties);
                        WCaseNo caseNo = new WCaseNo(text.Text.Substring(match.Groups[1].Index), text.properties);
                        IEnumerable<IInline> contents = new List<IInline>(2) { label, caseNo };
                        return new WLine(line, contents);
                    }
                    pattern = @"^ *([A-Z0-9/]+) *$";
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
                }
            }
        }
        return base.Enrich(block);
    }

    protected override IBlock Enrich(IBlock block) {
        if (block is WLine line && line.Contents.Count() == 1 && line.Contents.First() is WText wText) {
            Regex[] regexes = {
                new Regex(@"^\s*No:?\s*([A-Z0-9/]+)\s*$", RegexOptions.IgnoreCase)
            };
            foreach (Regex re in regexes) {
                Match match = re.Match(wText.Text);
                if (!match.Success)
                    continue;
                List<IInline> contents = Split(wText, match.Groups[1]);
                return new WLine(line, contents);
            }
        }
        return base.Enrich(block);
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
        Regex re0 = new Regex(@"^\s*Case\s+(No|Number)s?:?\s*([^ ]+)", RegexOptions.IgnoreCase);
        // Regex re00 = new Regex(@"^\s*No:?\s*([A-Z0-9/]+)\s*$", RegexOptions.IgnoreCase);
        Regex re1 = new Regex(@"^\s*Case\s+(No|Number)s?:?\s*$", RegexOptions.IgnoreCase);
        Regex re2 = new Regex(@"^\s*([^ ]+)", RegexOptions.IgnoreCase);
        List<IInline> enriched = new List<IInline>();
        for (int i = 0; i < line.Count(); i++) {
            IInline inline = line.ElementAt(i);
            if (inline is WText text) {
                Match match = re0.Match(text.Text);
                if (match.Success) {
                    Group group = match.Groups[2];
                    List<IInline> replacement = Split(text, group, (t, props) => new WCaseNo(t, props));
                    enriched.AddRange(replacement);
                    continue;
                }
                match = re2.Match(text.Text);
                if (match.Success && i > 0) {
                    IInline last = line.ElementAt(i - 1);
                    if (last is WText text0 && re1.IsMatch(text0.Text)) {
                        Group group = match.Groups[1];
                        List<IInline> replacement = Split(text, group, (t, props) => new WCaseNo(t, props));
                        enriched.AddRange(replacement);
                        continue;
                    }
                }
            }
            enriched.Add(inline);
        }
        return enriched;
    }

    private IEnumerable<IInline> Enrich0(IEnumerable<IInline> line) {
        Regex re = new Regex(@"^\s*Case\s+(No|Number)s?:?\s*([A-Z\d/\.]+)", RegexOptions.IgnoreCase);
        return line.SelectMany(inline => {
            if (inline is WText text) {
                Match match = re.Match(text.Text);
                if (match.Success) {
                    Group group = match.Groups[2];
                    return Split(text, group, (t, props) => new WCaseNo(t, props));
                }
            }
            return new List<IInline>(1) { inline };
        });
    }

}

}
