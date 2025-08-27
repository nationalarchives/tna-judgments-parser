using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private readonly List<IBlockContainer> conclusions = [];

        private void ParseConclusions()
        {
            ParseExplanatoryNote();
            if (i < Document.Body.Count)
                ParseCommencementHistory();
        }

        private void ParseExplanatoryNote()
        {
            WLine heading = null;
            WLine subheading = null;
            if (IsStartOfExplanatoryNote(Document.Body[i].Block as WLine))
            {
                heading = Document.Body[i].Block as WLine;
                i += 1;
            }
            WLine line2 = Document.Body[i].Block as WLine;
            if (line2.NormalizedContent.StartsWith('(') && line2.NormalizedContent.EndsWith(')'))
            {
                subheading = line2;
                i += 1;
            }
            
            List<IBlock> explanatoryNotesBlocks = []; // The explanatory note blockContainer's blocks (children)
            List<IBlock> blocksSegment = []; // Used to keep track of current blockLists segment
            HeadingTblock currentHeadingTblock = null;
            IBlock block;
            while (i < Document.Body.Count)
            {
                block = Document.Body[i].Block;

                if (!(block as WLine).IsAllBold())
                {
                    blocksSegment.Add(block as WLine);
                    i += 1;
                }
                else
                {
                    // Heading tblock encountered
                    if (!IsStartOfCommencementHistoryTable(block as WLine))
                    {
                        if (blocksSegment.Count > 0)
                        {
                            // There is a heading so add blocksSegment as a child to the heading and add heading as a child to explanatoryNote
                            if (currentHeadingTblock is not null)
                            {
                                currentHeadingTblock.Blocks = (List<IBlock>)BlockList.ParseBlocks(blocksSegment);
                                explanatoryNotesBlocks.Add(currentHeadingTblock);
                            }
                            // No heading so add blocksSegment as a direct child to the explanatoryNote
                            else
                                explanatoryNotesBlocks.AddRange((List<IBlock>)BlockList.ParseBlocks(blocksSegment));
                            blocksSegment.Clear();
                        }
                        
                        currentHeadingTblock = new HeadingTblock { Heading = block as WLine };
                        i += 1;
                    }
                    // End of explanatory note
                    else
                        break;
                }
            }
            if (currentHeadingTblock is not null)
            {
                if (blocksSegment.Count > 0)
                    currentHeadingTblock.Blocks = (List<IBlock>)BlockList.ParseBlocks(blocksSegment);
                explanatoryNotesBlocks.Add(currentHeadingTblock);
            }
            else
                if (blocksSegment.Count > 0)
                    explanatoryNotesBlocks.AddRange((List<IBlock>)BlockList.ParseBlocks(blocksSegment));

            conclusions.Add(new ExplanatoryNote { Heading = heading, Subheading = subheading, Blocks = explanatoryNotesBlocks });
        }

        private void ParseCommencementHistory()
        {
            WLine heading = null;
            WLine subheading = null;
            if (IsStartOfCommencementHistoryTable(Document.Body[i].Block as WLine))
            {
                heading = Document.Body[i].Block as WLine;
                i += 1;
            }
            WLine line2 = Document.Body[i].Block as WLine;
            if (line2.NormalizedContent.StartsWith('(') && line2.NormalizedContent.EndsWith(')'))
            {
                subheading = line2;
                i += 1;
            }
            List<IBlock> blocks = [];
            IBlock block;
            while (i < Document.Body.Count)
            {
                block = Document.Body[i].Block;

                if (Match(LdappTableBlock.Parse) is LdappTableBlock tableBlock)
                    blocks.Add(tableBlock);
                else
                    blocks.Add(block);
                i += 1;
            }

            conclusions.Add(new CommencementHistory { Heading = heading, Subheading = subheading, Blocks = blocks });
        }

        private static bool IsStartOfExplanatoryNote(WLine line)
        {
            return line.NormalizedContent.ToUpper().Equals("EXPLANATORY NOTE");
        }

        private static bool IsStartOfCommencementHistoryTable(WLine line)
        {
            return line.NormalizedContent.StartsWith("NOTE AS TO EARLIER COMMENCEMENT", System.StringComparison.CurrentCultureIgnoreCase);
        }
    }

    internal class ExplanatoryNote : BlockContainer
    {

        public override string Name { get; internal init; } = "blockContainer";

        public override string Class { get; internal init; } = "explanatoryNote";

    }

    internal class CommencementHistory : BlockContainer
    {

        public override string Name { get; internal init; } = "blockContainer";

        public override string Class { get; internal init; } = "commencementHistory";
        
    }
}
