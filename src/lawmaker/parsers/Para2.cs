
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseCurrentAsPara2() {
            if (Current() is not WLine line)
                return null;
            return ParsePara2(line);
        }

        private HContainer ParsePara2(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            if (!Para2.IsValidNumber(np.Number.Text))
                return null;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            i += 1;

            if (i == Document.Body.Count)
                return new Para2Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];

            int finalChildStartLine = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(line))
                    break;

                int save = i;
                IBlock childStartLine = Current();
                IDivision next = ParseNextBodyDivision();
                if (next is UnknownLevel || IsExtraIntroLine(next, childStartLine, line, children.Count))
                {
                    intro.Add(childStartLine);
                    continue;
                }
                if (!Para2.IsValidChild(next))
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
            List<IBlock> wrapUp = HandleWrapUp(children, finalChildStartLine);
            if (children.Count == 0)
            {
                QuotedStructure qs = ParseQuotedStructure();
                if (qs is not null)
                    intro.Add(qs);
                return new Para2Leaf { Number = num, Contents = intro };
            }
            else
            {
                return new Para2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
            }

        }

    }

}
