
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

        internal static string GetLastLine(IDivision division)
        {
            return division switch
            {
            // should be using some kind of polymorphism
            Leaf leaf => GetLastLine(leaf),
            Branch branch => GetLastLine(branch),
            ILineable lineable => lineable.GetLastLineAsString(),
            WDummyDivision dummy => dummy
                .Contents
                .OfType<ILineable>()
                .LastOrDefault()
                .GetLastLineAsString(),
            _ => null,
            };
        }

        static string GetLastLine(Leaf leaf)
        {
            if (leaf.Contents?.LastOrDefault() is Mod mod) {
                return GetLastLine(mod);
            }

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

        static string GetLastLine(Branch branch)
        {
            if (branch.WrapUp?.Count > 0)
            {
                List<IInline> inlines = [];
                foreach (IBlock block in branch.WrapUp)
                {
                    if (block is ILine line)
                        inlines.AddRange(line.Contents);
                }
                return IInline.ToString(inlines, "");
            }
            return GetLastLine(branch.Children.Last());
        }

        static WLine GetLastLine(WTable table) => table
            .Rows?
            .LastOrDefault()?
            .Cells?
            .LastOrDefault()?
            .Contents?
            .OfType<WLine>()?
            .LastOrDefault()
            ;

        // All inheritors of IBlock:
        // Mod, ILine, ITableOfContents2, IQuotedStructure, ISignatureName, IDivWrapper, ITable, LdappTableBlock
        // We should probably figure out a better way than this using some kind of polymorphism... otherwise we have to update this switch for every new block
        internal static string GetLastLine(IEnumerable<IBlock> blocks) => blocks?.LastOrDefault() switch
        {
            WLine line => line.TextContent,
            Mod mod => GetLastLine(mod),
            BlockQuotedStructure qs => GetLastLine(qs),
            LdappTableBlock tableBlock => GetLastLine(tableBlock.Table.Rows.LastOrDefault().Cells.LastOrDefault().Contents),
            _ => null
        };

        static string GetLastLine(Mod mod)
        {
            return GetLastLine(mod.Contents);
        }

        static string GetLastLine(BlockQuotedStructure qs)
        {
            LegislationParser.QuoteDistance++;
            return GetLastLine(qs.Contents.Last());
        }

        /* replace */

        internal static bool Replace(IList<IDivision> divisions, Func<WLine, WLine> replace)
        {
            IDivision last = divisions.LastOrDefault();
            if (last is null)
                return false;
            return ReplaceLastLine(last, replace);
        }

        static bool ReplaceLastLine(IDivision division, Func<WLine, WLine> replace)
        {
            if (division is Leaf leaf)
                return ReplaceLastLineInLeaf(leaf, replace);
            if (division is Branch branch)
                return ReplaceLastLineInBranch(branch, replace);
            if (division is WDummyDivision dummy)
                return ReplaceLastLine(dummy, replace);
            return false;
        }

        #nullable enable
        static bool ReplaceLastLine(WDummyDivision dummy, Func<WLine, WLine> replace)
        {
            return dummy.Contents.LastOrDefault() switch
            {
                WTable table => ReplaceLastLine(table, replace),
                _ => false
            };
        }

        static bool ReplaceLastLine(WTable table, Func<WLine, WLine> replace)
        {
            ICell? lastCell = table.Rows?.LastOrDefault()?.Cells.LastOrDefault();
            if (lastCell is null) return false;
            List<IBlock>? cellContents = lastCell.Contents.ToList();
            var result = ReplaceLastOf(cellContents, replace);
            if (result) lastCell.Contents = cellContents;
            return result;
        }
        #nullable disable

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
