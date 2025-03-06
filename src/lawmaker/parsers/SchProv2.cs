
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseSchProv2(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            string numText = IgnoreStartQuote(np.Number.Text, quoteDepth);
            if (!SchProv2.IsValidNumber(numText))
                return null;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            i += 1;
            if (i == Document.Body.Count)
                return new SchProv2Leaf { Number = num, Contents = intro };

            HandleExtraParagraphs(line, intro);
            HandleQuotedStructures(intro);

            if (IsEndOfQuotedStructure(intro))
                return new SchProv2Leaf { Number = num, Contents = intro };

            List<IBlock> wrapUp = [];
            List<IDivision> children = ParseSchProv2Children(line, intro, wrapUp);

            if (children.Count == 0)
            {
                return new SchProv2Leaf { Number = num, Contents = intro };
            }
            return new SchProv2Branch { Number = num, Intro = intro, Children = children, WrapUp = wrapUp };
        }

        internal List<IDivision> ParseSchProv2Children(WLine leader, List<IBlock> intro, List<IBlock> wrapUp)
        {
            List<IDivision> children = [];
            int finalChildStart = i;
            while (i < Document.Body.Count)
            {
                if (BreakFromProv1(leader))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (!SchProv2.IsValidChild(next))
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