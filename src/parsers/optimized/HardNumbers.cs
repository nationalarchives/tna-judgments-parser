
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

/// this class identifies leading numbers that appear as plain text
class HardNumbers {

    internal static List<BlockWithBreak> Extract(IEnumerable<BlockWithBreak> contents) {
        HardNumbers extractor = new HardNumbers(contents);
        extractor.ExtractAll();
        return extractor.Extracted;
    }

    private readonly List<BlockWithBreak> Input;
    private readonly List<BlockWithBreak> Extracted;

    private HardNumbers(IEnumerable<BlockWithBreak> contents) {
        Input = contents.ToList();
        Extracted = new List<BlockWithBreak>();
    }

    private void ExtractAll() {
        foreach (var original in Input) {
            BlockWithBreak extracted = Extract1(original);
            Extracted.Add(extracted);
        }
        SecondPass();
    }

    private BlockWithBreak Extract1(BlockWithBreak bb) {
        if (bb.Block is not WLine line)
            return bb;
        WOldNumberedParagraph removed = ExtractHardNumber(line);
        if (removed is null)
            return bb;
        return new BlockWithBreak { Block = removed, LineBreakBefore = bb.LineBreakBefore };
    }

    private WOldNumberedParagraph ExtractHardNumber(WLine line) {
        if (line is WOldNumberedParagraph)
            return null;
        WOldNumberedParagraph removed = ExtractPlainNumber(line);
        if (removed is not null)
            return removed;
        foreach (string format in NumberFormats) {
            removed = ExtractNumberWithFormat(line, format);
            if (removed is not null)
                return removed;
        }
        return null;
    }

    private static readonly string PlainNumberFormat = @"^[“""]?\d+$";

    private static readonly string[] NumberFormats = new string[] {
        @"^([“""]?\d+\.)",              @"^([“""]?\(\d+\))",
        @"^([“""]?[A-Z]\.)",            @"^([“""]?\([A-Z]\))",
        @"^([“""]?[a-z]\.)",            @"^([“""]?\([a-z]\))",
        @"^([“""]?[ivx]+\.)",           @"^([“""]?\([ivx]+\))",
        // compound
        @"^([“""]?[1-9]\d*\.\d+\.?)",        @"^([“""]?\([1-9]\d*\.\d+\))",
        @"^([“""]?[1-9]\d*\.\d+\.\d+\.?)",   @"^([“""]?\([1-9]\d*\.\d+\.\d+\))"
    }.Select(s => s + @"( |$)").ToArray();

    private WOldNumberedParagraph ExtractPlainNumber(WLine line) {
        if (line is WOldNumberedParagraph)
            return null;
        if (line.Contents.FirstOrDefault() is not WText first)
            return null;
        if (line.Contents.Skip(1).FirstOrDefault() is not WTab)
            return null;
        if (!Regex.IsMatch(first.Text, PlainNumberFormat))
            return null;
        return new WOldNumberedParagraph(first, line.Contents.Skip(2), line);
    }

    private WOldNumberedParagraph ExtractNumberWithFormat(WLine line, string format) {
        if (line is WOldNumberedParagraph)
            return null;
        IEnumerable<IInline> contents = line.Contents;
        contents = contents.SkipWhile(first => first is WTab || first is WLineBreak);
        if (contents.FirstOrDefault() is not WText first)
            return null;
        IEnumerable<IInline> rest = contents.Skip(1);
        string trimmed = first.Text.TrimStart();
        RunProperties firstProps = first.properties;
        RunProperties lastProps = first.properties;
        Match match = Regex.Match(trimmed, format);
        if (!match.Success && trimmed.Length < 5 && rest.FirstOrDefault() is WText second) {
            trimmed = trimmed + second.Text;
            lastProps = second.properties;
            rest = rest.Skip(1);
            match = Regex.Match(trimmed, format);
        }
        if (!match.Success)
            return null;
        WText num = new WText(match.Groups[1].Value, firstProps);
        string after = trimmed.Substring(num.Text.Length).TrimStart();
        if (!string.IsNullOrEmpty(after)) {
            WText after2 = new WText(after, lastProps);
            rest = rest.Prepend(after2);
        } else if (rest.FirstOrDefault() is WTab) {
            rest = rest.Skip(1);
        }
        if (rest.FirstOrDefault() is WText next) {
            string nextTrimmed = next.Text.TrimStart();
            if (nextTrimmed != next.Text) {
                WText replacement = new WText(next.Text.TrimStart(), next.properties);
                rest = rest.Skip(1).Prepend(replacement);
            }
        }
        return new WOldNumberedParagraph(num, rest, line);
    }

    private int GetIndent(WLine line) {
        float indent = line.LeftIndentInches ?? 0f;
        return (int) Math.Round(indent / 8);
    }

    private struct IndexAndNumber { internal int I, N; }  // I is the position of the paragraph in the document; N is the int value of its number

    /// reverts uppercase letters that are out of place, e.g., a C that isn't preceded by a B, or an A that isn't followed by a B
    private void SecondPass() {
        IDictionary<int, Stack<IndexAndNumber>> magic = new Dictionary<int, Stack<IndexAndNumber>>();
        int i = 0;
        foreach (BlockWithBreak original in Input) {
            BlockWithBreak extracted = Extracted[i];
            if (object.ReferenceEquals(extracted, original)) {  // ignore soft numbers
                i += 1;
                continue;
            }
            if (extracted.Block is not WOldNumberedParagraph np) {
                i += 1;
                continue;
            }
            Match match = Regex.Match(np.Number.Text, @"^[“""]?([A-Z])\.$");
            if (!match.Success) {
                i += 1;
                continue;
            }
            int n = match.Groups[1].Value[0] - 64;  // A = 1, B = 2, etc
            int indent = GetIndent(np);
            IndexAndNumber here = new IndexAndNumber { I = i, N = n };

            Func<IndexAndNumber?> GetLast = () => magic.ContainsKey(indent) && magic[indent].Count > 0 ? magic[indent].Peek() : null;
            Action SetLast = () => { if (!magic.ContainsKey(indent)) magic[indent] = new Stack<IndexAndNumber>(); magic[indent].Push(here); };
            IndexAndNumber? last = GetLast();

            Action Revert = () => { Extracted[i] = Input[i]; };
            Action RevertLast = () => {
                int lastI = last.Value.I;
                Extracted[lastI] = Input[lastI];
                Stack<IndexAndNumber> magic1 = magic[indent];
                magic1.Pop();
                last = magic1.Count == 0 ? null : magic1.Peek();
            };

            if (n == 1 && !last.HasValue) {
                SetLast();
                i += 1;
                continue;
            }
            if (!last.HasValue) {  // n > 1
                Revert();
                i += 1;
                continue;
            }
            if (n == last.Value.N + 1) {
                SetLast();
                i += 1;
                continue;
            }
            if (n == 1 && last.Value.N > 1) {
                SetLast();
                i += 1;
                continue;
            }
            if (last.Value.N > 1) {  // n > 1
                Revert();
                i += 1;
                continue;
            }
            // last.Value.N == 1 && n != 2
            RevertLast();  // now last is second to last
            // last.HasValue && last.Value.N == 1 should be impossible
            if (n == 1) {
                SetLast();
                i += 1;
                continue;
            }
            if (!last.HasValue) {
                Revert();
                i += 1;
                continue;
            }
            if (n == last.Value.N + 1) {
                SetLast();
                i += 1;
                continue;
            }
            Revert();
            i += 1;
        }

    }

}

}
