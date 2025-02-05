
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
            if (!Para2.IsPara2Number(np.Number.Text))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [ WLine.RemoveNumber(np) ];

            return new Para2Leaf { Number = num, Contents = intro };
        }

    }

}
