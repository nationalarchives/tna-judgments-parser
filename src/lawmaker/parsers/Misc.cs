
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private IBlock Current() => Document.Body[i].Block;

        private static bool IsLeftAligned(WLine line) {
            var alignment = line.GetEffectiveAlignment();
            return !alignment.HasValue || alignment == AlignmentValues.Left || alignment == AlignmentValues.Justify;
        }

        private static bool IsCenterAligned(WLine line) {
            var alignment = line.GetEffectiveAlignment();
            return alignment == AlignmentValues.Center;
        }

        private static bool IsFlushLeft(WLine line) => OptimizedParser.IsFlushLeft(line);

        private static float GetEffectiveIndent(WLine line) => OptimizedParser.GetEffectiveIndent(line);

        private bool CurrentLineIsIndentedLessThan(WLine parent) {
            return LineAtIsIndentedLessThan(i, parent);
        }

        private bool LineAtIsIndentedLessThan(int index, WLine parent) {
            if (Document.Body[index].Block is not WLine line)
                return false;
            return GetEffectiveIndent(line) < GetEffectiveIndent(parent);

        }

        private static bool NextChildNumberIsAcceptable(List<IDivision> children, IDivision next) {
            if (children.Count == 0)
                return true;
            IDivision prev = children.Last();
            if (prev is Para1 && next is Para1) {
                string num1 = prev.Number.Text.TrimStart('(').TrimEnd(')');
                string num2 = next.Number.Text.TrimStart('(').TrimEnd(')');
                if (num1.Length == 1 && num2.Length == 1)
                    return num1[0] == (char)(num2[0] - 1);
            }
            if (prev is Para2 && next is Para2) {
                string num1 = prev.Number.Text.TrimStart('(').TrimEnd(')');
                string num2 = next.Number.Text.TrimStart('(').TrimEnd(')');
                return Roman.LowerRomanToInt(num1) == Roman.LowerRomanToInt(num2) - 1;
            }
            return true;
        }

    }

}
