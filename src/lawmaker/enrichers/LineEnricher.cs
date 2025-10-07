
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{
    /*
      This is an abstract class which facilitates the enrichment of all WLine 
      descendants of a given set of Divisions or Blocks.
      To utilize, create a subclass and override the EnrichLine() method with 
      your logic for enriching each line.
    */
    public abstract class LineEnricher
    {

        internal virtual void EnrichDivisions(IList<IDivision> divisions)
        {
            foreach (var div in divisions)
                EnrichDivision(div);
        }

        internal virtual void EnrichDivision(IDivision division)
        {
            if (division is Leaf leaf)
                EnrichLeaf(leaf);
            else if (division is Branch branch)
                EnrichBranch(branch);
        }

        internal virtual void EnrichBranch(Branch branch)
        {
            if (branch.Intro != null)
                EnrichBlocks(branch.Intro);
            EnrichDivisions(branch.Children);
            if (branch.WrapUp != null)
                EnrichBlocks(branch.WrapUp);
        }

        internal virtual void EnrichLeaf(Leaf leaf)
        {
            if (leaf.Contents?.Count > 0)
                EnrichBlocks(leaf.Contents);
        }


        internal virtual void EnrichBlocks(IList<IBlock> blocks)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                IBlock enriched;
                if (blocks[i] is WLine line)
                    enriched = EnrichLine(line);
                else if (blocks[i] is Mod mod)
                    enriched = EnrichMod(mod);
                else
                    continue;

                blocks.RemoveAt(i);
                blocks.Insert(i, enriched);
            }
        }

        internal virtual Mod EnrichMod(Mod raw)
        {
            List<IBlock> enrichedBlocks = [];
            for (int i = 0; i < raw.Contents.Count; i++)
            {
                if (raw.Contents[i] is WLine line)
                {
                    IBlock enrichedLine = EnrichLine(line);
                    enrichedBlocks.Add(enrichedLine);
                }
                // Must enrich the divisions inside quoted structures
                else if (raw.Contents[i] is BlockQuotedStructure qs)
                {
                    EnrichDivisions(qs.Contents);
                    enrichedBlocks.Add(qs);
                }
                else
                {
                    enrichedBlocks.Add(raw.Contents[i]);
                }
            }
            return new Mod() { Contents = enrichedBlocks };
        }

        internal abstract IBlock EnrichLine(WLine raw);

    }

}
