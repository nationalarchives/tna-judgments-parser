
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private HContainer ParseProv2(WLine line, bool quoted = false)
        {
            if (line is not WOldNumberedParagraph np)
                return null;
            if (quoted && !Prov2.IsQuotedProv2Number(np.Number.Text))
                return null;
            if (!quoted && !Prov2.IsProv2Number(np.Number.Text))
                return null;

            i += 1;

            IFormattedText num = np.Number;
            List<IBlock> intro = [WLine.RemoveNumber(np)];

            if (i == Document.Body.Count)
                return new Prov2Leaf { Number = num, Contents = intro };

            List<IDivision> children = [];

            while (i < Document.Body.Count)
            {
                if (CurrentLineIsIndentedLessThan(line))
                    break;

                int save = i;
                IDivision next = ParseNextBodyDivision();
                if (next is not Para1 && next is not UnnumberedParagraph)
                {
                    i = save;
                    break;
                }
                if (next is Para1 && !NextChildNumberIsAcceptable(children, next)) {
                    i = save;
                    break;
                }
                children.Add(next);
            }
            if (children.Count == 0)
            {
                QuotedStructure qs = ParseQuotedStructure();
                if (qs is not null)
                    intro.Add(qs);
                return new Prov2Leaf { Number = num, Contents = intro };
            }
            else
            {
                return new Prov2Branch { Number = num, Intro = intro, Children = children };
            }
        }

    }

}
