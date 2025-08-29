#nullable enable

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
            if (i >= Document.Body.Count)
                return;
            ParseExplanatoryNote();
            ParseCommencementHistory();
        }

        private void ParseExplanatoryNote()
        {
            if (i >= Document.Body.Count)
                return;

            // Handle heading and subheading
            WLine? heading = null;
            if (ExplanatoryNote.IsHeading(Current()))
            {
                heading = Current() as WLine;
                i++;
            }
            WLine? subheading = null;
            if (ExplanatoryNote.IsSubheading(Current()))
            {
                subheading = Current() as WLine;
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
                    if (!CommencementHistory.IsHeading(block))
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
            if (i >= Document.Body.Count)
                return;

            // Handle heading and subheading
            WLine? heading = null;
            if (CommencementHistory.IsHeading(Current()))
            {
                heading = Current() as WLine;
                i += 1;
            }
            WLine? subheading = null;
            if (CommencementHistory.IsSubheading(Current()))
            {
                subheading = Current() as WLine;
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

    }

    internal class ExplanatoryNote : BlockContainer
    {

        public override string Name { get; internal init; } = "blockContainer";

        public override string Class { get; internal init; } = "explanatoryNote";

        public static bool IsHeading(IBlock block)
        {
            if (block is not WLine line)
                return false;
            return line.NormalizedContent.ToUpper().Equals("EXPLANATORY NOTE");
        }

        public static bool IsSubheading(IBlock block)
        {
            if (block is not WLine line)
                return false;
            string text = line.NormalizedContent;
            return text.StartsWith('(') && text.EndsWith(')');
        }

    }

    internal class CommencementHistory : BlockContainer
    {

        public override string Name { get; internal init; } = "blockContainer";

        public override string Class { get; internal init; } = "commencementHistory";

<<<<<<< HEAD
||||||| parent of 6c1424c0 (LNI-315: Minor tidy)

=======
        public static bool IsHeading(IBlock block)
        {
            if (block is not WLine line)
                return false;
            return line.NormalizedContent.ToUpper().StartsWith("NOTE AS TO EARLIER COMMENCEMENT");
        }

        public static bool IsSubheading(IBlock block)
        {
            if (block is not WLine line)
                return false;
            string text = line.NormalizedContent;
            return text.StartsWith('(') && text.EndsWith(')');
        }

>>>>>>> 6c1424c0 (LNI-315: Minor tidy)
    }
}
