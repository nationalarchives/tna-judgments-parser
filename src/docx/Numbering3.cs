
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.DOCX
{
    class Numbering3
    {

        class NumberingContext
        {
            internal MainDocumentPart Main { get; }

            private readonly ConditionalWeakTable<Paragraph, ParagraphState> _states = [];

            private sealed class ParagraphState
            {
                internal readonly Dictionary<int, int> CountersByIlvl = new();
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
            if (ctx.TryGetCachedN(paragraph, ilvl, out int cached))
                return cached;

            int? numId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
            if (numId is null)
                return 0;

            Paragraph? previous = FindPreviousParagraph(ctx, paragraph, numId.Value, ilvl);
            int value;
            if (previous is null)
            {
                value = 1;
            }
            else
            {
                value = CalculateN(ctx, previous, ilvl) + 1;
            }

            ctx.SetCachedN(paragraph, ilvl, value);
            return value;
        }

        private static Paragraph? FindPreviousParagraph(NumberingContext ctx, Paragraph paragraph, int numId, int ilvl)
        {
            Body? body = ctx.Main.Document?.Body;
            if (body is null)
                return null;

            Paragraph? last = null;
            foreach (Paragraph candidate in body.Elements<Paragraph>())
            {
                if (ReferenceEquals(candidate, paragraph))
                    break;

                var info = UK.Gov.Legislation.Judgments.DOCX.Numbering.GetNumberingIdAndIlvl(ctx.Main, candidate);
                if (!info.Item1.HasValue)
                    continue;
                if (info.Item1.Value == numId && info.Item2 == ilvl)
                    last = candidate;
            }
            return last;
        }

    }

}
