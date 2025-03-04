
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParsePara1(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            if (!Para1.IsValidNumber(np.Number.Text))
                return null;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            i += 1;

            if (i == Document.Body.Count)
                return new Para1Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];

            int finalChildStartLine = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(line))
                    break;

                int save = i;
                IBlock childStartLine = Current();
                IDivision next = ParseNextBodyDivision();
                if (IsExtraIntroLine(next, childStartLine, line, children.Count))
                {
                    intro.Add(childStartLine);
                    continue;
                }

                if (next is Para1) {
                    // Para1 & Para2 nums are both lowercase alphabetical 
                    // Para1 parser has higher precedence, so must force parse as Para2
                    i = save;
                    next = ParseCurrentAsPara2();
                }
                if (!Para1.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                if (!NextChildIsAcceptable(children, next)) {
                    i = save;
                    break;
                }
                children.Add(next);
                finalChildStartLine = save;
            }
            List<IBlock> wrapUp = HandleWrapUp(children, finalChildStartLine);
            if (children.Count == 0)
            {
                BlockQuotedStructure qs = ParseQuotedStructure();
                if (qs is not null)
                    intro.Add(qs);
                return new Para1Leaf { Number = num, Contents = intro };
            }
            else
            {
                return new Para1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
            }

        }

    }

}
