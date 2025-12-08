
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseSchedulePart(WLine line)
        {
            var save = i;
            
            if (!PeekSchedulePartHeading(line))
                return null;

            IFormattedText number = new WText(
                line.NormalizedContent,
                line.Contents.Where(i => i is WText).Cast<WText>().Select(t => t.properties).FirstOrDefault()
            );

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new SchedulePartLeaf { Number = number };

            IBlock line2 = Body[i + 1];
            // Current line may be a WLine or WTable
            if (line2 is not WLine && line2 is not WTable)
                return null;
            
            // Schedule parts may have no heading
            ILine heading = null;
            if (line2 is WLine)
                if (!(!IsCenterAligned((WLine) line2) || LanguageService.IsMatch(((WLine) line2).TextContent, ScheduleChapter.NumberPatterns)))
                    heading = (WLine) line2;
            i += heading is null ? 1 : 2;
            
            if (line2 is WLine && IsEndOfQuotedStructure(((WLine) line2).NormalizedContent))
                return new SchedulePartLeaf { Number = number, Heading = heading };
                
            WLine partBodyStartLine = (WLine) Body[i-1];
            List<IBlock> contents = ParseSchedulePartLeafContent(partBodyStartLine);
            HContainer part;
            if (contents.Count > 0)
                part = new SchedulePartLeaf { Number = number, Heading = heading, Contents = contents };
            else
            {
                List<IDivision> children = ParseSchedulePartBranchChildren();
                // A SchedulePartBranch must have at least 1 child.
                if (children.Count == 0)
                {
                    i = save;
                    return null;
                }
                part = new SchedulePartBranch { Number = number, Heading = heading, Children = children };
            }
            
            return part;
        }

        private bool PeekSchedulePartHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!IsCenterAligned(line))
                return false;
            if (i > Body.Count - 2)
                return false;
            string numText = IgnoreQuotedStructureStart(line.NormalizedContent, quoteDepth);
            if (!LanguageService.IsMatch(numText, SchedulePart.NumberPatterns))
                return false;
            return true;
        }
        
        /// <summary>
        /// Returns the <c>SchedulePartLeaf</c> content following the <paramref name="heading"/>, if present. 
        /// Otherwise, returns an empty list.
        /// </summary>
        /// <param name="heading">The line representing the schedule heading.</param>
        /// <returns>A list of <c>SchedulePartLeaf</c> content.</returns>
        internal List<IBlock> ParseSchedulePartLeafContent(WLine heading)
        {
            int save = i;
            List<IBlock> contents = [];

            // Handle when schedule content begins immediately with one or more quoted structures.
            HandleMod(heading, contents, true);
            if (contents.Count > 0)
                return contents;

            // Handle all other schedule content.
            // If the next line(s) do not constitute a division, handle them as paragraphs (or tables).
            IDivision next = ParseNextBodyDivision();
            i = save;
            if (next is UnnumberedLeaf || next is UnknownLevel || next is WDummyDivision)
                contents = HandleParagraphs(heading).Skip(1).ToList();
            return contents;
        }

        /// <summary>
        /// Parses and returns a list of child divisions belonging to the <c>SchedulePartBranch</c>.
        /// </summary>
        /// <returns>A list of child divisions belonging to the <c>SchedulePartBranch</c></returns>
        internal List<IDivision> ParseSchedulePartBranchChildren()
        {
            List<IDivision> children = [];
            while (i < Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !SchedulePart.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!SchedulePart.IsValidChild(next))
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