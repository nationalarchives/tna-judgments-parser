
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX
{
    partial class Numbering3
    {

        private readonly record struct LevelCounter(
            int Value,
            int NumId,
            string? StyleId,
            bool HasExplicitNumId);

        private class NumberingContext
        {
            internal MainDocumentPart Main { get; }
            internal bool NumbersHaveBeenCalculated = false;

            private readonly ConditionalWeakTable<Paragraph, ParagraphState> _states = [];

            private sealed class ParagraphState
            {
                internal readonly Dictionary<int, int> CountersByIlvl = [];
            }

            internal NumberingContext(MainDocumentPart main)
            {
                Main = main;
            }

            internal bool TryGetCachedN(Paragraph paragraph, int ilvl, out int value)
            {
                ParagraphState state;
                if (!_states.TryGetValue(paragraph, out state))
                {
                    value = default;
                    return false;
                }
                if (!state.CountersByIlvl.TryGetValue(ilvl, out value))
                    return false;
                return true;
            }

            internal void SetCachedN(Paragraph paragraph, int ilvl, int value)
            {
                ParagraphState state = _states.GetValue(paragraph, _ => new ParagraphState());
                state.CountersByIlvl[ilvl] = value;
            }

        }

        private static readonly ConditionalWeakTable<MainDocumentPart, NumberingContext> Contexts = [];

        private static NumberingContext GetContext(MainDocumentPart main) => Contexts.GetValue(main, m => new(m));


        internal static int CalculateN(Paragraph paragraph, int ilvl)
        {
            MainDocumentPart main = Main.Get(paragraph);
            return CalculateN(GetContext(main), paragraph, ilvl);
        }

        internal static int CalculateN(MainDocumentPart main, Paragraph paragraph, int ilvl)
        {
            NumberingContext ctx = GetContext(main);
            return CalculateN(ctx, paragraph, ilvl);
        }

        private static int CalculateN(NumberingContext ctx, Paragraph paragraph, int ilvl)
        {
            if (!ctx.NumbersHaveBeenCalculated)
            {
                CalculateAllNumbers(ctx);
                ctx.NumbersHaveBeenCalculated = true;
            }
            if (ctx.TryGetCachedN(paragraph, ilvl, out int cached))
                return cached;
            return 0;
        }

        private static bool ShouldSkipReset(
            NumberingContext ctx,
            Style currentStyle,
            string? targetStyleId,
            bool currentHasExplicitNumId,
            bool targetHasExplicitNumId,
            int currentIlvl)
        {
            // Root-level paragraphs always reset their children (legacy behavior, see test76).
            if (currentIlvl == 0)
                return false;

            if (targetHasExplicitNumId || currentHasExplicitNumId)
                return false;

            if (currentStyle is null || string.IsNullOrEmpty(targetStyleId))
                return false;

            string currentStyleId = currentStyle.StyleId?.Value;
            if (currentStyleId == targetStyleId)
                return true;

            string basedOn = currentStyle.BasedOn?.Val?.Value;
            while (basedOn != null)
            {
                if (basedOn == targetStyleId)
                    return true;

                Style baseStyle = Styles.GetStyle(ctx.Main, basedOn);
                if (baseStyle == null)
                    break;

                basedOn = baseStyle.BasedOn?.Val?.Value;
            }
            return false;
        }

        private static void CalculateAllNumbers(NumberingContext ctx)
        {
            // abstractNumId -> ilvl -> (value, lastNumId). Shared counter state per abstract list.
            // We keep the last numId so continuations can inherit state.
            var counters = new Dictionary<int, Dictionary<int, LevelCounter>>();

            // numId -> abstractNumId cache to avoid repeated lookups
            var numIdToAbsNumId = new Dictionary<int, int>();

            // tracks which (numId, ilvl) start overrides already fired
            var startOverrideConsumed = new HashSet<(int numId, int ilvl)>();

            // tracks the last ilvl for each abstractNumId
            var lastIlvls = new Dictionary<int, int>();

            // tracks which (abstractNumId, ilvl) pairs have consumed a startOverride (test37, test87)
            var overrideConsumedAtLevel = new HashSet<(int absNumId, int ilvl)>();

            // tracks which numbering instance currently "owns" each level in an abstract list.
            // We reuse this both for parent/child relationships so we can detect when a child
            // level is running under a different parent instance (test46).
            var levelOwners = new Dictionary<(int absNumId, int ilvl), int>();

            // tracks which numIds have been seen globally to distinguish fresh lists from continuations
            var seenNumIds = new HashSet<int>();

            // helper to check if we should apply an override
            bool ShouldApplyOverride(int absNumId, int numId, int ilvl, bool requiresParentOwner, bool isGloballyNew, out int overrideVal)
            {
                // Style-only paragraphs inherit their numbering identity from the parent style.
                // When a sibling with an explicit numId temporarily owns the parent level, Word
                // suppresses the child's override so the list remains sequential (test46 copy).
                if (requiresParentOwner && ilvl > 0 &&
                    levelOwners.TryGetValue((absNumId, ilvl - 1), out int parentOwner) && parentOwner != numId)
                {
                    overrideVal = default;
                    return false;
                }

                // test37, test87: check if we JUST came from a deeper level that consumed override
                // test66: BUT if we are a GLOBALLY new numId with an EXPLICIT override, we are a fresh list instance and should ignore sibling history
                bool hasExplicit = HasExplicitOverride(ctx.Main, numId, ilvl);
                bool justCameFromDeeperWithOverride = (!isGloballyNew || !hasExplicit) &&
                                                       lastIlvls.TryGetValue(absNumId, out int prevIlvl) &&
                                                       prevIlvl > ilvl &&
                                                       overrideConsumedAtLevel.Contains((absNumId, prevIlvl));

                if (justCameFromDeeperWithOverride)
                {
                    overrideVal = default;
                    return false;
                }

                return TryApplyStartOverride(ctx, absNumId, numId, ilvl, startOverrideConsumed, overrideConsumedAtLevel, out overrideVal);
            }

            foreach (var paragraph in ctx.Main.Document.Body.Descendants<Paragraph>())
            {
                if (Paragraphs.IsDeleted(paragraph))
                    continue;
                if (Paragraphs.IsEmptySectionBreak(paragraph))
                    continue;
                if (Paragraphs.IsMergedWithFollowing(paragraph))
                    continue;

                var merge = (paragraph.Parent as TableCell)?.TableCellProperties?.VerticalMerge;
                if (merge is not null
                    && (merge.Val is null || merge.Val == MergedCellValues.Continue)
                    && string.IsNullOrEmpty(paragraph.InnerText))
                {
                    continue; // skip empty paragraphs in vertically merged table cells (test97)
                }

                Style style = Styles.GetStyle(ctx.Main, paragraph) ?? Styles.GetDefaultParagraphStyle(ctx.Main);

                int? ownNumId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
                bool hasExplicitNumId = ownNumId.HasValue;
                // TODO consider paragraph.ParagraphProperties?.NumberingProperties?.NumberingChange?.Id?.Value
                int? styleNumId = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);
                int? numIdOpt = ownNumId ?? styleNumId;
                if (!numIdOpt.HasValue)
                    continue;
                int numId = numIdOpt.Value;

                int? ownIlvl;

                if (numId == 0)
                {
                    if (TryListNum(paragraph, out int listNumId, out int listIlvl))
                    {
                        ownNumId = listNumId;
                        numId = listNumId;
                        ownIlvl = listIlvl;
                        hasExplicitNumId = true;
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    ownIlvl = paragraph.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
                }

                int? styleIlvl = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value);
                int ilvl = ownIlvl ?? styleIlvl ?? 0; // This differs a bit from Numbering.GetNumberingIdAndIlvl

                bool requiresParentOwner = !hasExplicitNumId && styleNumId.HasValue;

                if (!numIdToAbsNumId.TryGetValue(numId, out var absNumId))
                {
                    int? absOpt = GetAbstractNumId(ctx, numId);
                    if (!absOpt.HasValue)
                        continue;
                    absNumId = absOpt.Value;
                    numIdToAbsNumId[numId] = absNumId;
                }

                bool isGloballyNew = seenNumIds.Add(numId);

                if (!counters.ContainsKey(absNumId))
                    counters[absNumId] = new Dictionary<int, LevelCounter>();
                var ilvlCounters = counters[absNumId];

                // reset deeper counters
                var levelsToReset = ilvlCounters.Keys.Where(l => l > ilvl).ToList();
                foreach (var l in levelsToReset)
                {
                    LevelCounter counter = ilvlCounters[l];
                    if (ShouldSkipReset(ctx, style, counter.StyleId, hasExplicitNumId, counter.HasExplicitNumId, ilvl))
                        continue;
                    // If lvlRestart=0, this level should never restart (test94)
                    if (LevelNeverRestarts(ctx.Main, absNumId, l))
                        continue;
                    ilvlCounters.Remove(l);
                }

                // Cache parent level values for this paragraph (for formats like "%1.%2")
                for (int parentLevel = 0; parentLevel < ilvl; parentLevel++)
                {
                    int parentValue;
                    if (ilvlCounters.TryGetValue(parentLevel, out var parentState))
                        parentValue = parentState.Value;
                    else
                        parentValue = GetBaseStart(ctx, absNumId, numId, parentLevel);
                    ctx.SetCachedN(paragraph, parentLevel, parentValue);
                }

                int newValue;
                if (ilvlCounters.TryGetValue(ilvl, out var current))
                {
                    // If this is a different numId, check if it has a startOverride
                    if (current.NumId != numId)
                    {
                        if (ShouldApplyOverride(absNumId, numId, ilvl, requiresParentOwner, isGloballyNew, out int overrideValue))
                            newValue = overrideValue;
                        else
                            newValue = current.Value + 1;
                    }
                    else
                    {
                        newValue = current.Value + 1;
                    }
                }
                else
                {
                    // No existing counter at this level
                    bool returningFromDeeper = lastIlvls.TryGetValue(absNumId, out int lastIlvl) && lastIlvl > ilvl;

                    if (ShouldApplyOverride(absNumId, numId, ilvl, requiresParentOwner, isGloballyNew, out int overrideValue))
                    {
                        // Override applies, use it as-is (don't add +1 even if returning from deeper)
                        newValue = overrideValue;
                    }
                    else if (returningFromDeeper)
                    {
                        // Returning from deeper level without override: increment from base
                        newValue = GetBaseStart(ctx, absNumId, numId, ilvl) + 1;
                    }
                    else
                    {
                        // Fresh start at this level - use abstract start, not Level start,
                        // since any startOverride has already been consumed
                        newValue = Numbering2.GetAbstractStart(ctx.Main, absNumId, ilvl);
                    }
                }

                string styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? style.StyleId?.Value;
                ilvlCounters[ilvl] = new LevelCounter(newValue, numId, styleId, hasExplicitNumId);
                lastIlvls[absNumId] = ilvl;
                levelOwners[(absNumId, ilvl)] = numId;
                ctx.SetCachedN(paragraph, ilvl, newValue);

                HandleNamedListNum(ctx, paragraph, counters);
            }
        }

        private static int? GetAbstractNumId(NumberingContext ctx, int numId)
        {
            NumberingInstance instance = Numbering.GetNumbering(ctx.Main, numId);
            if (instance is null)
                return null;
            AbstractNum abstractNum = Numbering.GetAbstractNum(ctx.Main, instance);
            return abstractNum.AbstractNumberId;
        }

        [GeneratedRegex(@"^ ?LISTNUM (\d+) \\l (\d)")]
        private static partial Regex ListNumRegex();

        private static bool TryListNum(Paragraph paragraph, out int numId, out int ilvl)
        {
            Match match = ListNumRegex().Match(paragraph.InnerText);
            if (!match.Success)
            {
                numId = default;
                ilvl = default;
                return false;
            }
            numId = int.Parse(match.Groups[1].Value);
            ilvl = int.Parse(match.Groups[2].Value) - 1; // ilvl indexes are 0 based
            return true;
        }


        [GeneratedRegex(@"^ ?LISTNUM ""(?<name>[A-Za-z][A-Za-z0-9]*)"" \\l (?<l>\d)( \\s (?<s>\d+))? $")]
        private static partial Regex NamedListNumRegex();

        // KNOWN LIMITATIONS:
        // - Does not respect custom abstract start values from the numbering definition
        // - Does not handle parent level dependencies/resets
        // - Does not update lastIlvls[absNumId] or levelOwners[(absNumId, ilvl)]
        private static void HandleNamedListNum(NumberingContext ctx, Paragraph paragraph, Dictionary<int, Dictionary<int, LevelCounter>> counters)
        {
            Match match = paragraph.Descendants<Run>()
                .Select(r => NamedListNumRegex().Match(r.InnerText))
                .Where(m => m.Success)
                .FirstOrDefault();
            if (match is null)
                return;

            string name = match.Groups["name"].Value;
            AbstractNum absNum = Numbering.GetAbstractNum(ctx.Main, name);
            int absNumId = absNum.AbstractNumberId;
            int ilvl = int.Parse(match.Groups["l"].Value) - 1; // ilvl indexes are 0 based

            if (!counters.ContainsKey(absNumId))
                counters[absNumId] = [];
            var ilvlCounters = counters[absNumId];

            var start = match.Groups["s"].Success ? int.Parse(match.Groups["s"].Value) : 1;

            ilvlCounters[ilvl] = new LevelCounter(start, -1, null, false);
            // lastIlvls[absNumId] = ilvl;
            ctx.SetCachedN(paragraph, ilvl, start);
        }

        private static bool HasExplicitOverride(MainDocumentPart main, int numId, int ilvl)
        {
            NumberingInstance instance = Numbering.GetNumbering(main, numId);
            if (instance == null)
                return false;
            foreach (LevelOverride lo in instance.Descendants<LevelOverride>())
            {
                if (lo.LevelIndex != null && lo.LevelIndex.Value == ilvl && lo.StartOverrideNumberingValue != null)
                    return true;
            }
            return false;
        }

        private static int GetBaseStart(NumberingContext ctx, int absNumId, int numId, int ilvl)
        {
            NumberingInstance instance = Numbering.GetNumbering(ctx.Main, numId);
            if (instance != null)
            {
                foreach (Level level in instance.Descendants<Level>())
                {
                    if (level.LevelIndex?.Value == ilvl && level.StartNumberingValue?.Val?.Value is int start)
                        return start;
                }
            }
            return Numbering2.GetAbstractStart(ctx.Main, absNumId, ilvl);
        }

        // Checks if an abstract level has lvlRestart=0, meaning it should never restart its numbering,
        // even when returning from a deeper level (test94).
        private static bool LevelNeverRestarts(MainDocumentPart main, int absNumId, int ilvl)
        {
            Level level = Numbering.GetLevelAbstract(main, absNumId, ilvl);
            int? restart = level?.LevelRestart?.Val?.Value;
            return restart.HasValue && restart.Value == 0;
        }

        // Word's <w:startOverride> applies only once per numbering instance and level.
        // We memoize each (numId, ilvl) pair so that the override seeds the counter for
        // the very first paragraph of that sequence, and subsequent paragraphs just increment.
        private static bool TryApplyStartOverride(NumberingContext ctx, int absNumId, int numId, int ilvl,
            HashSet<(int numId, int ilvl)> consumed, HashSet<(int absNumId, int ilvl)> overrideConsumedAtLevel, out int value)
        {
            if (consumed.Contains((numId, ilvl)))
            {
                value = default;
                return false;
            }
            int? startOverride = Numbering2.GetStartOverride(ctx.Main, numId, ilvl);
            if (startOverride.HasValue)
            {
                value = startOverride.Value;
                consumed.Add((numId, ilvl));
                overrideConsumedAtLevel.Add((absNumId, ilvl));
                return true;
            }
            value = default;
            return false;
        }

    }

}
