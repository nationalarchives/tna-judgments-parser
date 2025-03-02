
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
            if (quoted && !Prov2.IsValidQuotedNumber(np.Number.Text))
                return null;
            if (!quoted && !Prov2.IsValidNumber(np.Number.Text))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            if (i == Document.Body.Count)
                return new SchProv2Leaf { Number = num, Contents = intro };

            List<IBlock> wrapUp = [];
            List<IDivision> children = ParseSchProv2Children(line, intro, wrapUp);

            if (children.Count == 0)
            {
                AddFollowingToContent(line, intro);
                return new SchProv2Leaf { Number = num, Contents = intro };
            }
            return new SchProv2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        internal List<IDivision> ParseSchProv2Children(WLine leader, List<IBlock> intro, List<IBlock> wrapUp)
        {
            List<IDivision> children = [];
            int finalChildStartLine = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(leader))
                    break;

                int save = i;
                IBlock childStartLine = Current();
                IDivision next = ParseNextBodyDivision();
                if (IsExtraIntroLine(next, childStartLine, leader, children.Count))
                {
                    intro.Add(childStartLine);
                    continue;
                }
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
                finalChildStartLine = save;
            }
            wrapUp.AddRange(HandleWrapUp(children, finalChildStartLine));
            return children;
        }

    }

}