
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseScheduleChapter(WLine line)
        {
            var save = i;

            if (!PeekScheduleChapterHeading(line))
                return null;

            IFormattedText number = new WText(
                line.NormalizedContent,
                line.Contents.Where(i => i is WText).Cast<WText>().Select(t => t.properties).FirstOrDefault()
            );

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new ScheduleChapterLeaf { Number = number };

            IBlock line2 = Body[i + 1];
            // Current line may be a WLine or WTable
            if (line2 is not WLine && line2 is not WTable)
                return null;

            // Schedule chapters may have no heading
            ILine heading = null;
            if (line2 is WLine l2)
                if (l2.IsCenterAligned())
                    heading = (WLine) line2;

            i += heading is null ? 1 : 2;

            if (line2 is WLine && IsEndOfQuotedStructure(((WLine) line2).NormalizedContent))
                return new ScheduleChapterLeaf { Number = number, Heading = heading };

            WLine chapterBodyStartLine = (WLine) Body[i-1];
            List<IBlock> contents = ParseLeafContents(chapterBodyStartLine);
            HContainer chapter;
            if (contents.Count > 0)
                chapter = new ScheduleChapterLeaf { Number = number, Heading = heading, Contents = contents };
            else
            {
                List<IDivision> children = ParseScheduleChapterBranchChildren();
                // A ScheduleChapterBranch must have at least 1 child.
                if (children.Count == 0)
                {
                    i = save;
                    return null;
                }
                chapter = new ScheduleChapterBranch { Number = number, Heading = heading, Children = children };
            }

            return chapter;
        }

        private bool PeekScheduleChapterHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!line.IsCenterAligned())
                return false;
            if (i > Body.Count - 2)
                return false;
            string numText = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!LanguageService.IsMatch(numText, ScheduleChapter.NumberPatterns))
                return false;
            return true;
        }

        /// <summary>
        /// Parses and returns a list of child divisions belonging to the <c>ScheduleChapterBranch</c>.
        /// </summary>
        /// <returns>A list of child divisions belonging to the <c>ScheduleChapterBranch</c></returns>
        internal List<IDivision> ParseScheduleChapterBranchChildren()
        {
            List<IDivision> children = [];
            while (i < Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !ScheduleChapter.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!ScheduleChapter.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                children.Add(next);

                if (IsEndOfQuotedStructure(next))
                    break;
            }
            return children;
        }

    }

}
