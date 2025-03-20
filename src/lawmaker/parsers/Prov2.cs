
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseProv2(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            string numText = IgnoreQuotedStructureStart(np.Number.Text, quoteDepth);
            if (!Prov2.IsValidNumber(numText))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = HandleParagraphs(np);

            if (IsEndOfQuotedStructure(intro))
                return new Prov2Leaf { Number = num, Contents = intro };

            List<IBlock> wrapUp = [];
            List<IDivision> children = ParseProv2Children(line, intro, wrapUp);

            if (children.Count == 0)
            {
                return new Prov2Leaf { Number = num, Contents = intro };
            }
            return new Prov2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        internal List<IDivision> ParseProv2Children(WLine leader, List<IBlock> intro, List<IBlock> wrapUp)
        {
            List<IDivision> children = [];
            int finalChildStart = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(leader))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!Prov2.IsValidChild(next))
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
            return children;
        }

    }

}
