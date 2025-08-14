
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Bibliography;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private IBlock Current() => Document.Body[i].Block;
        private IBlock Previous() => i > 0 ? Document.Body[i-1].Block : null;

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
