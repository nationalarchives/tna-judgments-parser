
using System;
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private CrossHeading ParseCrossheading(WLine line)
        {
            if (!PeekCrossHeading(line))
                return null;

            var save1 = i;
            i += 1;

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
            return new CrossHeading { Heading = line, Children = children };
        }

        private bool PeekCrossHeading(WLine line)
        {
            if (line is WOldNumberedParagraph np)
                return false;
            if (!IsCenterAligned(line))
                return false;
            if (!line.IsAllItalicized())
                return false;
            if (i == Document.Body.Count - 1)
                return false;
            return true;
        }

    }

}
