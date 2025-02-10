
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
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
            hContainer = ParsePart(line);
            if (hContainer != null)
                return hContainer;
            i = save;

            save = i;
            hContainer = ParseCrossheading(line);
            if (hContainer != null)
                return hContainer;
            i = save;

            save = i;
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
                    return new Para2Leaf() { Number = np.Number, Contents = [WLine.RemoveNumber(np)] };
                else
                    return new UnknownLevel() { Number = np.Number, Contents = [WLine.RemoveNumber(np)] };
            }
            else
            {
                return new UnknownLevel() { Contents = [line] };
            }
        }

        /* helper functions for content, intro and wrapUp */

        private void AddFollowingToContent(WLine leader, List<IBlock> container)
        {
            while (i < Document.Body.Count)
            {
                IBlock current = Current();
                if (Current() is not WLine line)
                {
                    i += 1;
                    container.Add(current);
                    continue;
                }
                QuotedStructure qs = ParseQuotedStructure(line);
                if (qs is not null)
                {
                    container.Add(qs);
                    continue;
                }
                if (line is WOldNumberedParagraph)
                    break;
                if (!IsLeftAligned(line))
                    break;
                if (LineIsIndentedLessThan(line, leader))
                    break;
                int save = i;
                HContainer test = ParseProv1(line);
                i = save;
                if (test is not null)
                    break;
                i += 1;
                container.Add(line);
            }
        }

        private void AddFollowingToIntroOrWrapUp(WLine leader, List<IBlock> container)
        {
            while (i < Document.Body.Count)
            {
                if (Current() is not WLine line)
                    break;
                if (line is WOldNumberedParagraph)
                    break;
                if (!IsLeftAligned(line))
                    break;
                if (LineIsIndentedLessThan(line, leader))
                    break;
                int save = i;
                HContainer test = ParseProv1(line);
                i = save;
                if (test is not null)
                    break;
                i += 1;
                container.Add(line);
            }
        }

    }

}
