
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseSchProv2(WLine line)
        {
            bool quoted = quoteDepth > 0;
            if (line is not WOldNumberedParagraph np)
                return null;
            if (quoted && !Prov2.IsQuotedProv2Number(np.Number.Text))
                return null;
            if (!quoted && !Prov2.IsProv2Number(np.Number.Text))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            if (i == Document.Body.Count)
                return new SchProv2Leaf { Number = num, Contents = intro };

            List<IDivision> children = ParseSchProv2Children(line);

            if (children.Count == 0)
            {
                AddFollowingToContent(line, intro);
                return new SchProv2Leaf { Number = num, Contents = intro };
            }
            else
            {
                List<IBlock> wrapUp = [];
                AddFollowingToIntroOrWrapUp(line, wrapUp);
                return new SchProv2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
            }
        }

        internal List<IDivision> ParseSchProv2Children(WLine leader, bool ignoreIndentation = false)
        {
            List<IDivision> children = [];
            while (i < Document.Body.Count)
            {
                if (!CurrentIsPossibleSchProv2Child(leader, ignoreIndentation))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!SchProv2.IsValidChild(next))
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

        private bool CurrentIsPossibleSchProv2Child(WLine leader, bool ignoreIndentation = false)
        {
            if (Current() is not WLine line)
                return true;
            if (!IsLeftAligned(line))
                return false;

            if (!ignoreIndentation)
            {
                if (LineIsIndentedLessThan(line, leader))
                    return false;
                if (line is WOldNumberedParagraph && !LineIsIndentedMoreThan(line, leader))
                    return false;
            }
            return true;
        }

    }

}