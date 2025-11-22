
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
            // abstractNumId -> ilvl -> (value, lastNumId)
            var counters = new Dictionary<int, Dictionary<int, (int value, int numId)>>();

            var numIdToAbsNumId = new Dictionary<int, int>();

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

                if (numId == 0 && TryHandleListNum(ctx, paragraph, counters))
                    continue;

                if (!numIdToAbsNumId.TryGetValue(numId, out var absNumId))
                {
                    int? absOpt = GetAbstractNumId(ctx, numId);
                    if (!absOpt.HasValue)
                        continue;
                    absNumId = absOpt.Value;
                    numIdToAbsNumId[numId] = absNumId;
                }

                int? ownIlvl = paragraph.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
                int? styleIlvl = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value);
                int ilvl = ownIlvl ?? styleIlvl ?? 0; // This differs a bit from Numbering.GetNumberingIdAndIlvl

                if (!counters.ContainsKey(absNumId))
                    counters[absNumId] = new Dictionary<int, (int value, int numId)>();
                var ilvlCounters = counters[absNumId];

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

                // When a paragraph appears at a given level,
                // it implicitly restarts the numbering of any deeper levels.
                var levelsToReset = ilvlCounters.Keys.Where(l => l > ilvl).ToList();
                foreach (var l in levelsToReset)
                    ilvlCounters.Remove(l);

                int newValue;
                if (ilvlCounters.TryGetValue(ilvl, out var current))
                {
                    // If this is a different numId, check if it has a startOverride
                    if (current.numId != numId)
                    {
                        int? startOverride = Numbering2.GetStartOverride(ctx.Main, numId, ilvl);
                        if (startOverride.HasValue)
                            newValue = startOverride.Value;
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
                    newValue = Numbering2.GetStart(ctx.Main, numId, ilvl);
                }

                ilvlCounters[ilvl] = (newValue, numId);
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

        private static bool TryHandleListNum(NumberingContext ctx, Paragraph paragraph, Dictionary<int, Dictionary<int, (int value, int numId)>> counters)
        {
            Match match = ListNumRegex().Match(paragraph.InnerText);
            if (!match.Success)
                return false;

            int numId = int.Parse(match.Groups[1].Value);
            int ilvl = int.Parse(match.Groups[2].Value) - 1; // ilvl indexes are 0 based
            int? absOpt = GetAbstractNumId(ctx, numId);
            if (!absOpt.HasValue)
                return false;
            int absNumId = absOpt.Value;

            if (!counters.ContainsKey(absNumId))
                counters[absNumId] = new Dictionary<int, (int value, int numId)>();
            var ilvlCounters = counters[absNumId];

            int newValue;
            if (ilvlCounters.TryGetValue(ilvl, out var current))
            {
                if (current.numId != numId)
                {
                    int? startOverride = Numbering2.GetStartOverride(ctx.Main, numId, ilvl);
                    if (startOverride.HasValue)
                        newValue = startOverride.Value;
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
                newValue = Numbering2.GetStart(ctx.Main, numId, ilvl);
            }

            ilvlCounters[ilvl] = (newValue, numId);
            ctx.SetCachedN(paragraph, ilvl, newValue);
            return true;
        }

    }

}
