
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseCurrentAsPara2(string startQuote) {
            if (Current() is not WLine line)
                return null;
            return ParsePara2(line, startQuote);
        }

        private HContainer ParsePara2(WLine line, string startQuote)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            string numText = (startQuote == null) ? np.Number.Text : np.Number.Text[1..];
            if (!Para2.IsValidNumber(numText))
                return null;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            i += 1;
            if (i == Document.Body.Count)
                return new Para2Leaf { Number = num, Contents = intro };

            HandleExtraParagraphs(line, intro);
            HandleQuotedStructures(intro);

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
