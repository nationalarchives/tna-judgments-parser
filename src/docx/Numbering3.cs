
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

        private static void CalculateAllNumbers(NumberingContext ctx)
        {
            // abstractNumId -> ilvl -> (value, lastNumId). Shared counter state per abstract list.
            // We keep the last numId so continuations can inherit state.
            var counters = new Dictionary<int, Dictionary<int, (int value, int numId)>>();

            // numId -> abstractNumId cache to avoid repeated lookups
            var numIdToAbsNumId = new Dictionary<int, int>();

            // tracks which (numId, ilvl) start overrides already fired
            var startOverrideConsumed = new HashSet<(int numId, int ilvl)>();

            // tracks the last ilvl for each abstractNumId
            var lastIlvls = new Dictionary<int, int>();

            // tracks which (abstractNumId, ilvl) pairs have consumed a startOverride (test37, test87)
            var overrideConsumedAtLevel = new HashSet<(int absNumId, int ilvl)>();

            // helper to check if we should apply an override
            bool ShouldApplyOverride(int absNumId, int numId, int ilvl, out int overrideVal)
            {
                // test37, test87: check if we JUST came from a deeper level that consumed override
                bool justCameFromDeeperWithOverride = lastIlvls.TryGetValue(absNumId, out int prevIlvl) &&
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

                Style style = Styles.GetStyle(ctx.Main, paragraph) ?? Styles.GetDefaultParagraphStyle(ctx.Main);

                int? ownNumId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
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

                if (!numIdToAbsNumId.TryGetValue(numId, out var absNumId))
                {
                    int? absOpt = GetAbstractNumId(ctx, numId);
                    if (!absOpt.HasValue)
                        continue;
                    absNumId = absOpt.Value;
                    numIdToAbsNumId[numId] = absNumId;
                }

                if (!counters.ContainsKey(absNumId))
                    counters[absNumId] = new Dictionary<int, (int value, int numId)>();
                var ilvlCounters = counters[absNumId];

                // reset deeper counters
                var levelsToReset = ilvlCounters.Keys.Where(l => l > ilvl).ToList();
                foreach (var l in levelsToReset)
                    ilvlCounters.Remove(l);

                // Cache parent level values for this paragraph (for formats like "%1.%2")
                for (int parentLevel = 0; parentLevel < ilvl; parentLevel++)
                {
                    int parentValue;
                    if (ilvlCounters.TryGetValue(parentLevel, out var parentState))
                        parentValue = parentState.value;
                    else
                        parentValue = Numbering2.GetStart(ctx.Main, numId, parentLevel);
                    ctx.SetCachedN(paragraph, parentLevel, parentValue);
                }

                int newValue;
                if (ilvlCounters.TryGetValue(ilvl, out var current))
                {
                    // If this is a different numId, check if it has a startOverride
                    if (current.numId != numId)
                    {
                        if (ShouldApplyOverride(absNumId, numId, ilvl, out int overrideValue))
                            newValue = overrideValue;
                        else
                            newValue = current.value + 1;
                    }
                    else
                    {
                        newValue = current.value + 1;
                    }
                }
                else
                {
                    // check if we are returning from a deeper level
                    if (lastIlvls.TryGetValue(absNumId, out int lastIlvl) && lastIlvl > ilvl)
                    {
                        newValue = Numbering2.GetStart(ctx.Main, numId, ilvl) + 1;
                    }
                    else
                    {
                        if (ShouldApplyOverride(absNumId, numId, ilvl, out int overrideValue))
                            newValue = overrideValue;
                        else
                            newValue = Numbering2.GetStart(ctx.Main, numId, ilvl);
                    }
                }

                ilvlCounters[ilvl] = (newValue, numId);
                lastIlvls[absNumId] = ilvl;
                ctx.SetCachedN(paragraph, ilvl, newValue);
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
