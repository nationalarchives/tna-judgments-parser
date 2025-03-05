
using System.Collections.Generic;
using System.Linq;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParsePara1(WLine line, string startQuote)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            string numText = (startQuote == null) ? np.Number.Text : np.Number.Text[1..];
            if (!Para1.IsValidNumber(numText))
                return null;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            i += 1;

            if (i == Document.Body.Count)
                return new Para1Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            while (i < Document.Body.Count)
            {
                int save = i;
                BlockQuotedStructure qs = ParseQuotedStructure(children.Count);
                if (qs != null)
                {
                    intro.Add(qs);
                    continue;
                }
                i = save;

                if (BreakFromProv1(line))
                    break;

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
                    next = ParseCurrentAsPara2(startQuote);
                }
                if (!Para1.IsValidChild(next))
                {
                    i = save;
                    break;
                }
                if (!HasValidIndentForChild(childStartLine, line))
                {
                    List<IBlock> addToWrapUp = HandleWrapUp2(next, children.Count);
                    if (addToWrapUp.Count > 0)
                        wrapUp.AddRange(addToWrapUp);
                    else
                        i = save;
                    break;
                }
                children.Add(next);
            }

            if (children.Count == 0)
                return new Para1Leaf { Number = num, Contents = intro };
            else
                return new Para1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

    }

}
