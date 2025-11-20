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

    partial class LegislationParser : BlockParser
    {

        internal static bool IsCenterAligned(WLine line)
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
            if (!line.Contents.OfType<WTab>().Any())
                return line.NormalizedContent;

            IEnumerable<IInline> inlines = line.Contents.Reverse().TakeWhile(i => i is not WTab).Reverse();
            return new WLine(line, inlines).NormalizedContent;
        }

        private static string IgnoreRightTabbedText(WLine line)
        {
            if (!line.Contents.OfType<WTab>().Any())
                return line.NormalizedContent;

            IEnumerable<IInline> inlines = line.Contents.Reverse().SkipWhile(i => i is not WTab).Reverse();
            return new WLine(line, inlines).NormalizedContent;
        }

        private static bool ContentHasTabbedText(WLine line)
        {
            if (!line.Contents.OfType<WTab>().Any())
                return false;
            if (!line.Contents.Reverse().TakeWhile(i => i is not WTab).Any())
                return false;
            return true;
        }

        private static float GetEffectiveIndent(WLine line) => OptimizedParser.GetEffectiveIndent(line);

        private bool CurrentLineIsIndentedLessThan(WLine parent)
        {
            if (Body[i] is not WLine line)
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
            if (!line.IsLeftAligned())
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

    }

}
