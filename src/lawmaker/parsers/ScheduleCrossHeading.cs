
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseScheduleCrossheading(WLine line)
        {
            if (!PeekScheduleCrossHeading(line))
                return null;

            var save1 = i;
            i += 1;

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new CrossHeadingLeaf { Heading = line };

            List<IDivision> children = [];
            while (i < Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !ScheduleCrossHeading.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!ScheduleCrossHeading.IsValidChild(next))
                {
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
            return new ScheduleCrossHeadingBranch { Heading = line, Children = children };
        }

        private bool PeekScheduleCrossHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (i == Body.Count - 1)
                return false;
            if (frames.IsSecondaryDocName())
            {
                if (!line.IsFlushLeft() && quoteDepth == 0)
                    return false;
                return line.IsPartiallyBold();
            }
            else
            {
                if (!line.IsCenterAligned())
                    return false;
                return line.IsPartiallyItalicized();
            }
        }

    }

}
