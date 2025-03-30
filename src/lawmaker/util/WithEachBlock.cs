
using System;
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker.Util
{

    class WithEachBlock
    {

        internal static void Do(IList<IDivision> divisions, Action<IBlock> consumer)
        {
            foreach (var div in divisions)
                Do1(div, consumer);
        }

        static void Do1(IDivision division, Action<IBlock> consumer)
        {
            if (division is Leaf leaf)
                DoWithLeaf(leaf, consumer);
            if (division is Branch branch)
                DoWithBranch(branch, consumer);
        }

        static void DoWithLeaf(Leaf leaf, Action<IBlock> consumer)
        {
            DoWithBlocks(leaf.Contents, consumer);
        }

        static void DoWithBranch(Branch branch, Action<IBlock> consumer)
        {
            DoWithBlocks(branch.Intro, consumer);
            Do(branch.Children, consumer);
            DoWithBlocks(branch.WrapUp, consumer);
        }

        static void DoWithBlocks(IList<IBlock> blocks, Action<IBlock> consumer)
        {
            if (blocks is null)
                return;
            foreach (var block in blocks)
            {
                consumer(block);
                if (block is Mod mod)
                    DoWithBlocks(mod.Contents, consumer);
                else if (block is BlockQuotedStructure qs)
                    Do(qs.Contents, consumer);
            }
        }

    }

}
