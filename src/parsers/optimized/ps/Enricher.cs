
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parsers;

namespace UK.Gov.NationalArchives.CaseLaw.Parse {

class PressSummaryEnricher {

    internal static IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        return new PressSummaryEnricher(blocks).Enrich();
    }

    enum State {
        Start,
        AfterDateBeforeDocType, AfterDocTypeBeforeDate, // date and docType can be in either order
        AfterDateAndDocTypeBeforeCite,                  // cite is always after both date and docType
        Done                                            // haven't necessarily found all but stop looking
    };

    private State state = State.Start;

    internal readonly IEnumerable<IBlock> Blocks;

    private readonly List<IBlock> Enriched;

    private readonly IEnumerator<IBlock> Enumerator;

    private PressSummaryEnricher(IEnumerable<IBlock> blocks) {
        Blocks = blocks;
        Enriched = new List<IBlock>(blocks.Count());
        Enumerator = blocks.GetEnumerator();
    }

    private IEnumerable<IBlock> Enrich() {
        while (Enumerator.MoveNext()) {
            IBlock block = Enumerator.Current;
            if (state == State.Start) {
                if (block is not WLine line) {
                    return Blocks;
                }
                if (line.NormalizedContent == "" && line.Contents.Any(i => i is WImageRef)) {
                    Enriched.Add(line);
                    continue;
                }
                if (IsRed(line)) {
                    WRestriction restriction = new WRestriction(line);
                    Enriched.Add(restriction);
                    continue;
                }
                if (line.NormalizedContent.StartsWith(@"Reporting Restrictions Apply", StringComparison.InvariantCultureIgnoreCase)) {
                    WRestriction restriction = new WRestriction(line);
                    Enriched.Add(restriction);
                    continue;
                }
                WLine enriched1 = EnrichDate(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.AfterDateBeforeDocType;
                    continue;
                }
                enriched1 = EnrichDocType(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.AfterDocTypeBeforeDate;
                    continue;
                }
                return Blocks;
            }
            if (state == State.AfterDateBeforeDocType) {
                if (block is not WLine line) {
                    Enriched.Add(block);
                    state = State.Done;
                    continue;
                }
                if (IsRed(line)) {
                    WRestriction restriction = new WRestriction(line);
                    Enriched.Add(restriction);
                    continue;
                }
                WLine enriched1 = EnrichDocType(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.AfterDateAndDocTypeBeforeCite;
                    continue;
                }
                Enriched.Add(line);
                state = State.Done;
                continue;
            }
            if (state == State.AfterDocTypeBeforeDate) {
                if (block is not WLine line) {
                    Enriched.Add(block);
                    state = State.Done;
                    continue;
                }
                if (IsRed(line)) {
                    WRestriction restriction = new WRestriction(line);
                    Enriched.Add(restriction);
                    continue;
                }
                WLine enriched1 = EnrichDate(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.AfterDateAndDocTypeBeforeCite;
                    continue;
                }
                Enriched.Add(line);
                state = State.Done;
                continue;
            }
            if (state == State.AfterDateAndDocTypeBeforeCite) {
                if (block is not WLine line) {
                    Enriched.Add(block);
                    state = State.Done;
                    continue;
                }
                if (IsRed(line)) {
                    WRestriction restriction = new WRestriction(line);
                    Enriched.Add(restriction);
                    continue;
                }
                WLine enriched1 = EnrichCite(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.Done;
                    continue;
                }
                if (line.NormalizedContent.Contains(@" v ", StringComparison.InvariantCultureIgnoreCase)) {
                    WLine title = WDocTitle2.ConvertContents(line);
                    Enriched.Add(title);
                    continue;
                }
                if (line.NormalizedContent.Contains(@"In the matter of ", StringComparison.InvariantCultureIgnoreCase)) {
                    WLine title = WDocTitle2.ConvertContents(line);
                    Enriched.Add(title);
                    continue;
                }
                if (line.NormalizedContent.StartsWith(@"REFERENCE ")) {
                    Enriched.Add(line);
                    continue;
                }
                Enriched.Add(line);
                state = State.Done;
                continue;
            }
            if (state == State.Done) {
                Enriched.Add(block);
                while (Enumerator.MoveNext())
                    Enriched.Add(Enumerator.Current);
                return Enriched;
            }
        }
        return Blocks;
    }

