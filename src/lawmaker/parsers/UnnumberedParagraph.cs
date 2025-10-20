
using System;
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private HContainer ParseUnnumberedParagraph(WLine line)
        {
            if (line is WOldNumberedParagraph)
                return null;
            if (!line.IsLeftAligned())
                return null;

            i += 1;

            List<IBlock> intro = HandleParagraphs(line);

            if (IsEndOfQuotedStructure(intro))
                return new UnnumberedLeaf { Contents = intro };

            List<IDivision> children = [];

            while (i < Body.Count)
            {
                if (CurrentLineIsIndentedLessThan(line))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not Para1)
                {
                    i = save;
                    break;
                }
                children.Add(next);
            }
            if (children.Count == 0)
            {
                return new UnnumberedLeaf { Contents = intro };
            }
            else
            {
                return new UnnumberedBranch { Intro = intro, Children = children };
            }
        }

    }

}
