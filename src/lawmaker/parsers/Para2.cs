
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

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [ WLine.RemoveNumber(np) ];

            if (i == Document.Body.Count)
                return new Para2Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];

            while (i < Document.Body.Count)
            {
                if (IsProv1End(line))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
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
            }
            List<IBlock> wrapUp = HandleClosingWords(children);
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

        private bool CurrentIsPossiblePara2Child(WLine leader) => CurrentIsPossibleProv1Child(leader);

    }

}
