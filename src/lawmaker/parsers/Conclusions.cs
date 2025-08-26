
using System.Collections.Generic;
using System.CommandLine.IO;
using System.Linq;
using DocumentFormat.OpenXml.Office.ContentType;
using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private readonly List<IDivision> conclusions = [];

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
            if ((Document.Body[i].Block as WLine).NormalizedContent.ToUpper().Equals("EXPLANATORY NOTE"))
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
            List<IBlock> contents = [];
            IBlock block;
            while (i < Document.Body.Count)
            {
                block = Document.Body[i].Block;

                if (!(block as WLine).NormalizedContent.StartsWith("NOTE AS TO EARLIER COMMENCEMENT", System.StringComparison.CurrentCultureIgnoreCase))
                {
                    contents.Add(block as WLine);
                    i += 1;
                }
                else
                    break;
            }

            conclusions.Add(new ExplanatoryNoteLeaf { Heading = heading, Subheading = subheading, Contents = contents });
        }

        private void ParseCommencementHistory()
        {
            WLine heading = null;
            WLine subheading = null;
            if ((Document.Body[i].Block as WLine).NormalizedContent.StartsWith("NOTE AS TO EARLIER COMMENCEMENT", System.StringComparison.CurrentCultureIgnoreCase))
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
            List<IBlock> contents = [];
            IBlock block;
            while (i < Document.Body.Count)
            {
                block = Document.Body[i].Block;

                if (Match(LdappTableBlock.Parse) is LdappTableBlock tableBlock)
                    contents.Add(tableBlock);
                else
                    contents.Add(block);
                i += 1;
            }

            conclusions.Add(new CommencementHistoryLeaf { Heading = heading, Subheading = subheading, Contents = contents });
        }
    }

    internal interface ExplanatoryNote
    {
        public ILine Subheading { get; }

        // public static bool IsValidChild(IDivision child)
        // {
        //     if (child is Prov1)
        //         return true;
        //     return false;
        // }
    }

    internal class ExplanatoryNoteLeaf : Leaf, ExplanatoryNote
    {
        public ILine Subheading { get; internal set; }

        public override string Name { get; internal init; } = "blockContainer";

        public override string Class => "explanatoryNote";
    }
    
    internal interface CommencementHistory
    {
        public ILine Subheading { get; }
    }

    internal class CommencementHistoryLeaf : Leaf, CommencementHistory
    {
        public ILine Subheading { get; internal set; }

        public override string Name { get; internal init; } = "blockContainer";

        public override string Class => "commencementHistory";
    }
}
