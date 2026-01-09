#nullable enable

using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

using static UK.Gov.Legislation.Lawmaker.LanguageService;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private readonly List<BlockContainer> conclusions = [];

        private static readonly HashSet<string> rubricStyles = ["Draft", "Correction", "Approval", "LaidDraft"];

        private void ParseConclusions()
        {
            ExplanatoryNote? explanatoryNote = Match(ExplanatoryNote.Parse);
            if (explanatoryNote is not null)
                conclusions.Add(explanatoryNote);

            CommencementHistory? commencementHistory = ParseCommencementHistory();
            if (commencementHistory is not null)
                conclusions.Add(commencementHistory);
        }

        private HeadingTblock? ParseHeadingTblock()
        {
            if (!ExplanatoryNote.IsTblockHeading(Current()))
                return null;
            WLine heading = (Current() as WLine)!;

            int save = i;
            i += 1;

            List<IBlock> content = [];
            while (i < Body.Count)
            {
                IBlock? currentBlock = Current();
                if (currentBlock is null)
                    break;

                // If we hit the the start of the Commencement History table,
                // then the Explanatory Note must have ended.
                if (CommencementHistory.IsHeading(LanguageService, currentBlock))
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
            BlockParser parser = new(content) { LanguageService = LanguageService };
            IEnumerable<IBlock> structuredContent = BlockList.ParseFrom(parser);
            return new HeadingTblock { Heading = heading, Content = structuredContent };
        }

        private CommencementHistory? ParseCommencementHistory()
        {
            if (i >= Body.Count)
                return null;

            // Handle heading and subheading
            if (!CommencementHistory.IsHeading(LanguageService, Current()))
                return null;
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
            while (i < Body.Count)
            {
                block = Body[i];

                if (block is WLine line)
                {
                    // A centre-aligned line typically indicates that the preface has been reached.
                    if (line.IsCenterAligned())
                        break;
                    // If we hit a rubric, the Commencement History must have ended. Relevant for WSI,
                    // where the Commencement History is in the Header rather than in the Conclusions.
                    if (line.Style is not null && rubricStyles.Contains(line.Style))
                        break;
                }

                if (Match(LdappTableBlock.Parse) is LdappTableBlock tableBlock)
                    blocks.Add(tableBlock);
                else
                {
                    blocks.Add(block);
                    i += 1;
                }
            }

            return new CommencementHistory { Heading = heading, Subheading = subheading, Content = blocks };
        }

    }

    internal class CommencementHistory : BlockContainer
    {

        public override string Name { get; internal init; } = "blockContainer";

        public override string Class { get; internal init; } = "commencementHistory";

        private static readonly LanguagePatterns HeadingPatterns = new()
        {
            [Lang.EN] = [@"^NOTE +AS +TO +EARLIER +COMMENCEMENT.*$"],
            [Lang.CY] = [@"^NODYN +AM +Y +(RHEOLIADAU|GORCHMYNION|GORCHYMYN) +CYCHWYN +(CYNHARACH|BLAENOROL)$"]
        };

        public static bool IsHeading(LanguageService langService, IBlock? block)
        {
            if (block is not WLine line)
                return false;
            return langService.IsMatch(line.NormalizedContent, HeadingPatterns);
        }

        public static bool IsSubheading(IBlock? block)
        {
            if (block is not WLine line)
                return false;
            string text = line.NormalizedContent;
            return text.StartsWith('(') && text.EndsWith(')');
        }

    }
}
