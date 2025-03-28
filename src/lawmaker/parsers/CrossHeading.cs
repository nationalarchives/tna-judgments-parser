
using System;
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseCrossheading(WLine line)
        {
            if (!PeekCrossHeading(line))
                return null;

            var save1 = i;
            i += 1;

            if (IsEndOfQuotedStructure(line.NormalizedContent))
                return new CrossHeadingLeaf { Heading = line };

            List<IDivision> children = [];

            while (i < Document.Body.Count)
            {
                HContainer peek = PeekGroupingProvision();
                if (peek != null && !CrossHeading.IsValidChild(peek))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!CrossHeading.IsValidChild(next)) {
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
            return new CrossHeadingBranch { Heading = line, Children = children };
        }

        private bool PeekCrossHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!IsCenterAligned(line))
                return false;
            if (!line.IsPartiallyItalicized())
                return false;
            if (i == Document.Body.Count - 1)
                return false;
            return true;
        }

    }

}
