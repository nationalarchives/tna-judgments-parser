
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseUnnumberedParagraph(WLine line, string startQuote)
        {
            if (line is WOldNumberedParagraph)
                return null;
            if (!IsLeftAligned(line))
                return null;

            i += 1;

            List<IBlock> intro = [line];

            if (i == Document.Body.Count)
            {
                return new UnnumberedLeaf { Contents = intro };
            }

            List<IDivision> children = [];

            while (i < Document.Body.Count)
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
