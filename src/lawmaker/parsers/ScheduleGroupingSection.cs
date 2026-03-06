
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseScheduleGroupingSection(WLine line)
        {
            if (!PeekScheduleGroupingSectionHeading(line))
                return null;

            var save1 = i;
            i += 1;

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new ScheduleGroupingSectionLeaf { Heading = line };

            List<IDivision> children = [];

            while (i < Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !ScheduleGroupingSection.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!ScheduleGroupingSection.IsValidChild(next)) {
                    i = save;
                    break;
                }
                children.Add(next);

                if (IsEndOfQuotedStructure(next))
                    break;
            }
            if (children.Count == 0)
            {
                i = save1;
                return null;
            }
            return new ScheduleGroupingSectionBranch { Heading = line, Children = children };
        }

        private bool PeekScheduleGroupingSectionHeading(WLine line)
        {
            // Schedule grouping sections only exist in secondary legislation
            if (!frames.IsSecondaryDocName())
                return false;
            if (line is WOldNumberedParagraph np)
                return false;
            if (!line.IsCenterAligned())
                return false;
            if (!line.IsPartiallyItalicized())
                return false;
            if (i == Body.Count - 1)
                return false;
            return true;
        }

    }

}
