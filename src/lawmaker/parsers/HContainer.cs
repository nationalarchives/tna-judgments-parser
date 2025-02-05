
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        // always leaves i in the right place; never returns null
        private HContainer ParseLine(WLine line)
        {
            HContainer hContainer;

            var save = i;
            hContainer = ParseProv1(line);
            if (hContainer != null)
                return hContainer;
            i = save;

            hContainer = ParseProv2(line);
            if (hContainer != null)
                return hContainer;
            i = save;

            hContainer = ParsePara1(line);
            if (hContainer != null)
                return hContainer;
            i = save;

            // hContainer = ParsePara2(line);
            // if (hContainer != null)
            //     return hContainer;
            // i = save;

            hContainer = ParseUnnumberedParagraph(line);
            if (hContainer != null)
                return hContainer;
            i = save;

            i += 1;
            if (line is WOldNumberedParagraph np)
            {
                if (Para2.IsPara2Number(np.Number.Text))
                    return new Para2Leaf() { Number = np.Number, Contents = [ WLine.RemoveNumber(np) ] };
                else
                    return new UnknownLevel() { Number = np.Number, Contents = [ WLine.RemoveNumber(np) ] };
            }
            else
            {
                return new UnknownLevel() { Contents = [ line ] };
            }
        }

    }

}
