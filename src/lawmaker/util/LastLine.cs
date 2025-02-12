
using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    class LastLine
    {

        internal static bool Replace(IList<IDivision> divisions, Func<WLine, WLine> replace)
        {
            IDivision last = divisions.LastOrDefault();
            if (last is null)
                return false;
            return ReplaceLastLine1(last, replace);
        }

        static bool ReplaceLastLine1(IDivision division, Func<WLine, WLine> replace)
        {
            if (division is Leaf leaf)
                return ReplaceLastLineInLeaf(leaf, replace);
            if (division is Branch branch)
                return ReplaceLastLineInBranch(branch, replace);
            return false;
        }

        static bool ReplaceLastLineInLeaf(Leaf leaf, Func<WLine, WLine> replace)
        {
            return ReplaceLastOf(leaf.Contents, replace);
        }

        static bool ReplaceLastLineInBranch(Branch branch, Func<WLine, WLine> replace)
        {
            if (branch.WrapUp?.Count > 0)
                return ReplaceLastOf(branch.WrapUp, replace);
            return Replace(branch.Children, replace);
        }

        static bool ReplaceLastOf(IList<IBlock> blocks, Func<WLine, WLine> replace)
        {
            IBlock last = blocks.LastOrDefault();
            if (last is not WLine line)
                return false;
            // tables?
            WLine replacement = replace(line);
            if (replacement is null)
                return false;
            blocks.RemoveAt(blocks.Count - 1);
            blocks.Add(replacement);
            return true;
        }

    }

}
