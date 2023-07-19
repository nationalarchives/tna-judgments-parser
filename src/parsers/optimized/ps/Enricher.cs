
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

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
        AfterCiteBeforeOnAppealFrom,
        AfterOnAppealFromBeforeJustices,
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
                if (IsRestriction(line)) {
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
                if (IsRestriction(line)) {
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
                if (IsRestriction(line)) {
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
                // some have no date
                enriched1 = EnrichCite(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.AfterCiteBeforeOnAppealFrom;
                    continue;
                }
                if (IsCaseName(line)) {
                    WLine title = WDocTitle2.ConvertContents(line);
                    Enriched.Add(title);
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
                if (IsRestriction(line)) {
                    WRestriction restriction = new WRestriction(line);
                    Enriched.Add(restriction);
                    continue;
                }
                if (line.NormalizedContent.StartsWith("Reference by ")) {
                    Enriched.Add(line);
                    continue;
                }
                WLine enriched1 = EnrichCite(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.AfterCiteBeforeOnAppealFrom;
                    continue;
                }
                if (IsCaseName(line)) {
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
            if (state == State.AfterCiteBeforeOnAppealFrom) {
                if (block is not WLine line) {
                    Enriched.Add(block);
                    state = State.Done;
                    continue;
                }
                WLine enriched1 = EnrichOnAppealFrom(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.AfterOnAppealFromBeforeJustices;
                    continue;
                }
                if (line.NormalizedContent.StartsWith("On appeal from ")) {  // for NICA
                    Enriched.Add(enriched1);
                    state = State.AfterOnAppealFromBeforeJustices;
                    continue;
                }
                Enriched.Add(line);
                state = State.Done;
                continue;
            }
            if (state == State.AfterOnAppealFromBeforeJustices) {
                if (block is not WLine line) {
                    Enriched.Add(block);
                    state = State.Done;
                    continue;
                }
                WLine enriched1 = EnrichJustices(line);
                if (!Object.ReferenceEquals(enriched1, line)) {
                    Enriched.Add(enriched1);
                    state = State.Done;
                    continue;
                }
                Enriched.Add(line);
                state = State.Done;
                continue;
            }
            if (state == State.Done) {
                Enriched.Add(block);
            }
        }
        return Enriched;
    }

    /* helper functions */

    private bool IsRestriction(WLine line) {
        if (line.NormalizedContent.StartsWith(@"Reporting Restrictions Apply", StringComparison.InvariantCultureIgnoreCase))
            return true;
        if (IsRed(line))
            return true;
        return false;
    }
    private bool IsRed(WLine line) {
        return line.Contents.All(IsRedOrEmpty);
    }
    private static bool IsRedOrEmpty(IInline inline) {
        if (inline is not WText text)
            return false;
        if (string.IsNullOrWhiteSpace(text.Text))
            return true;
        if (text.FontColor is not null && Regex.IsMatch(text.FontColor, "^[0-9A-F]{2}0{4}$", RegexOptions.IgnoreCase))
            return true;
        return false;
    }

    private WLine EnrichDate(WLine line) {
        return Date0.Enrich(line, "release", 1);
    }

    private WLine EnrichDocType(WLine line) {
        if (!line.Contents.All(i => i is WText))
            return line;
        if (!string.Equals(line.NormalizedContent, "Press Summary", StringComparison.OrdinalIgnoreCase))
            return line;
        IEnumerable<WText> contents = line.Contents.Cast<WText>();
        if (contents.Count() == 1) {
            WDocType1 docType1 = new WDocType1(contents.First());
            return WLine.Make(line, new List<IInline>(1) { docType1 });
        }
        WDocType2 docType = new WDocType2(contents);
        return WLine.Make(line, new List<IInline>(1) { docType });
    }

    private bool IsCaseName(WLine line) {
        if (line.NormalizedContent.Contains(@" v ", StringComparison.InvariantCultureIgnoreCase))
            return true;
        if (line.NormalizedContent.Contains(@"In the matter of ", StringComparison.InvariantCultureIgnoreCase))
            return true;
        if (line.NormalizedContent.Contains("(Appellant)") && line.NormalizedContent.Contains("(Respondent)"))
            return true;
        if (line.NormalizedContent.Contains("(Applicant)") && line.NormalizedContent.Contains("(Intervener)"))
            return true;
        return false;
    }

    private WLine EnrichCite(WLine line) {
        string pattern = @"(\[\d{4}\] (UKSC|UKPC) \d+)\.? *$";
        var constructor = (string text, RunProperties rProps) => new WNeutralCitation(text, rProps);
        return EnrichFromEnd(line, pattern, constructor, true);
    }

    private static WLine EnrichFromEnd(WLine line, string pattern, Func<string, RunProperties, IInline> constructor, bool wrapBeforeInDocTitle = false) {
        IEnumerator<IInline> reversed = line.Contents.Reverse().GetEnumerator();
        string end = "";
        while (reversed.MoveNext()) {
            if (reversed.Current is not WText wText)
                return line;
            end = wText.Text + end;
            Match match = Regex.Match(end, pattern);
            if (match.Success) {
                List<IInline> before = new List<IInline>();
                List<IInline> replacement = new List<IInline>();
                Group group = match.Groups[1];
                if (group.Index > 0) {
                    WText leading = new WText(end.Substring(0, group.Index), wText.properties);
                    before.Add(leading);
                }
                IInline middle = constructor(group.Value, wText.properties);
                replacement.Add(middle);
                if (group.Index + group.Length < end.Length) {
                    WText trailing = new WText(end.Substring(group.Index + group.Length), (line.Contents.Last() as WText).properties);
                    replacement.Add(trailing);
                }
                while (reversed.MoveNext())
                    before.Insert(0, reversed.Current);
                if (!wrapBeforeInDocTitle)
                    return WLine.Make(line, Enumerable.Concat(before, replacement));
                if (string.IsNullOrWhiteSpace(IInline.ToString(before)))
                    return WLine.Make(line, Enumerable.Concat(before, replacement));
                if (before.Count == 1 && before.First() is WText wText1) {
                    string wTest1Trimmed = wText1.Text.TrimEnd();
                    WDocTitle docTitle1;
                    if (wTest1Trimmed.Length == wText1.Text.Length) {
                        docTitle1 = new WDocTitle(wText1);
                        return WLine.Make(line, replacement.Prepend(docTitle1));
                    }
                    docTitle1 = new WDocTitle(wTest1Trimmed, wText1.properties);
                    WText space = new WText(wText1.Text.Substring(wTest1Trimmed.Length), wText1.properties);
                    return WLine.Make(line, replacement.Prepend(space).Prepend(docTitle1));
                }
                WDocTitle2 docTitle2 = new WDocTitle2 { Contents = before };
                return WLine.Make(line, replacement.Prepend(docTitle2));
            }
        }
        return line;
    }

    private WLine EnrichOnAppealFrom(WLine line) {
        if (!line.NormalizedContent.StartsWith("On appeal from"))
            return line;
        string pattern = @"(\[\d{4}\] EWCA (Civ|Crim) \d+)\.? *$";  // NICA?
        var constructor = (string text, RunProperties rProps) => {
            var normalized = Citations.Normalize(text);
            var prefix = "https://caselaw.nationalarchives.gov.uk/";
            var url = prefix + Citations.MakeUriComponent(normalized);
            return new WRef(text, rProps) {
                Href = url,
                Canonical = normalized,
                IsNeutral = true,
                Type = RefType.Case
            };
        };
        return EnrichFromEnd(line, pattern, constructor);
    }

    private WLine EnrichJustices(WLine line) {
        if (!line.NormalizedContent.StartsWith("Justices:", StringComparison.InvariantCultureIgnoreCase))
            return line;
        List<IInline> replacement = new List<IInline>();
        IEnumerator<IInline> reversed = line.Contents.Reverse().GetEnumerator();
        string pattern = @"([:,] +)([[A-Z][A-Za-z \(]+?[a-z\)]) *$";
        while (reversed.MoveNext()) {
            if (reversed.Current is not WText wText) {
                replacement.Insert(0, reversed.Current);
                break;
            }
            string text = wText.Text;
            Match match = Regex.Match(text, pattern);
            while (match.Success) {
                Group commaGroup = match.Groups[1];
                Group nameGroup = match.Groups[2];
                string after = text.Substring(nameGroup.Index + nameGroup.Length);

                if (!string.IsNullOrEmpty(after)) {
                    WText wText2 = new WText(after, wText.properties);
                    replacement.Insert(0, wText2);
                }

                WJudge judge = new WJudge(nameGroup.Value, wText.properties);
                replacement.Insert(0, judge);

                WText comma = new WText(commaGroup.Value, wText.properties);
                replacement.Insert(0, comma);

                text = text.Substring(0, commaGroup.Index);
                match = Regex.Match(text, pattern);
            }
            if (!string.IsNullOrEmpty(text)) {
                WText wText2 = new WText(text, wText.properties);
                replacement.Insert(0, wText2);
            }
        }
        while (reversed.MoveNext())
            replacement.Insert(0, reversed.Current);
        return WLine.Make(line, replacement);
    }

}

}
