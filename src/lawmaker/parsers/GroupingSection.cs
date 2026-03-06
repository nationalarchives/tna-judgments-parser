
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseGroupingSection(WLine line)
        {
            if (!PeekGroupingSectionHeading(line))
                return null;

            var save1 = i;
            i += 1;

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new GroupingSectionLeaf { Heading = line };

            List<IDivision> children = [];

            while (i < Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !GroupingSection.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!GroupingSection.IsValidChild(next)) {
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
            return new GroupingSectionBranch { Heading = line, Children = children };
        }

        private bool PeekGroupingSectionHeading(WLine line)
        {
            // Grouping sections only exist in secondary legislation
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
