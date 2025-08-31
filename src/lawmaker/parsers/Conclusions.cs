#nullable enable

using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private readonly List<BlockContainer> conclusions = [];

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
            if (!ExplanatoryNote.IsHeading(Current()))
                return;
            WLine heading = (Current() as WLine)!;
            
            int save = i;
            i += 1;

            WLine? subheading = null;
            if (ExplanatoryNote.IsSubheading(Current()))
            {
                subheading = Current() as WLine;
                i += 1;
            }
            
            List<IBlock> content = [];
            while (i < Document.Body.Count)
            {
                IBlock currentBlock = Current();

                // If we hit the the start of the Commencement History table,
                // then the Explanatory Note must have ended.
                if (CommencementHistory.IsHeading(currentBlock))
                    break;

                if (ParseHeadingTblock() is HeadingTblock headingTblock)
                    content.Add(headingTblock);
                else
                {
                    content.Add(currentBlock);
                    i += 1;
                }
            }
            if (content.Count == 0)
            {
                i = save;
                return;
            }
            IEnumerable<IBlock> structuredContent = BlockList.ParseFrom(content);
            conclusions.Add(new ExplanatoryNote { Heading = heading, Subheading = subheading, Content = structuredContent });
        }

        private HeadingTblock? ParseHeadingTblock()
        {
            if (!ExplanatoryNote.IsTblockHeading(Current()))
                return null;
            WLine heading = (Current() as WLine)!;

            int save = i;
            i += 1;

            List<IBlock> content = [];
            while (i < Document.Body.Count)
            {
                IBlock currentBlock = Current();

                // If we hit the the start of the Commencement History table,
                // then the Explanatory Note must have ended.
                if (CommencementHistory.IsHeading(currentBlock))
                    break;

                // If we hit another Tblock heading, this Tblock must end.
                if (ExplanatoryNote.IsTblockHeading(currentBlock))
                    break;

                content.Add(currentBlock);
                i += 1;
            }
            if (content.Count == 0)
            {
                i = save;
                return null;
            }
            IEnumerable<IBlock> structuredContent = BlockList.ParseFrom(content);
            return new HeadingTblock { Heading = heading, Content = structuredContent };
        }

        private void ParseCommencementHistory()
        {
            if (i >= Document.Body.Count)
                return;

            // Handle heading and subheading
            if (!CommencementHistory.IsHeading(Current()))
                return;
            WLine heading = (Current() as WLine)!;

            int save = i;
            i += 1;

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

            conclusions.Add(new CommencementHistory { Heading = heading, Subheading = subheading, Content = blocks });
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

        public static bool IsTblockHeading(IBlock block)
        {
            if (block is not WLine line)
                return false;
            return line.IsAllBold();
        }


    }

    internal class CommencementHistory : BlockContainer
    {

        public override string Name { get; internal init; } = "blockContainer";

        public override string Class { get; internal init; } = "commencementHistory";

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

    }
}
