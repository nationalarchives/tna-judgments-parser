
using System.Collections.Generic;
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
            string numText = IgnoreQuotedStructureStart(np.Number.Text, quoteDepth);
            if (!Para2.IsValidNumber(numText))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = HandleParagraphs(np);

            if (IsEndOfQuotedStructure(intro))
                return new Para2Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];
            List<IBlock> wrapUp = [];

            int finalChildStart = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(line))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Para2.IsValidChild(next))
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
                return new Para2Leaf { Number = num, Contents = intro };
            else
                return new Para2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

    }

}
