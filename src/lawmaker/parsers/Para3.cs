
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParsePara3(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            string numText = IgnoreStartQuote(np.Number.Text, quoteDepth);
            if (!Para3.IsValidNumber(numText))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            return new Para3Leaf { Number = num, Contents = intro };
        }

    }

}