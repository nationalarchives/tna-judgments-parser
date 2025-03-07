
using System.Collections.Generic;
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
            string numText = IgnoreStartQuote(np.Number.Text, quoteDepth);
            if (!Para1.IsValidNumber(numText))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = HandleParagraphs(np);

            if (IsEndOfQuotedStructure(intro))
                return new Para1Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            int finalChildStart = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(line))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
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
                children.Add(next);
                finalChildStart = save;

                if (IsEndOfQuotedStructure(next))
                    break;
            }
            wrapUp.AddRange(HandleWrapUp(children, finalChildStart));

            if (children.Count == 0)
                return new Para1Leaf { Number = num, Contents = intro };
            else
                return new Para1Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

    }

}
