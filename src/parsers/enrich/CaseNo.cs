
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class CaseNo : Enricher {

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
        Regex re0 = new Regex(@"^\s*Case\s+(No|Number):?\s*(\d+[/\.]\d{2}(\d{2})?)", RegexOptions.IgnoreCase);
        Regex re1 = new Regex(@"^\s*Case\s+(No|Number):?\s*$", RegexOptions.IgnoreCase);
        Regex re2 = new Regex(@"^\s*(\d+/\d{2}(\d{2})?)", RegexOptions.IgnoreCase);
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
        Regex re = new Regex(@"^\s*Case\s+(No|Number):?\s*(\d+[/\.]\d{2}(\d{2})?)", RegexOptions.IgnoreCase);
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
