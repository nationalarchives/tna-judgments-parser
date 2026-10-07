
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse {

partial class Judge : Enricher {

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        List<IBlock> enriched = new List<IBlock>(blocks.Count());
        while (blocks.Any()) {
            List<IBlock> magic = EnrichOnlyUntilDashes(blocks);
            if (magic is null)
                magic = EnrichOnlyUntilNameNotFound(blocks);
            if (magic is null)
                magic = BeforeAndJudgeNameOnSameLine(blocks);
            if (magic is not null) {
                enriched.AddRange(magic);
                enriched.AddRange(blocks.Skip(magic.Count));
                break; // assumes 'enriched' will be returned
            }
            enriched.Add(blocks.First());
            blocks = blocks.Skip(1);
        }
        return enriched; // can't return 'blocks' b/c it will have changed
    }

    private List<IBlock> EnrichOnlyUntilDashes(IEnumerable<IBlock> blocks) {
        blocks = blocks.Take(6);
        if (!blocks.Any())
            return null;
        IBlock block = blocks.First();
        if (block is not WLine first)
            return null;
        if (!IsBefore(first.NormalizedContent))
            return null;
        List<IBlock> enriched = [block];
        blocks = blocks.Skip(1);
        bool found = false;
        while (blocks.Any()) {
            block = blocks.First();
            if (block is not WLine line)
                return null;
            if (line.Contents.Count() != 1) {
                enriched.Add(block);
                blocks = blocks.Skip(1);
                continue;
            }
            IInline inline = line.Contents.First();
            if (inline is not WText text) {
                enriched.Add(block);
                blocks = blocks.Skip(1);
                continue;
            }
            if (IsAJudgeName(text)) {
                found = true;
                WJudge judge = new WJudge(text.Text, text.properties);
                enriched.Add(WLine.Make(line, [judge]));
                blocks = blocks.Skip(1);
                continue;
            }
            if (Regex.IsMatch(text.Text, @"^ ?-( -)+ ?$"))
                return found ? enriched : null;
            enriched.Add(block);
            blocks = blocks.Skip(1);
        }
        return null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex AllWhitespace();

    static string RemoveAllWhitespace(string text) {
        return AllWhitespace().Replace(text, "");
    }

    static bool IsBefore(string text) {
        text = RemoveAllWhitespace(text);
        text = text.TrimEnd(':');
        return "Before".Equals(text, System.StringComparison.InvariantCultureIgnoreCase);
    }

    private List<IBlock> EnrichOnlyUntilNameNotFound(IEnumerable<IBlock> blocks) {
        blocks = blocks.Take(6);
        if (!blocks.Any())
            return null;
        IBlock block = blocks.First();
        if (block is not WLine first)
            return null;
        if (!IsBefore(first.NormalizedContent))
            return null;
        List<IBlock> enriched = [block];
        blocks = blocks.Skip(1);
        bool found = false;
        while (blocks.Any()) {
            block = blocks.First();
            if (block is not WLine line)
                return found ? enriched : null;
            if (line.Contents.Count() != 1)
                return found ? enriched : null;
            IInline inline = line.Contents.First();
            if (inline is not WText text)
                return found ? enriched : null;
            if (IsAJudgeName(text)) {
                found = true;
                WJudge judge = new WJudge(text.Text, text.properties);
                enriched.Add(WLine.Make(line, [judge]));
                blocks = blocks.Skip(1);
                continue;
            }
            if (IsALawyerName(text)) {
                // IBlock next = blocks.Skip(1).FirstOrDefault();
                // if (next is not null)
                    // if (next is WLine line2)
                        // if (line2.Contents.Count() == 1)
                            // if (line2.Contents.First() is WText text2)
                                // if (text2.Text.StartsWith("(sitting as", System.StringComparison.InvariantCultureIgnoreCase) || text2.Text.StartsWith("SITTING AS", System.StringComparison.InvariantCultureIgnoreCase)) {
                                    found = true;
                                    WJudge judge = new(text.Text, text.properties);
                                    enriched.Add(WLine.Make(line, [judge]));
                                    blocks = blocks.Skip(1);
                                    continue;
                                // }
            }
            Match match = Regex.Match(text.Text, @"([A-Z].*? [KQ]\.?C\.?) \(?Sitting As", RegexOptions.IgnoreCase);
            if (match.Success) {
                found = true;
                Group group = match.Groups[1];
                WJudge judge = new(group.Value, text.properties);
                WText rest = new(text.Text[group.Length..], text.properties);
                enriched.Add(WLine.Make(line, [judge, rest]));
                blocks = blocks.Skip(1);
                continue;
            }
            return found ? enriched : null;
        }
        return found ? enriched : null;
    }

    private static bool IsAJudgeName(WText text) {
        string normalized = Regex.Replace(text.Text, @"\s+", " ").Trim();
        ISet<string> starts = new HashSet<string> {
            "LORD JUSTICE ", "THE RIGHT HONOURABLE LORD JUSTICE ", "(LORD JUSTICE ",
            "LADY JUSTICE ", "THE RIGHT HONOURABLE LADY JUSTICE ", "(LADY JUSTICE ",
            "MR JUSTICE ", "MR. JUSTICE ",
            "MRS JUSTICE ", "MRS. JUSTICE ",
            "THE HONOURABLE MR JUSTICE ", "THE HONOURABLE MR. JUSTICE ", "THE HON. MR JUSTICE ", "THE HON MR JUSTICE ",
            "THE HONOURABLE MRS JUSTICE ", "THE HONOURABLE MRS. JUSTICE ", "THE HON. MRS JUSTICE ", "THE HON MRS JUSTICE ",
            "HIS HONOUR JUDGE ", "HER HONOUR JUDGE ", "HHJ", // ewfc/b/2024/38
            "SENIOR COSTS JUDGE ", "DISTRICT JUDGE ", "DEPUTY DISTRICT JUDGE ",
            "SIR "
            };
        foreach (string start in starts)
            if (normalized.StartsWith(start, System.StringComparison.InvariantCultureIgnoreCase))
                return true;
        return false;
    }

    private static bool IsALawyerName(WText text) {
        return text.Text.EndsWith(" Q.C.") || text.Text.EndsWith(" QC") ||
            text.Text.EndsWith(" K.C.") || text.Text.EndsWith(" KC");
    }

    private static List<IBlock> BeforeAndJudgeNameOnSameLine(IEnumerable<IBlock> blocks) {
        if (!blocks.Any())
            return null;
        IBlock block = blocks.First();
        if (block is not WLine line)
            return null;
        if (line.Contents.FirstOrDefault() is not WText first)
            return null;
        if (!first.Text.Trim().TrimEnd(':').Equals("Before", System.StringComparison.InvariantCultureIgnoreCase))
            return null;
        if (line.Contents.Skip(1).FirstOrDefault() is not WText second)
            return null;
        if (!IsAJudgeName(second))
            return null;
        var repl1 = new List<IInline>(blocks.Count()) { first, new WJudge(second.Text, second.properties) };
        repl1.AddRange(line.Contents.Skip(2));
        var repl2 = WLine.Make(line, repl1);
        return blocks.Skip(1).Prepend(repl2).ToList();
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        throw new System.NotImplementedException();
    }

}

}
