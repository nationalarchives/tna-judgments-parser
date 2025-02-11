
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private int quoteDepth = 0;

        private QuotedStructure ParseQuotedStructure()
        {
            if (i == Document.Body.Count)
                return null;
            IBlock block = Document.Body[i].Block;
            if (block is not WLine line)
                return null;
            return ParseQuotedStructure(line);
        }

        private QuotedStructure ParseQuotedStructure(WLine line)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            if (!np.Number.Text.StartsWith('“'))
                return null;
            WText newNum = new WText(np.Number.Text.TrimStart('“'), null);  // not ideal
            np = new WOldNumberedParagraph(newNum, np);

            quoteDepth += 1;

            int save = i;
            var p2 = ParseProv2(np);
            if (p2 is null)
            {
                quoteDepth -= 1;
                i = save;
                return null;
            }

            List<IDivision> contents = [p2];
            while (i < Document.Body.Count)
            {
                if (Document.Body[i].Block is not WLine nextLine)
                    break;
                save = i;
                p2 = ParseProv2(nextLine);
                if (p2 is null)
                {
                    i = save;
                    break;
                }
                if (LineIsIndentedLessThan(nextLine, line))
                {
                    i = save;
                    break;
                }
                contents.Add(p2);
            }
            quoteDepth -= 1;
            return new QuotedStructure { Contents = contents, StartQuote = "“" };
        }

    }

}
