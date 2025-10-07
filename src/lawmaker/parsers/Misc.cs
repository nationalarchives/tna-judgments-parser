#nullable enable
using System.Collections.Generic;
using System.Linq;
using System;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Bibliography;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private int Save() => i;
        private void Restore(int save) => i = save;
        // Get the current block the parser is at
        internal IBlock Current() => Document.Body[i].Block;
        // Get the block that is `num` positions away.
        // `Peek(0)` will show the current block without advancing (same as `Current()`).
        // At the moment num can be negative to look behind. There are currently no safeguards
        // for checking within the bounds of the Document Body.
        internal IBlock Peek(int num = 1) => Document.Body[i+num].Block;

        // Move the parser forward and return the block the parser was on when `Advance()` was called.
        internal IBlock Advance() {
            IBlock current = Current();
            i++;
            return current;
        }

        // Advance the parser forward by `num` and returns to blocks passed.
        internal IEnumerable<IBlock> Advance(int num)
        {
            if (num <= 0) return [];
            var slice = Document.Body[i..(i + num)]
                .Select(block => block.Block);
            i += slice.Count();
            return slice;
        }

        // Move the parser forward while `condition` is true and return everything advanced over
        internal List<IBlock> AdvanceWhile(Predicate<IBlock> condition)
        {
            IEnumerable<IBlock> list = Document.Body[i..]
                .Select(block => block.Block)
                .TakeWhile(block => condition(block) && !IsAtEnd());
            Advance(list.Count());
            return list.ToList();
        }

        internal delegate T? ParseStrategy<T>(LegislationParser parser);
        // Attempts to match the current block with the supplied strategy.
        // If the strategy successfully matches then the result is returned.
        // If the strategy returns null (indicating the matching was unsuccessful) then the parser position is reset to before the `strategy` was called.
        // `strategy` is expected to update the state itself using `Advance` and `AdvanceWhile`
        internal T? Match<T>(ParseStrategy<T> strategy)
        {
            // TODO: memoize here if needed
            int save = this.Save();
            T? block = strategy(this);
            if (block == null) this.Restore(save);
            return block;
        }

        internal bool IsAtEnd() => i > Document.Body.Count;
        private IBlock? Previous() => i > 0 ? Document.Body[i-1].Block : null;

        private static bool IsLeftAligned(WLine line)
        {
            var alignment = line.GetEffectiveAlignment();
            return !alignment.HasValue || alignment == AlignmentValues.Left || alignment == AlignmentValues.Justify;
        }

        private static bool IsCenterAligned(WLine line)
        {
            var alignment = line.GetEffectiveAlignment();
            return alignment == AlignmentValues.Center;
        }

        private static bool IsRightAligned(WLine line)
        {
            var alignment = line.GetEffectiveAlignment();
            return alignment == AlignmentValues.Right;
        }

        private static string GetRightTabbedText(WLine line)
        {
            if (ContentHasTabbedText(line))
            {
                return new WLine(line, [line.Contents.Last()]).NormalizedContent;
            }
            return null;
        }

        private static string IgnoreRightTabbedText(WLine line)
        {
            if (ContentHasTabbedText(line))
            {
                return new WLine(line, line.Contents.SkipLast(1)).NormalizedContent;
            }
            return line.NormalizedContent;
        }

        private static bool ContentHasTabbedText(WLine line)
        {
            if (line.Contents.Count() >= 2 && line.Contents.Last() is WText && line.Contents.SkipLast(1).Last() is WTab)
                return true;
            return false;

            // Style style = Judgments.DOCX.Styles.GetStyle(line.main, line.properties.ParagraphStyleId);
            // Tabs tabs = style.StyleParagraphProperties.Tabs;
            // return tabs.ChildElements.Where(tab =>
            // {
            //     TabStop tabStop = tab as TabStop;
            //     return tabStop.Val == "right" && tabStop.Position > 5000;
            // }).Any();
        }

        private static bool IsFlushLeft(WLine line) => OptimizedParser.IsFlushLeft(line);

        private static float GetEffectiveIndent(WLine line) => OptimizedParser.GetEffectiveIndent(line);

        private bool CurrentLineIsIndentedLessThan(WLine parent)
        {
            if (Document.Body[i].Block is not WLine line)
                return false;
            return LineIsIndentedLessThan(line, parent);
        }

        private static bool LineIsIndentedLessThan(WLine line, WLine other, float threshold = 0f)
        {
            return GetEffectiveIndent(line) < GetEffectiveIndent(other) - threshold;
        }

        private static bool LineIsIndentedMoreThan(WLine line, WLine other, float threshold = 0f)
        {
            return GetEffectiveIndent(line) > GetEffectiveIndent(other) + threshold;
        }

        private static bool HasValidIndentForChild(IBlock block, WLine leader)
        {
            if (block is not WLine line)
                return true;
            if (!IsLeftAligned(line))
                return false;
            if (LineIsIndentedLessThan(line, leader))
                return false;
            // This was causing havoc with NI subsections
            /*
            if (line is WOldNumberedParagraph && !LineIsIndentedMoreThan(line, leader))
                return false;
            */
            return true;
        }

        private static bool NextChildIsAcceptable(List<IDivision> children, IDivision next)
        {
            if (children.Count == 0)
                return true;
            IDivision prev = children.Last();

            if (prev is CrossHeading != next is CrossHeading)
                return false;

            if (prev is Prov1 != next is Prov1)
                return false;
            if (prev is Prov1 && next is Prov1)
            {
                string num1 = prev.Number.Text.TrimEnd('.');
                string num2 = next.Number.Text.TrimEnd('.');
                if (num1.All(char.IsDigit) && num2.All(char.IsDigit))
                    return int.Parse(num1) == int.Parse(num2) - 1;
            }

            if (prev is Prov2 != next is Prov2)
                return false;
            if (prev is Prov2 && next is Prov2)
            {
                string num1 = prev.Number.Text.TrimStart('(').TrimEnd(')');
                string num2 = next.Number.Text.TrimStart('(').TrimEnd(')');
                if (num1.All(char.IsDigit) && num2.All(char.IsDigit))
                    return int.Parse(num1) == int.Parse(num2) - 1;
            }
            // if (prev is Para1 != next is Para1)
            //     return false;
            if (prev is Para1 && next is Para1)
            {
                string num1 = prev.Number.Text.TrimStart('(').TrimEnd(')');
                string num2 = next.Number.Text.TrimStart('(').TrimEnd(')');
                if (num1.Length == 1 && num2.Length == 1)
                    return num1[0] == (char)(num2[0] - 1);
            }
            // if (prev is Para2 !=  next is Para2)
            //     return false;
            if (prev is Para2 && next is Para2)
            {
                string num1 = prev.Number.Text.TrimStart('(').TrimEnd(')');
                string num2 = next.Number.Text.TrimStart('(').TrimEnd(')');
                return Roman.LowerRomanToInt(num1) == Roman.LowerRomanToInt(num2) - 1;
            }
            return true;
        }

    }

}
