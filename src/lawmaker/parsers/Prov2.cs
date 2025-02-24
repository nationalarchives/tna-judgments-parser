
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseProv2(WLine line)
        {
            bool quoted = quoteDepth > 0;
            if (line is not WOldNumberedParagraph np)
                return null;
            if (quoted && !Prov2.IsValidQuotedNumber(np.Number.Text))
                return null;
            if (!quoted && !Prov2.IsValidNumber(np.Number.Text))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            if (i == Document.Body.Count)
                return new Prov2Leaf { Number = num, Contents = intro };

            List<IDivision> children = ParseProv2Children(line);

            if (children.Count == 0)
            {
                AddFollowingToContent(line, intro);
                return new Prov2Leaf { Number = num, Contents = intro };
            }
            else
            {
                List<IBlock> wrapUp = [];
                AddFollowingToIntroOrWrapUp(line, wrapUp);
                return new Prov2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
            }
        }

        internal List<IDivision> ParseProv2Children(WLine leader)
        {
            List<IDivision> children = [];
            while (i < Document.Body.Count)
            {
                if (!CurrentIsPossibleProv2Child(leader))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Prov2.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                if (!NextChildIsAcceptable(children, next))
                {
                    i = save;
                    break;
                }
                children.Add(next);
            }
            return children;
        }

        private bool CurrentIsPossibleProv2Child(WLine leader)
        {
            if (Current() is not WLine line)
                return true;
            if (!IsLeftAligned(line))
                return false;
            if (LineIsIndentedLessThan(line, leader))
                return false;
            if (line is WOldNumberedParagraph && !LineIsIndentedMoreThan(line, leader))
                return false;
            return true;
        }

    }

}
