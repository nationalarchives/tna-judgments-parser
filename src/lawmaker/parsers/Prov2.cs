
using System.Collections.Generic;
using System.Linq;
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

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            i += 1;

            if (i == Document.Body.Count)
                return new Prov2Leaf { Number = num, Contents = intro };

            List<IBlock> wrapUp = [];
            List<IDivision> children = ParseProv2Children(line, intro, wrapUp);

            if (children.Count == 0)
            {
                AddFollowingToContent(line, intro);
                return new Prov2Leaf { Number = num, Contents = intro };
            }
            return new Prov2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        internal List<IDivision> ParseProv2Children(WLine leader, List<IBlock> intro, List<IBlock> wrapUp)
        {
            List<IDivision> children = [];
            int finalChildStartLine = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1())
                    break;

                int save = i;
                IBlock childStartLine = Current();
                IDivision next = ParseNextBodyDivision();
                // It's safer to assume that an UnknownLevel is a child of the previous division rather than a new top level element
                if (next is UnknownLevel || IsExtraIntroLine(next, childStartLine, leader, children.Count))
                {
                    intro.Add(childStartLine);
                    continue;
                }
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
                finalChildStartLine = save;
            }
            wrapUp.AddRange(HandleWrapUp(children, finalChildStartLine));
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
