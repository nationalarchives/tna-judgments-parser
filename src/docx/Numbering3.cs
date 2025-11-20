
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
            if (ctx.TryGetCachedN(paragraph, ilvl, out int cached))
                return cached;

            Style style = Styles.GetStyle(ctx.Main, paragraph) ?? Styles.GetDefaultParagraphStyle(ctx.Main);

            int? ownNumId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
            // TODO consider paragraph.ParagraphProperties?.NumberingProperties?.NumberingChange?.Id?.Value
            int? styleNumId = Styles.GetStyleProperty(style, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);
            int? numId = ownNumId ?? styleNumId;


            if (numId is null)
                return 0;
            Paragraph previous = FindPreviousParagraph(ctx, paragraph, numId.Value, ilvl);

            int value;
            if (previous is null)
                value = GetStartValue(ctx, numId.Value, ilvl);
            else
                value = CalculateN(ctx, previous, ilvl) + 1;

            ctx.SetCachedN(paragraph, ilvl, value);
            return value;
        }

        private static Paragraph FindPreviousParagraph(NumberingContext ctx, Paragraph paragraph, int numId, int ilvl)
        {
            Paragraph prev = paragraph.PreviousSibling<Paragraph>();
            while (prev != null)
            {
                Style prevStyle = Styles.GetStyle(ctx.Main, prev) ?? Styles.GetDefaultParagraphStyle(ctx.Main);
                int? prevOwnNumId = prev.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
                // TODO consider prev.ParagraphProperties?.NumberingProperties?.NumberingChange?.Id?.Value
                int? prevStyleNumId = Styles.GetStyleProperty(prevStyle, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value);
                int? prevNumId = prevOwnNumId ?? prevStyleNumId;

                int? prevOwnIlvl = prev.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
                int? prevStyleIlvl = Styles.GetStyleProperty(prevStyle, s => s.StyleParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value);
                int prevIlvl = prevOwnIlvl ?? prevStyleIlvl ?? 0; // This differs a bit from Numbering.GetNumberingIdAndIlvl

                if (prevNumId.HasValue && prevNumId.Value == numId && prevIlvl == ilvl)
                    return prev;

                prev = prev.PreviousSibling<Paragraph>();
            }
            return null;
        }

        private static int GetStartValue(NumberingContext ctx, int numId, int ilvl)
        {
            var level = Numbering.GetLevel(ctx.Main, numId, ilvl);
            int? start = level?.StartNumberingValue?.Val?.Value;
            if (start.HasValue)
                return start.Value;
            return 1;
        }

    }

}