    private bool IsRed(WLine line) {
        return line.Contents.All(IsRedOrEmpty);
    }
    private static bool IsRedOrEmpty(IInline inline) {
        if (inline is not WText text)
            return false;
        if (string.IsNullOrWhiteSpace(text.Text))
            return true;
        // if ("FF0000".Equals(text.FontColor, StringComparison.InvariantCultureIgnoreCase))
        //     return true;
        if (text.FontColor is not null && Regex.IsMatch(text.FontColor, "^[0-9A-F]{2}0{4}$", RegexOptions.IgnoreCase))
            return true;
        return false;
    }

    private WLine EnrichDate(WLine line) {
        return Date0.Enrich(line, "release", 1);
    }

    private WLine EnrichDocType(WLine line) {
        if (!line.Contents.All(i => i is IFormattedText))
            return line;
        if (!string.Equals(line.NormalizedContent, "Press Summary", StringComparison.OrdinalIgnoreCase))
            return line;
        WDocType docType = new WDocType(line.Contents.Cast<IFormattedText>());
        return WLine.Make(line, new List<IInline>(1) { docType });
    }

    private WLine EnrichCite(WLine line) {
        // WLine enriched = EnrichLastText(line);
        // if (!Object.ReferenceEquals(enriched, line))
        //     return enriched;
        // enriched = EnrichFromEnd(line);
        // if (!Object.ReferenceEquals(enriched, line))
        //     return enriched;
        // return EnrichWholeLine(line);
        return EnrichFromEnd(line);
    }

    private static string pattern1 = @"(\[\d{4}\] (UKSC|UKPC) \d+)\.? *$";

    // private static WLine EnrichLastText(WLine line) {
    //     if (line.Contents.LastOrDefault() is not WText text)
    //         return line;
    //     Match match = Regex.Match(text.Text, pattern1);
    //     if (!match.Success)
    //         return line;
    //     List<IInline> replacement = Helper.SplitOnGroup(text, match.Groups[1], (txt, rProps) => new WNeutralCitation(txt, rProps));
    //     return WLine.Make(line, Enumerable.Concat( line.Contents.SkipLast(1), replacement ));
    // }

    private static WLine EnrichFromEnd(WLine line) {
        IEnumerator<IInline> reversed = line.Contents.Reverse().GetEnumerator();
        string end = "";
        while (reversed.MoveNext()) {
            if (reversed.Current is not WText wText)
                return line;
            end = wText.Text + end;
            Match match = Regex.Match(end, pattern1);
            if (match.Success) {
                List<IInline> replacement = new List<IInline>();
                Group group = match.Groups[1];
                if (group.Index > 0) {
                    WText leading = new WText(end.Substring(0, group.Index), wText.properties);
                    replacement.Add(leading);
                }
                IInline middle = new WNeutralCitation(group.Value, wText.properties);
                replacement.Add(middle);
                if (group.Index + group.Length < end.Length) {
                    WText trailing = new WText(end.Substring(group.Index + group.Length), (line.Contents.Last() as WText).properties);
                    replacement.Add(trailing);
                }
                while (reversed.MoveNext())
                    replacement.Insert(0, reversed.Current);
                return WLine.Make(line, replacement);
            }
        }
        return line;
    }

    // private static string pattern2 = @"^\[\d{4}\] (UKSC|UKPC) \d+$";

    // private static WLine EnrichWholeLine(WLine line) {
    //     if (!line.Contents.All(inline => inline is IFormattedText))
    //         return line;
    //     Match match = Regex.Match(line.NormalizedContent, pattern2);
    //     if (!match.Success)
    //         return line;
    //     WNeutralCitation2 ncn = new WNeutralCitation2 { Contents = line.Contents.Cast<IFormattedText>() };
    //     return WLine.Make(line, new List<IInline>(1) { ncn });
    // }

}

}
