using System.Collections.Generic;
using DocumentFormat.OpenXml.Spreadsheet;
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
            List<IBlock> blocks = [];
            IBlock block;
            while (i < Document.Body.Count)
            {
                block = Document.Body[i].Block;

                if (!(block as WLine).IsAllBold())
                {
                    blocks.Add(block as WLine);
                    i += 1;
                }
                else
                {
                    // Heading tblock encountered
                    if (!IsStartOfCommencementHistoryTable(block as WLine))
                    {
                        blocks.Add(block as WLine); // TODO: Add logic to handle heading blocks
                        i += 1;
                    }
                    // End of explanatory note
                    else
                    {
                        break;
                    }
                }
            }
            blocks = (List<IBlock>) BlockList.ParseFrom(blocks);

            conclusions.Add(new ExplanatoryNote { Heading = heading, Subheading = subheading, Blocks = blocks });
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
