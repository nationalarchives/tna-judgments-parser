
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
            if (line is WOldNumberedParagraph np)
                return null;
            if (!IsCenterAligned(line))
                return null;
            if (!line.IsAllItalicized())
                return null;
            if (i == Document.Body.Count - 1)
                return null;

            var save1 = i;
            i += 1;

            List<IDivision> children = [];

            while (i < Document.Body.Count)
            {

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

    }

}
