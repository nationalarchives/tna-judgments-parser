
using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    class LastLine()
    {
        // private static readonly ILogger Logger = Logging.Factory.CreateLogger<LastLine>();

        internal static WLine GetLastLine(IDivision division)
        {
            if (division is Leaf leaf)
                return GetLastLine(leaf);
            if (division is Branch branch)
                return GetLastLine(branch);
            return null;
        }

        static WLine GetLastLine(Leaf leaf)
        {
            return GetLastLine(leaf.Contents);
        }

        static WLine GetLastLine(Branch branch)
        {
            if (branch.WrapUp?.Count > 0)
                return GetLastLine(branch.WrapUp);
            return GetLastLine(branch.Children.Last());
        }

        // All inheritors of IBlock:
        // Mod, ILine, ITableOfContents2, IQuotedStructure, ISignatureName, IDivWrapper, ITable, LdappTableBlock
        // We should probably figure out a better way than this using some kind of polymorphism... otherwise we have to update this switch for every new block
        internal static WLine GetLastLine(IEnumerable<IBlock> blocks) => blocks?.LastOrDefault() switch
        {
            WLine line => line,
            Mod mod => GetLastLineInMod(mod),
            BlockQuotedStructure qs => GetLastLineInQuotedStructure(qs),
            LdappTableBlock tableBlock => GetLastLine(tableBlock.Table.Rows.LastOrDefault().Cells.LastOrDefault().Contents),
            _ => null
        };
        // static WLine GetLastLineIn(IEnumerable<IBlock> blocks)
        // {
        //     IBlock last = blocks.LastOrDefault();
        //     if (last is not WLine line)
        //         return null;
        //     return line;
        // }

        /*
         * Obtains the entire text content of the last paragraph of the given division,
         * including any Numbers or Headings.
         */
        internal static string GetLastParagraphText(IDivision division)
        {
            if (division is Leaf leaf)
                return GetLastParagraphTextInLeaf(leaf);
            if (division is Branch branch)
                return GetLastParagraphTextInBranch(branch);
            return null;
        }

        static WLine GetLastLineInMod(Mod mod)
        {
            return GetLastLine(mod.Contents);
        }

        static WLine GetLastLineInQuotedStructure(BlockQuotedStructure qs)
        {
            LegislationParser.QuoteDistance++;
            return GetLastLine(qs.Contents.Last());
        }

        /*
         * Obtains the combined text content of a Leaf.
         * Includes the Number, Heading, and Contents.
         */
        static string GetLastParagraphTextInLeaf(Leaf leaf)
        {
            List<IInline> inlines = [];
            if (leaf.HeadingPrecedesNumber)
            {
                if (leaf.Heading != null)
                    inlines.AddRange(leaf.Heading.Contents);
                if (leaf.Number != null)
                    inlines.Add(leaf.Number);
            }
            else
            {
                if (leaf.Number != null)
                    inlines.Add(leaf.Number);
                if (leaf.Heading != null)
                    inlines.AddRange(leaf.Heading.Contents);
            }
            if (leaf.Contents != null)
            {
                foreach (IBlock block in leaf.Contents)
                {
                    if (block is ILine line)
                        inlines.AddRange(line.Contents);
                }
            }
            return IInline.ToString(inlines, "");
        }

        static string GetLastParagraphTextInBranch(Branch branch)
        {
            if (branch.WrapUp?.Count > 0)
            {
                List<IInline> inlines = [];
                foreach (IBlock block in branch.WrapUp)
                {
                    if (block is ILine line)
                        inlines.AddRange(line.Contents);
                }
                return IInline.ToString(inlines, " ");
            }
            return GetLastParagraphText(branch.Children.Last());
        }

        /* replace */

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
            if (leaf.Contents?.Count > 0)
                return ReplaceLastOf(leaf.Contents, replace);
            if (leaf.Heading is not null)
                return ReplaceHeading(leaf, replace);
            return false;
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

            // Drill down into the contents of mods and quoted structures.
            if (last is Mod mod)
                return ReplaceLastOf(mod.Contents, replace);
            if (last is BlockQuotedStructure qs)
                return Replace(qs.Contents, replace);

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

        static bool ReplaceHeading(HContainer hContainer, Func<WLine, WLine> replace)
        {
            if (hContainer.Heading is not WLine line)
                return false;
            // tables?
            WLine replacement = replace(line);
            if (replacement is null)
                return false;
            hContainer.Heading = replacement;
            return true;
        }

    }

}
